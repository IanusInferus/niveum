using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using BaseSystem;

namespace Server
{
    public partial class Http<TServerContext>
    {
        /// <summary>
        /// 本类的所有非继承的公共成员均是线程安全的。
        /// </summary>
        public class HttpSession
        {
            public HttpServer Server { get; private set; }
            public IPEndPoint RemoteEndPoint { get; private set; }

            private LockedVariable<DateTime> LastActiveTimeValue;
            public DateTime LastActiveTime { get { return LastActiveTimeValue.Check(v => v); } }

            private ISessionContext Context;
            private IServerImplementation si;
            private IHttpVirtualTransportServer vts;
            private int NumBadCommands = 0;
            private Boolean IsDisposed = false;

            public HttpSession(HttpServer Server, IPEndPoint RemoteEndPoint)
            {
                this.Server = Server;
                this.RemoteEndPoint = RemoteEndPoint;
                this.LastActiveTimeValue = new LockedVariable<DateTime>(DateTime.UtcNow);

                Context = Server.ServerContext.CreateSessionContext();
                Context.Quit += Quit;
                Context.Authenticated += () => Server.NotifySessionAuthenticated(this);

                var p = Server.ServerContext.CreateServerImplementationWithJsonAdapter(Context);
                si = p.Key;
                var a = p.Value;
                JsonHttpPacketServer.CheckCommandAllowedDelegate cca = CommandName =>
                {
                    if (Server.CheckCommandAllowed == null) { return true; }
                    return Server.CheckCommandAllowed(Context, CommandName);
                };
                vts = new JsonHttpPacketServer(a, cca);
            }

            public void Dispose()
            {
                if (IsDisposed) { return; }
                IsDisposed = true;

                IsExitingValue.Update(b => true);

                Server.SessionMappings.DoAction(Mappings =>
                {
                    if (Mappings.ContainsKey(Context))
                    {
                        Mappings.Remove(Context);
                    }
                });
                Server.ServerContext.TryUnregisterSession(Context);

                si.Dispose();

                IsRunningValue.Update(b => false);
                while (NumSessionCommand.Check(n => n != 0))
                {
                    NumSessionCommandUpdated.WaitOne();
                }
                NumSessionCommandUpdated.Dispose();

                while (CommandQueue.Check(q => q.IsRunning))
                {
                    CommandQueueCompleted.WaitOne();
                }
                CommandQueueCompleted.Dispose();

                Context.Dispose();

                IsExitingValue.Update(b => false);

                if (Server.ServerContext.EnableLogSystem)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Name = "SessionExit", Message = "" });
                }
            }

            //线程安全
            private void StopAsync()
            {
                bool Done = false;
                IsExitingValue.Update(b =>
                {
                    if (!IsRunningValue.Check(bb => bb))
                    {
                        Done = true;
                        return b;
                    }
                    Done = b;
                    return true;
                });
                if (Done) { return; }

                if (Server.ServerContext.EnableLogSystem)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Name = "SessionExitAsync", Message = "" });
                }
                Server.NotifySessionQuit(this);
            }

            public void Start()
            {
                IsRunningValue.Update
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        return true;
                    }
                );

                try
                {
                    Context.RemoteEndPoint = RemoteEndPoint;

                    Server.ServerContext.RegisterSession(Context);
                    Server.SessionMappings.DoAction(Mappings => Mappings.Add(Context, this));

                    if (Server.ServerContext.EnableLogSystem)
                    {
                        Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Name = "SessionEnter", Message = "" });
                    }
                }
                catch (Exception ex)
                {
                    OnCriticalError(ex, new StackTrace(true));
                    StopAsync();
                }
            }

            public Boolean Push(HttpListenerContext ListenerContext, String NewSessionId)
            {
                var Pushed = true;
                IsExitingValue.DoAction
                (
                    b =>
                    {
                        if (b)
                        {
                            Pushed = false;
                            return;
                        }
                        IsRunningValue.DoAction
                        (
                            bb =>
                            {
                                if (!bb)
                                {
                                    Pushed = false;
                                    return;
                                }

                                PushCommand(ListenerContext, NewSessionId);
                            }
                        );
                    }
                );

                return Pushed;
            }

            private AutoResetEvent NumSessionCommandUpdated = new AutoResetEvent(false);
            private LockedVariable<int> NumSessionCommand = new LockedVariable<int>(0);
            private class SessionCommandQueue
            {
                public bool IsRunning;
                public Queue<Action> Queue;
            }
            private LockedVariable<SessionCommandQueue> CommandQueue = new LockedVariable<SessionCommandQueue>(new SessionCommandQueue { IsRunning = false, Queue = new Queue<Action>() });
            private AutoResetEvent CommandQueueCompleted = new AutoResetEvent(false);
            private void ExecuteCommandQueue()
            {
                while (true)
                {
                    Action a = null;
                    CommandQueue.DoAction
                    (
                        q =>
                        {
                            if (q.Queue.Count > 0)
                            {
                                a = q.Queue.Dequeue();
                            }
                            else
                            {
                                q.IsRunning = false;
                                CommandQueueCompleted.Set();
                            }
                        }
                    );
                    if (a == null)
                    {
                        return;
                    }
                    a();
                }
            }

            private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
            private LockedVariable<Boolean> IsExitingValue = new LockedVariable<Boolean>(false);
            public Boolean IsRunning
            {
                get
                {
                    return IsRunningValue.Check(b => b);
                }
            }

            private void LockSessionCommand()
            {
                NumSessionCommand.Update(n => n + 1);
                NumSessionCommandUpdated.Set();
            }
            private void ReleaseSessionCommand()
            {
                NumSessionCommand.Update(n =>
                {
                    NumSessionCommandUpdated.Set();
                    return n - 1;
                });
            }

            private void Quit()
            {
                LockSessionCommand();

                Action a = () =>
                {
                    StopAsync();
                };

                CommandQueue.DoAction
                (
                    q =>
                    {
                        q.Queue.Enqueue(a);
                        q.Queue.Enqueue(ReleaseSessionCommand);
                        if (!q.IsRunning)
                        {
                            q.IsRunning = true;
                            ThreadPool.QueueUserWorkItem(o => ExecuteCommandQueue());
                        }
                    }
                );
            }
            private void PushCommand(HttpListenerContext ListenerContext, String NewSessionId)
            {
                LockSessionCommand();

                Action a;
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    a = () =>
                    {
                        MessageLoop(ListenerContext, NewSessionId);
                    };
                }
                else
                {
                    a = () =>
                    {
                        try
                        {
                            MessageLoop(ListenerContext, NewSessionId);
                        }
                        catch (Exception ex)
                        {
                            OnCriticalError(ex, new StackTrace(true));
                            StopAsync();
                        }
                    };
                }

                CommandQueue.DoAction
                (
                    q =>
                    {
                        q.Queue.Enqueue(a);
                        q.Queue.Enqueue(ReleaseSessionCommand);
                        if (!q.IsRunning)
                        {
                            q.IsRunning = true;
                            ThreadPool.QueueUserWorkItem(o => ExecuteCommandQueue());
                        }
                    }
                );
            }

            private void MessageLoop(HttpListenerContext ListenerContext, String NewSessionId)
            {
                String Data;
                using (var InputStream = ListenerContext.Request.InputStream.AsReadable())
                {
                    Data = ListenerContext.Request.ContentEncoding.GetString(InputStream.Read((int)(ListenerContext.Request.ContentLength64)));
                }
                var Query = HttpListenerRequestExtension.GetQuery(ListenerContext.Request);
                if (Data == "")
                {
                    if (Query.ContainsKey("data"))
                    {
                        Data = Query["data"];
                    }
                }
                JArray Objects;
                try
                {
                    Objects = JToken.Parse(Data) as JArray;
                }
                catch
                {
                    ListenerContext.Response.StatusCode = 400;
                    ListenerContext.Response.Close();
                    StopAsync();
                    return;
                }
                if (Objects == null || Objects.Any(j => j.Type != JTokenType.Object))
                {
                    ListenerContext.Response.StatusCode = 400;
                    ListenerContext.Response.Close();
                    StopAsync();
                    return;
                }

                foreach (var co in Objects)
                {
                    var r = vts.Handle(co as JObject);
                    if (r.OnCommand || r.OnBadCommand || r.OnBadCommandLine)
                    {
                        ReadCommand(r);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                var jo = new JObject();
                jo["commands"] = new JArray(vts.TakeWriteBuffer());
                jo["sessionid"] = NewSessionId;
                var Result = jo.ToString(Newtonsoft.Json.Formatting.None);
                {
                    if (Query.ContainsKey("callback"))
                    {
                        var CallbackName = Query["callback"];
                        Result = "{0}({1});".Formats(CallbackName, Result);
                    }
                }
                var Bytes = TextEncoding.UTF8.GetBytes(Result);
                ListenerContext.Response.StatusCode = 200;
                ListenerContext.Response.AddHeader("Accept-Ranges", "none");
                ListenerContext.Response.ContentEncoding = System.Text.Encoding.UTF8;
                ListenerContext.Response.ContentLength64 = Bytes.Length;
                ListenerContext.Response.ContentType = "application/json; charset=utf-8";
                using (var OutputStream = ListenerContext.Response.OutputStream.AsWritable())
                {
                    OutputStream.Write(Bytes);
                }
                ListenerContext.Response.Close();

                LastActiveTimeValue.Update(v => DateTime.UtcNow);
            }

            private void ReadCommand(HttpVirtualTransportServerHandleResult r)
            {
                if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                {
                    return;
                }

                if (r.OnCommand)
                {
                    var CommandName = r.Command.CommandName;

                    Action a = () =>
                    {
                        var CurrentTime = DateTime.UtcNow;
                        Context.RequestTime = CurrentTime;
                        if (Server.ServerContext.EnableLogPerformance)
                        {
                            var sw = new Stopwatch();
                            sw.Start();
                            r.Command.ExecuteCommand();
                            sw.Stop();
                            Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Name = CommandName, Message = String.Format("{0}ms", sw.ElapsedMilliseconds) });
                        }
                        else
                        {
                            r.Command.ExecuteCommand();
                        }
                    };

                    if (Debugger.IsAttached)
                    {
                        a();
                    }
                    else
                    {
                        try
                        {
                            a();
                        }
                        catch (Exception ex)
                        {
                            RaiseUnknownError(CommandName, ex, new StackTrace(true));
                        }
                    }
                }
                else if (r.OnBadCommand)
                {
                    var CommandName = r.BadCommand.CommandName;

                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError(CommandName, "Too many bad commands, closing transmission channel.");
                        StopAsync();
                    }
                    else
                    {
                        RaiseError(CommandName, "Not recognized.");
                    }
                }
                else if (r.OnBadCommandLine)
                {
                    var CommandLine = r.BadCommandLine.CommandLine;

                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError(String.Format(@"""{0}""", CommandLine), "Too many bad commands, closing transmission channel.");
                        StopAsync();
                    }
                    else
                    {
                        RaiseError(String.Format(@"""{0}""", CommandLine), "Not recognized.");
                    }
                }
                else
                {
                    throw new InvalidProgramException();
                }
            }

            //线程安全
            public void RaiseError(String CommandName, String Message)
            {
                si.RaiseError(CommandName, Message);
            }
            //线程安全
            public void RaiseUnknownError(String CommandName, Exception ex, StackTrace s)
            {
                var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                if (Server.ServerContext.ClientDebug)
                {
                    si.RaiseError(CommandName, Info);
                }
                else
                {
                    si.RaiseError(CommandName, "Internal server error.");
                }
                if (Server.ServerContext.EnableLogUnknownError)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Unk", Name = "Exception", Message = Info });
                }
            }

            //线程安全
            private void OnCriticalError(Exception ex, StackTrace s)
            {
                if (Server.ServerContext.EnableLogCriticalError)
                {
                    var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Crtcl", Name = "Exception", Message = Info });
                }
            }
        }
    }
}
