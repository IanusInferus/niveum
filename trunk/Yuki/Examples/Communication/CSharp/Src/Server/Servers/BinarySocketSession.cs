using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
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
    public class BinarySocketSession : TcpSession<BinarySocketServer, BinarySocketSession>
    {
        private SessionContext Context;
        private int NumBadCommands = 0;

        public BinarySocketSession()
        {
            Context = new SessionContext();
            Context.SessionToken = Cryptography.CreateRandom4();
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

        private class Command
        {
            public String CommandName;
            public UInt32 CommandHash;
            public Byte[] Parameters;
        }
        private enum SessionCommandTag
        {
            Read = 0,
            Write = 1,
            ReadRaw = 2,
            Quit = 3
        }
        private class SessionCommand
        {
            public SessionCommandTag _Tag;
            public Command Read;
            public Command Write;
            public Unit ReadRaw;
            public Unit Quit;

            public static SessionCommand CreateRead(Command Value) { return new SessionCommand { _Tag = SessionCommandTag.Read, Read = Value }; }
            public static SessionCommand CreateWrite(Command Value) { return new SessionCommand { _Tag = SessionCommandTag.Write, Write = Value }; }
            public static SessionCommand CreateReadRaw() { return new SessionCommand { _Tag = SessionCommandTag.ReadRaw, ReadRaw = new Unit() }; }
            public static SessionCommand CreateQuit() { return new SessionCommand { _Tag = SessionCommandTag.Quit, Quit = new Unit() }; }

            public Boolean OnRead { get { return _Tag == SessionCommandTag.Read; } }
            public Boolean OnWrite { get { return _Tag == SessionCommandTag.Write; } }
            public Boolean OnReadRaw { get { return _Tag == SessionCommandTag.ReadRaw; } }
            public Boolean OnQuit { get { return _Tag == SessionCommandTag.Quit; } }
        }

        private AutoResetEvent NumAsyncOperationUpdated = new AutoResetEvent(false);
        private LockedVariable<int> NumAsyncOperation = new LockedVariable<int>(0);
        private AutoResetEvent NumSessionCommandUpdated = new AutoResetEvent(false);
        private LockedVariable<int> NumSessionCommand = new LockedVariable<int>(0);
        private LockedVariable<Task> SessionTask = new LockedVariable<Task>(new Task(() => { }));
        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
        public Boolean IsRunning
        {
            get
            {
                return IsRunningValue.Check(b => b);
            }
        }

        private bool TryLockAsyncOperation()
        {
            bool Done = false;
            IsRunningValue.DoAction
            (
                b =>
                {
                    if (b)
                    {
                        NumAsyncOperation.Update(n => n + 1);
                        NumAsyncOperationUpdated.Set();
                        Done = true;
                    }
                }
            );
            return Done;
        }
        void LockAsyncOperation()
        {
            NumAsyncOperation.Update(n => n + 1);
            NumAsyncOperationUpdated.Set();
        }
        void ReleaseAsyncOperation()
        {
            NumAsyncOperation.Update(n => n - 1);
            NumAsyncOperationUpdated.Set();
        }

        private bool TryLockSessionCommand()
        {
            bool Done = false;
            IsRunningValue.DoAction
            (
                b =>
                {
                    if (b)
                    {
                        NumSessionCommand.Update(n => n + 1);
                        NumSessionCommandUpdated.Set();
                        Done = true;
                    }
                }
            );
            return Done;
        }
        void ReleaseSessionCommand()
        {
            NumSessionCommand.Update(n => n - 1);
            NumSessionCommandUpdated.Set();
        }
        
        private void PushCommand(SessionCommand sc, TaskContinuationOptions? ContinuationOptions = null)
        {
            if (!TryLockSessionCommand()) { return; }

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

        private class TryShiftResult
        {
            public Command Command;
            public int Position;
        }

        private class BufferStateMachine
        {
            private int State;
            // 0 初始状态
            // 1 已读取NameLength
            // 2 已读取CommandHash
            // 3 已读取Name
            // 4 已读取ParametersLength

            private Int32 CommandNameLength;
            private String CommandName;
            private UInt32 CommandHash;
            private Int32 ParametersLength;

            public BufferStateMachine()
            {
                State = 0;
            }

            public TryShiftResult TryShift(Byte[] Buffer, int Position, int Length)
            {
                if (State == 0)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            CommandNameLength = s.ReadInt32();
                        }
                        if (CommandNameLength < 0 || CommandNameLength > 128) { throw new InvalidOperationException(); }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        State = 1;
                        return r;
                    }
                    return null;
                }
                else if (State == 1)
                {
                    if (Length >= CommandNameLength)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            CommandName = TextEncoding.UTF16.GetString(s.Read(CommandNameLength));
                        }
                        var r = new TryShiftResult { Command = null, Position = Position + CommandNameLength };
                        State = 2;
                        return r;
                    }
                    return null;
                }
                else if (State == 2)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            CommandHash = s.ReadUInt32();
                        }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        State = 3;
                        return r;
                    }
                    return null;
                }
                if (State == 3)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            ParametersLength = s.ReadInt32();
                        }
                        if (ParametersLength < 0 || ParametersLength > 8 * 1024) { throw new InvalidOperationException(); }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        State = 4;
                        return r;
                    }
                    return null;
                }
                else if (State == 4)
                {
                    if (Length >= ParametersLength)
                    {
                        Byte[] Parameters;
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            Parameters = s.Read(ParametersLength);
                        }
                        var cmd = new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters };
                        var r = new TryShiftResult { Command = cmd, Position = Position + ParametersLength };
                        CommandNameLength = 0;
                        CommandName = null;
                        CommandHash = 0;
                        ParametersLength = 0;
                        State = 0;
                        return r;
                    }
                    return null;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private BufferStateMachine bsm = new BufferStateMachine();
        private Byte[] Buffer = new Byte[8 * 1024];
        private int BufferLength = 0;
        private Boolean MessageLoop(SessionCommand sc)
        {
            if (!IsRunningValue.Check(b => b))
            {
                if (!sc.OnWrite && !sc.OnQuit)
                {
                    return true;
                }
            }

            if (sc.OnRead)
            {
                ReadCommand(sc.Read);
            }
            else if (sc.OnWrite)
            {
                var cmd = sc.Write;
                var CommandNameBytes = TextEncoding.UTF16.GetBytes(cmd.CommandName);
                Byte[] Bytes;
                using (var s = Streams.CreateMemoryStream())
                {
                    s.WriteInt32(CommandNameBytes.Length);
                    s.Write(CommandNameBytes);
                    s.WriteUInt32(cmd.CommandHash);
                    s.WriteInt32(cmd.Parameters.Length);
                    s.Write(cmd.Parameters);
                    s.Position = 0;
                    Bytes = s.Read((int)(s.Length));
                }
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
                Action<int> CompletedInner = Count =>
                {
                    if (Count == 0)
                    {
                        StopAsync();
                        return;
                    }
                    var FirstPosition = 0;
                    BufferLength += Count;
                    while (true)
                    {
                        var r = bsm.TryShift(Buffer, FirstPosition, BufferLength - FirstPosition);
                        if (r == null)
                        {
                            break;
                        }
                        FirstPosition = r.Position;

                        if (r.Command != null)
                        {
                            PushCommand(SessionCommand.CreateRead(r.Command));
                        }
                    }
                    if (FirstPosition > 0)
                    {
                        var CopyLength = BufferLength - FirstPosition;
                        for (int i = 0; i < CopyLength; i += 1)
                        {
                            Buffer[i] = Buffer[FirstPosition + i];
                        }
                        BufferLength = CopyLength;
                    }
                    QueueCommand(SessionCommand.CreateReadRaw());
                };
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
                if (!TryLockAsyncOperation()) { return true; }
                ReceiveAsync(Buffer, BufferLength, Buffer.Length - BufferLength, Completed, Faulted);
            }
            else if (sc.OnQuit)
            {
                return false;
            }
            else
            {
                throw new InvalidOperationException();
            }
            return true;
        }

        private void ReadCommand(Command cmd)
        {
            if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
            {
                return;
            }

            var CommandName = cmd.CommandName;
            var CommandHash = cmd.CommandHash;
            var Parameters = cmd.Parameters;

            var sv = Server.InnerServer;
            if (sv.HasCommand(CommandName, CommandHash) && (Server.CheckCommandAllowed != null ? Server.CheckCommandAllowed(Context, CommandName) : true))
            {
                Action a = () =>
                {
                    if (Server.EnableLogPerformance)
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        var s = Server.InnerServer.ExecuteCommand(Context, CommandName, CommandHash, Parameters);
                        sw.Stop();
                        Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Message = String.Format("Time {0}ms", sw.ElapsedMilliseconds) });
                        WriteCommand(CommandName, CommandHash, s);
                    }
                    else
                    {
                        var s = Server.InnerServer.ExecuteCommand(Context, CommandName, CommandHash, Parameters);
                        WriteCommand(CommandName, CommandHash, s);
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
            else
            {
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
        }

        //线程安全
        public void WriteCommand(String CommandName, UInt32 CommandHash, Byte[] Parameters)
        {
            PushCommand(SessionCommand.CreateWrite(new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters }));
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
                catch(Exception)
                {
                }
            }
            if (Server.EnableLogSystem)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExit" });
            }
            PushCommand(SessionCommand.CreateQuit());
            IsRunningValue.Update(b => false);
            Server.NotifySessionQuit(this);
        }
        protected override void StopInner()
        {
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

            PushCommand(SessionCommand.CreateQuit());
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
                        Buffer = null;
                    }
                    return null;
                }
            );
        }
    }
}
