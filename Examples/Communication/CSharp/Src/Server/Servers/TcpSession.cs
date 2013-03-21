using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Firefly;
using BaseSystem;
using Net;
using Communication.Json;
using Server.Algorithms;
using Server.Services;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class TcpSession
    {
        public TcpServer Server { get; private set; }
        private StreamedAsyncSocket Socket;
        public IPEndPoint RemoteEndPoint { get; private set; }

        private SessionContext Context;
        private ServerImplementation si;
        private ITcpVirtualTransportServer vts;
        private int NumBadCommands = 0;
        private Boolean IsDisposed = false;

        public TcpSession(TcpServer Server, StreamedAsyncSocket Socket, IPEndPoint RemoteEndPoint)
        {
            this.Server = Server;
            this.Socket = Socket;
            this.RemoteEndPoint = RemoteEndPoint;

            Context = new SessionContext();
            Context.SessionToken = Cryptography.CreateRandom(4);
            Context.Quit += Quit;
            Context.Authenticated += () => Server.NotifySessionAuthenticated(this);

            si = new ServerImplementation(Server.ServerContext, Context);
            si.RegisterCrossSessionEvents();
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
            if (Server.SerializationProtocolType == SerializationProtocolType.Binary)
            {
                BinaryCountPacketServer.CheckCommandAllowedDelegate cca = CommandName =>
                {
                    if (Server.CheckCommandAllowed == null) { return true; }
                    return Server.CheckCommandAllowed(Context, CommandName);
                };
                vts = new BinaryCountPacketServer(law, cca);
            }
            else if (Server.SerializationProtocolType == SerializationProtocolType.Json)
            {
                JsonLinePacketServer.CheckCommandAllowedDelegate cca = CommandName =>
                {
                    if (Server.CheckCommandAllowed == null) { return true; }
                    return Server.CheckCommandAllowed(Context, CommandName);
                };
                vts = new JsonLinePacketServer(law, cca);
            }
            else
            {
                throw new InvalidOperationException("InvalidSerializationProtocol: " + Server.SerializationProtocolType.ToString());
            }
            vts.ServerEvent += WriteCommand;
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
            Server.ServerContext.SessionSet.DoAction(ss =>
            {
                if (ss.Contains(Context))
                {
                    ss.Remove(Context);
                }
            });

            si.UnregisterCrossSessionEvents();

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

            Socket.Shutdown(SocketShutdown.Both);
            Socket.Dispose();

            Context.Dispose();

            IsExitingValue.Update(b => false);

            if (Server.EnableLogSystem)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExit" });
            }
        }

        //线程安全
        private void StopAsync()
        {
            bool Done = false;
            IsExitingValue.Update(b =>
            {
                Done = b;
                if (!b)
                {
                    Socket.Shutdown(SocketShutdown.Receive);
                    if (Server.EnableLogSystem)
                    {
                        Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExitAsync" });
                    }
                    Server.NotifySessionQuit(this);
                }
                return true;
            });
            if (Done) { return; }
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
                //WriteLine(@"<cross-domain-policy><allow-access-from domain=""*"" to-ports=""*""/></cross-domain-policy>" + "\0"); //发送安全策略
                PushCommand(SessionCommand.CreateReadRaw());
            }
            catch (Exception ex)
            {
                OnCriticalError(ex, new StackTrace(true));
                StopAsync();
            }
        }

        private enum SessionCommandTag
        {
            Read = 0,
            Write = 1,
            ReadRaw = 2
        }
        private class SessionCommand
        {
            public SessionCommandTag _Tag;
            public int Read;
            public Unit Write;
            public Unit ReadRaw;

            public static SessionCommand CreateRead(int Value) { return new SessionCommand { _Tag = SessionCommandTag.Read, Read = Value }; }
            public static SessionCommand CreateWrite() { return new SessionCommand { _Tag = SessionCommandTag.Write, Write = new Unit() }; }
            public static SessionCommand CreateReadRaw() { return new SessionCommand { _Tag = SessionCommandTag.ReadRaw, ReadRaw = new Unit() }; }

            public Boolean OnRead { get { return _Tag == SessionCommandTag.Read; } }
            public Boolean OnWrite { get { return _Tag == SessionCommandTag.Write; } }
            public Boolean OnReadRaw { get { return _Tag == SessionCommandTag.ReadRaw; } }
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

        private void Quit()
        {
            LockSessionCommand();

            Action a = () =>
            {
                StopAsync();
            };

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
        private void PushCommand(SessionCommand sc)
        {
            LockSessionCommand();

            Action a;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                a = () =>
                {
                    MessageLoop(sc);
                };
            }
            else
            {
                a = () =>
                {
                    try
                    {
                        MessageLoop(sc);
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
        private static Boolean IsSocketErrorKnown(SocketError se)
        {
            if (se == SocketError.ConnectionAborted) { return true; }
            if (se == SocketError.ConnectionReset) { return true; }
            if (se == SocketError.Shutdown) { return true; }
            if (se == SocketError.OperationAborted) { return true; }
            if (se == SocketError.Interrupted) { return true; }
            return false;
        }

        private void MessageLoop(SessionCommand sc)
        {
            if (sc.OnRead)
            {
                var Count = sc.Read;
                var r = vts.Handle(Count);
                if (r.OnContinue)
                {
                }
                else if (r.OnCommand || r.OnBadCommand || r.OnBadCommandLine)
                {
                    ReadCommand(r);
                    var RemainCount = vts.GetReadBuffer().Count;
                    if (RemainCount > 0)
                    {
                        PushCommand(SessionCommand.CreateRead(0));
                        return;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }

                PushCommand(SessionCommand.CreateReadRaw());
            }
            else if (sc.OnWrite)
            {
                var Bytes = vts.TakeWriteBuffer();
                LockSessionCommand();
                Socket.SendAsync
                (
                    Bytes,
                    0,
                    Bytes.Length,
                    () =>
                    {
                        ReleaseSessionCommand();
                    },
                    se =>
                    {
                        ReleaseSessionCommand();
                        if (!IsSocketErrorKnown(se))
                        {
                            OnCriticalError((new SocketException((int)se)), new StackTrace(true));
                        }
                        StopAsync();
                    }
                );
            }
            else if (sc.OnReadRaw)
            {
                Action<int> CompletedInner = Count =>
                {
                    if (Count <= 0)
                    {
                        StopAsync();
                        return;
                    }
                    PushCommand(SessionCommand.CreateRead(Count));
                };
                Action<int> Completed;
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Completed = Count => CompletedInner(Count);
                }
                else
                {
                    Completed = Count =>
                    {
                        try
                        {
                            CompletedInner(Count);
                        }
                        catch (Exception ex)
                        {
                            OnCriticalError(ex, new StackTrace(true));
                            StopAsync();
                        }
                    };
                }
                Action<SocketError> Faulted = se =>
                {
                    if (!IsSocketErrorKnown(se))
                    {
                        OnCriticalError((new SocketException((int)se)), new StackTrace(true));
                    }
                    StopAsync();
                };
                if (IsExitingValue.Check(b => b)) { return; }
                var Buffer = vts.GetReadBuffer();
                var BufferLength = Buffer.Offset + Buffer.Count;
                Socket.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private void ReadCommand(TcpVirtualTransportServerHandleResult r)
        {
            if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
            {
                return;
            }

            if (r.OnContinue)
            {
                throw new InvalidProgramException();
            }
            else if (r.OnCommand)
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
                    PushCommand(SessionCommand.CreateWrite());
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
                    RaiseError("", String.Format(@"""{0}"": Too many bad commands, closing transmission channel.", CommandLine));
                    StopAsync();
                }
                else
                {
                    RaiseError("", String.Format(@"""{0}"":  recognized.", CommandLine));
                }
            }
            else
            {
                throw new InvalidProgramException();
            }
        }

        //线程安全
        public void WriteCommand()
        {
            if (!IsRunningValue.Check(b => b)) { throw new InvalidOperationException("NotRunning"); }
            PushCommand(SessionCommand.CreateWrite());
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
