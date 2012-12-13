using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Server.Algorithms;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class ManagedTcpSession : TcpSession<ManagedTcpServer, ManagedTcpSession>
    {
        private SessionContext Context;
        private int NumBadCommands = 0;

        public ManagedTcpSession()
        {
            Context = new SessionContext();
            Context.SessionToken = Cryptography.CreateRandom(4);
            Context.Quit += StopAsync;
        }

        protected override void StartInner()
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

                Server.SessionMappings.DoAction(Mappings =>
                {
                    Mappings.Add(Context, this);
                });

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
            public Byte[] Write;
            public Unit ReadRaw;

            public static SessionCommand CreateRead(int Value) { return new SessionCommand { _Tag = SessionCommandTag.Read, Read = Value }; }
            public static SessionCommand CreateWrite(Byte[] Value) { return new SessionCommand { _Tag = SessionCommandTag.Write, Write = Value }; }
            public static SessionCommand CreateReadRaw() { return new SessionCommand { _Tag = SessionCommandTag.ReadRaw, ReadRaw = new Unit() }; }

            public Boolean OnRead { get { return _Tag == SessionCommandTag.Read; } }
            public Boolean OnWrite { get { return _Tag == SessionCommandTag.Write; } }
            public Boolean OnReadRaw { get { return _Tag == SessionCommandTag.ReadRaw; } }
        }

        private AutoResetEvent NumAsyncOperationUpdated = new AutoResetEvent(false);
        private LockedVariable<int> NumAsyncOperation = new LockedVariable<int>(0);
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

        private void LockAsyncOperation()
        {
            NumAsyncOperation.Update(n => n + 1);
            NumAsyncOperationUpdated.Set();
        }
        private void ReleaseAsyncOperation()
        {
            NumAsyncOperation.Update(n => n - 1);
            NumAsyncOperationUpdated.Set();
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

        private void PushCommand(SessionCommand sc, TaskContinuationOptions? ContinuationOptions = null)
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
                    Task nt;
                    if (ContinuationOptions != null)
                    {
                        nt = t.ContinueWith(tt => a(), ContinuationOptions.Value);
                    }
                    else
                    {
                        nt = t.ContinueWith(tt => a());
                    }
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
        private void QueueCommand(SessionCommand sc)
        {
            PushCommand(sc, TaskContinuationOptions.PreferFairness);
        }
        private Boolean IsSocketErrorKnown(SocketError se)
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
                if (Count > 0)
                {
                    var r = Server.VirtualTransportServer.Handle(Context, Count);
                    if (r.OnRead)
                    {
                    }
                    else if (r.OnCommand || r.OnBadCommand || r.OnBadCommandLine)
                    {
                        ReadCommand(r);
                        var RemainCount = Server.VirtualTransportServer.GetReadBuffer(Context).Count;
                        if (RemainCount > 0)
                        {
                            PushCommand(SessionCommand.CreateRead(RemainCount));
                            return;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }

                    QueueCommand(SessionCommand.CreateReadRaw());
                }
                else
                {
                    StopAsync();
                }
            }
            else if (sc.OnWrite)
            {
                var Bytes = sc.Write;
                LockAsyncOperation();
                SendAsync(Bytes, 0, Bytes.Length, () => { ReleaseAsyncOperation(); }, se =>
                {
                    try
                    {
                        if (!IsSocketErrorKnown(se))
                        {
                            OnCriticalError((new SocketException((int)se)), new StackTrace(true));
                        }
                        StopAsync();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                });
            }
            else if (sc.OnReadRaw)
            {
                Action<int> CompletedInner = Count => PushCommand(SessionCommand.CreateRead(Count));
                Action<int> Completed;
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Completed = Count =>
                    {
                        try
                        {
                            CompletedInner(Count);
                        }
                        finally
                        {
                            ReleaseAsyncOperation();
                        }
                    };
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
                        finally
                        {
                            ReleaseAsyncOperation();
                        }
                    };
                }
                Action<SocketError> Faulted = se =>
                {
                    try
                    {
                        if (!IsSocketErrorKnown(se))
                        {
                            OnCriticalError((new SocketException((int)se)), new StackTrace(true));
                        }
                        StopAsync();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                };
                if (IsExitingValue.Check(b => b)) { return; }
                LockAsyncOperation();
                var Buffer = Server.VirtualTransportServer.GetReadBuffer(Context);
                var BufferLength = Buffer.Offset + Buffer.Count;
                ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private void ReadCommand(VirtualTransportHandleResult r)
        {
            if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
            {
                return;
            }

            if (r.OnRead)
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
                        var s = r.Command.ExecuteCommand();
                        sw.Stop();
                        Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Message = String.Format("Time {0}ms", sw.ElapsedMilliseconds) });
                        WriteCommand(r.Command.PackageOutput(s));
                    }
                    else
                    {
                        var s = r.Command.ExecuteCommand();
                        WriteCommand(r.Command.PackageOutput(s));
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
        public void WriteCommand(Byte[] Bytes)
        {
            PushCommand(SessionCommand.CreateWrite(Bytes));
        }
        //线程安全
        public void RaiseError(String CommandName, String Message)
        {
            Server.RaiseError(Context, CommandName, Message);
        }
        //线程安全
        public void RaiseUnknownError(String CommandName, Exception ex, StackTrace s)
        {
            var Info = ExceptionInfo.GetExceptionInfo(ex, s);
            if (Server.ClientDebug)
            {
                Server.RaiseError(Context, CommandName, Info);
            }
            else
            {
                Server.RaiseError(Context, CommandName, "Internal server error.");
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

            var ss = GetSocket();
            if (ss != null)
            {
                try
                {
                    ss.Shutdown(SocketShutdown.Receive);
                }
                catch (Exception)
                {
                }
            }
            if (Server.EnableLogSystem)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExit" });
            }
            Server.NotifySessionQuit(this);
        }
        protected override void StopInner()
        {
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
            }

            IsRunningValue.Update(b => false);
            while (NumSessionCommand.Check(n => n != 0))
            {
                NumSessionCommandUpdated.WaitOne();
            }
            while (NumAsyncOperation.Check(n => n != 0))
            {
                NumAsyncOperationUpdated.WaitOne();
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
    }
}
