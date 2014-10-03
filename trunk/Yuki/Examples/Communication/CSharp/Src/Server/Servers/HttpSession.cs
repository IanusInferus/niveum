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

            private class HttpReadContext
            {
                public HttpListenerContext ListenerContext;
                public String NewSessionId;
                public Action<HttpVirtualTransportServerHandleResult[]> OnSuccess;
                public Action OnFailure;
            }
            private class HttpWriteContext
            {
                public HttpListenerContext ListenerContext;
                public String NewSessionId;
                public Dictionary<String, String> Query;
            }
            private LockedVariable<HttpReadContext> ReadContext = new LockedVariable<HttpReadContext>(null);
            private LockedVariable<HttpWriteContext> WriteContext = new LockedVariable<HttpWriteContext>(null);
            private SessionStateMachine<HttpVirtualTransportServerHandleResult, Unit> ssm;

            public HttpSession(HttpServer Server, IPEndPoint RemoteEndPoint, Func<ISessionContext, KeyValuePair<IServerImplementation, IHttpVirtualTransportServer>> VirtualTransportServerFactory, Action<Action> QueueUserWorkItem)
            {
                this.Server = Server;
                this.RemoteEndPoint = RemoteEndPoint;
                this.LastActiveTimeValue = new LockedVariable<DateTime>(DateTime.UtcNow);
                ssm = new SessionStateMachine<HttpVirtualTransportServerHandleResult, Unit>(ex => false, OnCriticalError, OnShutdownRead, OnShutdownWrite, OnWrite, OnExecute, OnStartRawRead, OnExit, QueueUserWorkItem);

                Context = Server.ServerContext.CreateSessionContext();
                Context.Quit += ssm.NotifyExit;
                Context.Authenticated += () => Server.NotifySessionAuthenticated(this);
                Context.SecureConnectionRequired += c =>
                {
                    throw new NotImplementedException();
                };

                var Pair = VirtualTransportServerFactory(Context);
                si = Pair.Key;
                vts = Pair.Value;
            }

            private void OnShutdownRead()
            {
                HttpReadContext PairContext = null;
                ReadContext.Update(c =>
                {
                    PairContext = c;
                    return null;
                });
                if (PairContext != null)
                {
                    if (PairContext.ListenerContext != null)
                    {
                        PairContext.ListenerContext.Response.StatusCode = 400;
                        PairContext.ListenerContext.Response.Close();
                    }
                    if (PairContext.OnFailure != null)
                    {
                        PairContext.OnFailure();
                    }
                }
            }
            private void OnShutdownWrite()
            {
                HttpWriteContext PairWriteContext = null;
                WriteContext.Update(c =>
                {
                    PairWriteContext = c;
                    return null;
                });
                if (PairWriteContext != null)
                {
                    HandleRawWrite(PairWriteContext.ListenerContext, PairWriteContext.NewSessionId, PairWriteContext.Query);
                }
            }
            private void OnWrite(Unit w, Action OnSuccess, Action OnFailure)
            {
                OnSuccess();
            }
            private void OnExecute(HttpVirtualTransportServerHandleResult r, Action OnSuccess, Action OnFailure)
            {
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
                            Action OnSuccessInner = () =>
                            {
                                sw.Stop();
                                Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Name = CommandName, Message = String.Format("{0}ms", sw.ElapsedMilliseconds) });
                                ssm.NotifyWrite(new Unit());
                                OnSuccess();
                            };
                            Action<Exception> OnFailureInner = ex =>
                            {
                                RaiseUnknownError(CommandName, ex, new StackTrace(true));
                                OnSuccess();
                            };
                            r.Command.ExecuteCommand(OnSuccessInner, OnFailureInner);
                        }
                        else
                        {
                            Action OnSuccessInner = () =>
                            {
                                ssm.NotifyWrite(new Unit());
                                OnSuccess();
                            };
                            Action<Exception> OnFailureInner = ex =>
                            {
                                RaiseUnknownError(CommandName, ex, new StackTrace(true));
                                OnSuccess();
                            };
                            r.Command.ExecuteCommand(OnSuccessInner, OnFailureInner);
                        }
                    };

                    ssm.AddToActionQueue(a);
                }
                else if (r.OnBadCommand)
                {
                    var CommandName = r.BadCommand.CommandName;

                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError(CommandName, "Too many bad commands, closing transmission channel.");
                        OnFailure();
                    }
                    else
                    {
                        RaiseError(CommandName, "Not recognized.");
                        OnSuccess();
                    }
                }
                else if (r.OnBadCommandLine)
                {
                    var CommandLine = r.BadCommandLine.CommandLine;

                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError("", String.Format(@"""{0}"": Too many bad commands, closing transmission channel.", CommandLine));
                        OnFailure();
                    }
                    else
                    {
                        RaiseError("", String.Format(@"""{0}"":  recognized.", CommandLine));
                        OnSuccess();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            private void OnStartRawRead(Action<HttpVirtualTransportServerHandleResult[]> OnSuccess, Action OnFailure)
            {
                HttpWriteContext PairWriteContext = null;
                WriteContext.Update(c =>
                {
                    if (c != null)
                    {
                        PairWriteContext = c;
                    }
                    return null;
                });
                if (PairWriteContext != null)
                {
                    HandleRawWrite(PairWriteContext.ListenerContext, PairWriteContext.NewSessionId, PairWriteContext.Query);
                }

                HttpReadContext PairContext = null;
                var Pushed = true;
                ReadContext.Update(c =>
                {
                    if (c != null)
                    {
                        if (c.ListenerContext != null)
                        {
                            c.OnSuccess = OnSuccess;
                            c.OnFailure = OnFailure;
                            PairContext = c;
                            Pushed = true;
                            return null;
                        }
                        else
                        {
                            Pushed = false;
                            return c;
                        }
                    }
                    Pushed = true;
                    return new HttpReadContext { ListenerContext = null, NewSessionId = null, OnSuccess = OnSuccess, OnFailure = OnFailure };
                });

                if (PairContext != null)
                {
                    HandleRawRead(PairContext.ListenerContext, PairContext.NewSessionId, PairContext.OnSuccess, PairContext.OnFailure);
                }
                if (!Pushed)
                {
                    OnFailure();
                }
            }

            private void HandleRawRead(HttpListenerContext ListenerContext, String NewSessionId, Action<HttpVirtualTransportServerHandleResult[]> OnSuccess, Action OnFailure)
            {
                String Data;
                using (var InputStream = ListenerContext.Request.InputStream.AsReadable())
                {
                    Byte[] Bytes;
                    try
                    {
                        Bytes = InputStream.Read((int)(ListenerContext.Request.ContentLength64));
                    }
                    catch
                    {
                        OnFailure();
                        return;
                    }
                    Data = ListenerContext.Request.ContentEncoding.GetString(Bytes);
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
                    OnFailure();
                    return;
                }
                if (Objects == null || Objects.Any(j => j.Type != JTokenType.Object))
                {
                    ListenerContext.Response.StatusCode = 400;
                    ListenerContext.Response.Close();
                    OnFailure();
                    return;
                }

                var Results = new List<HttpVirtualTransportServerHandleResult>();
                foreach (var co in Objects)
                {
                    HttpVirtualTransportServerHandleResult Result;
                    try
                    {
                        Result = vts.Handle(co as JObject);
                    }
                    catch (Exception ex)
                    {
                        if ((ex is InvalidOperationException) && (ex.Message != ""))
                        {
                            Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Known", Name = "Exception", Message = ex.Message });
                        }
                        else
                        {
                            OnCriticalError(ex, new StackTrace(true));
                        }
                        OnFailure();
                        return;
                    }
                    if (Result.OnCommand || Result.OnBadCommand || Result.OnBadCommandLine)
                    {
                        Results.Add(Result);
                    }
                    else
                    {
                        OnFailure();
                    }
                }

                var Success = false;
                WriteContext.Update(c =>
                {
                    if (c != null)
                    {
                        Success = false;
                        return c;
                    }
                    Success = true;
                    return new HttpWriteContext { ListenerContext = ListenerContext, NewSessionId = NewSessionId, Query = Query };
                });
                if (!Success)
                {
                    OnFailure();
                }
                else
                {
                    OnSuccess(Results.ToArray());
                }
            }

            private void HandleRawWrite(HttpListenerContext ListenerContext, String NewSessionId, Dictionary<String, String> Query)
            {
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
                    try
                    {
                        OutputStream.Write(Bytes);
                    }
                    catch
                    {
                    }
                }
                try
                {
                    ListenerContext.Response.Close();
                }
                catch
                {
                }

                LastActiveTimeValue.Update(v => DateTime.UtcNow);
            }

            public void Dispose()
            {
                if (IsDisposed) { return; }
                IsDisposed = true;

                IsExitingValue.Update(b => true);
                ssm.NotifyExit();

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

                SpinWait.SpinUntil(() => ssm.IsExited());

                Context.Dispose();

                IsExitingValue.Update(b => false);

                if (Server.ServerContext.EnableLogSystem)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Name = "SessionExit", Message = "" });
                }
            }

            //线程安全
            private void OnExit()
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
                    ssm.Start();
                }
                catch (Exception ex)
                {
                    OnCriticalError(ex, new StackTrace(true));
                    ssm.NotifyFailure();
                }
            }

            public Boolean Push(HttpListenerContext ListenerContext, String NewSessionId)
            {
                HttpReadContext PairContext = null;
                var Pushed = true;
                ReadContext.Update(c =>
                {
                    if (c != null)
                    {
                        if (c.ListenerContext != null)
                        {
                            Pushed = false;
                            return c;
                        }
                        else
                        {
                            c.ListenerContext = ListenerContext;
                            c.NewSessionId = NewSessionId;
                            PairContext = c;
                            return null;
                        }
                    }
                    Pushed = true;
                    return new HttpReadContext { ListenerContext = ListenerContext, NewSessionId = NewSessionId, OnSuccess = null, OnFailure = null };
                });

                if (PairContext != null)
                {
                    HandleRawRead(PairContext.ListenerContext, PairContext.NewSessionId, PairContext.OnSuccess, PairContext.OnFailure);
                }
                return Pushed;
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
