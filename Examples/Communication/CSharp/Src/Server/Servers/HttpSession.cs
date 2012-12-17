using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Json;
using Server.Algorithms;
using Server.Services;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class HttpSession
    {
        public HttpServer Server { get; set; }
        public IPEndPoint RemoteEndPoint { get; private set; }

        private SessionContext Context;
        private ServerImplementation si;
        private IHttpVirtualTransportServer vts;
        private int NumBadCommands = 0;
        private Boolean IsDisposed = false;

        public HttpSession(IPEndPoint RemoteEndPoint)
        {
            this.RemoteEndPoint = RemoteEndPoint;

            Context = new SessionContext();
            Context.SessionToken = Cryptography.CreateRandom(4);
            Context.Quit += StopAsync;

            si = new ServerImplementation(Server.ServerContext, Context);
            var law = new JsonLogAspectWrapper(si);
            law.ClientCommandIn += (CommandName, Parameters) =>
            {
                if (Server.EnableLogNormalIn)
                {
                    var CommandLine = String.Format(@"{0} {1}", CommandName, Parameters);
                    Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "In", Message = CommandLine });
                }
            };
            law.ClientCommandOut += (CommandName, Parameters) =>
            {
                if (Server.EnableLogNormalOut)
                {
                    var CommandLine = String.Format(@"svr {0} {1}", CommandName, Parameters);
                    Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
                }
            };
            law.ServerCommand += (CommandName, Parameters) =>
            {
                if (Server.EnableLogNormalOut)
                {
                    var CommandLine = String.Format(@"svr {0} {1}", CommandName, Parameters);
                    Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
                }
            };
            JsonHttpPacketServer.CheckCommandAllowedDelegate cca = CommandName =>
            {
                if (Server.CheckCommandAllowed == null) { return true; }
                return Server.CheckCommandAllowed(Context, CommandName);
            };
            vts = new JsonHttpPacketServer(law, cca);
        }

        public void Dispose()
        {
            if (IsDisposed) { return; }
            IsDisposed = true;

            IsExitingValue.Update(b => true);

            if (Server != null)
            {
                Server.SessionMappings.DoAction(Mappings =>
                {
                    if (Mappings.ContainsKey(Context))
                    {
                        Mappings.Remove(Context);
                    }
                });
                Server.ServerContext.SessionSet.DoAction(ss =>
                {
                    if (ss.Contains(Context))
                    {
                        ss.Remove(Context);
                    }
                });
            }

            IsRunningValue.Update(b => false);
            while (NumSessionCommand.Check(n => n != 0))
            {
                NumSessionCommandUpdated.WaitOne();
            }

            SessionTask.Update
            (
                t =>
                {
                    if (t != null)
                    {
                        t.Wait();
                        t.Dispose();
                    }
                    return null;
                }
            );

            IsExitingValue.Update(b => false);
        }

        //线程安全
        private void StopAsync()
        {
            bool Done = false;
            IsExitingValue.Update(b =>
            {
                Done = b;
                return true;
            });
            if (Done) { return; }

            if (Server != null)
            {
                Server.SessionMappings.DoAction(Mappings =>
                {
                    if (Mappings.ContainsKey(Context))
                    {
                        Mappings.Remove(Context);
                    }
                });
            }

            if (Server.EnableLogSystem)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExit" });
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

                Server.ServerContext.SessionSet.DoAction(ss => ss.Add(Context));
                Server.SessionMappings.DoAction(Mappings => Mappings.Add(Context, this));

                SessionTask.DoAction(t => t.Start());

                if (Server.EnableLogSystem)
                {
                    Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionEnter" });
                }
            }
            catch (Exception ex)
            {
                OnCriticalError(ex, new StackTrace(true));
                StopAsync();
            }
        }

        public Boolean Push(HttpListenerContext Socket, String NewSessionId)
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

                            PushCommand(Socket, NewSessionId);
                        }
                    );
                }
            );

            return Pushed;
        }

        private AutoResetEvent NumSessionCommandUpdated = new AutoResetEvent(false);
        private LockedVariable<int> NumSessionCommand = new LockedVariable<int>(0);
        private LockedVariable<Task> SessionTask = new LockedVariable<Task>(new Task(() => { }));
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
            NumSessionCommand.Update(n => n - 1);
            NumSessionCommandUpdated.Set();
        }

        private void PushCommand(HttpListenerContext Socket, String NewSessionId)
        {
            LockSessionCommand();

            Action a;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                a = () =>
                {
                    MessageLoop(Socket, NewSessionId);
                };
            }
            else
            {
                a = () =>
                {
                    try
                    {
                        MessageLoop(Socket, NewSessionId);
                    }
                    catch (Exception ex)
                    {
                        OnCriticalError(ex, new StackTrace(true));
                        StopAsync();
                    }
                };
            }

            SessionTask.Update
            (
                t =>
                {
                    var nt = t.ContinueWith(tt => a());
                    nt = nt.ContinueWith
                    (
                        tt =>
                        {
                            ReleaseSessionCommand();
                        }
                    );
                    return nt;
                }
            );
        }

        private void MessageLoop(HttpListenerContext Socket, String NewSessionId)
        {
            String Data;
            using (var InputStream = Socket.Request.InputStream.AsReadable())
            {
                Data = Socket.Request.ContentEncoding.GetString(InputStream.Read((int)(Socket.Request.ContentLength64)));
            }
            var Lines = Data.UnifyNewLineToLf().Split('\n').Select(Line => Line.Trim(' ')).Where(Line => Line != "").ToArray();

            foreach (var Line in Lines)
            {
                var r = vts.Handle(Line);
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
            var Bytes = TextEncoding.UTF8.GetBytes(jo.ToString(Newtonsoft.Json.Formatting.None));
            Socket.Response.StatusCode = 200;
            Socket.Response.AddHeader("Accept-Ranges", "none");
            Socket.Response.ContentEncoding = System.Text.Encoding.UTF8;
            Socket.Response.ContentLength64 = Bytes.Length;
            using (var OutputStream = Socket.Response.OutputStream.AsWritable())
            {
                OutputStream.Write(Bytes);
            }
            Socket.Response.Close();
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
                    if (Server.EnableLogPerformance)
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        r.Command.ExecuteCommand();
                        sw.Stop();
                        Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Message = String.Format("Time {0}ms", sw.ElapsedMilliseconds) });
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
            if (Server.ClientDebug)
            {
                si.RaiseError(CommandName, Info);
            }
            else
            {
                si.RaiseError(CommandName, "Internal server error.");
            }
            if (Server.EnableLogUnknownError)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Unk", Message = Info });
            }
        }

        //线程安全
        private void OnCriticalError(Exception ex, StackTrace s)
        {
            if (Server.EnableLogCriticalError)
            {
                var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Crtcl", Message = Info });
            }
        }

        private void Logon()
        {
        }
    }
}
