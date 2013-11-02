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

namespace Server
{
    public partial class Tcp<TServerContext>
    {
        /// <summary>
        /// 本类的所有非继承的公共成员均是线程安全的。
        /// </summary>
        public class TcpSession
        {
            public TcpServer Server { get; private set; }
            private StreamedAsyncSocket Socket;
            public IPEndPoint RemoteEndPoint { get; private set; }

            private ISessionContext Context;
            private IServerImplementation si;
            private ITcpVirtualTransportServer vts;
            private int NumBadCommands = 0;
            private Boolean IsDisposed = false;

            private LockedVariable<Boolean> IsOnWrite = new LockedVariable<Boolean>(false);
            private Byte[] WriteBuffer;

            public TcpSession(TcpServer Server, StreamedAsyncSocket Socket, IPEndPoint RemoteEndPoint)
            {
                this.Server = Server;
                this.Socket = Socket;
                this.RemoteEndPoint = RemoteEndPoint;
                Socket.TimedOut += StopAsync;

                Context = Server.ServerContext.CreateSessionContext();
                Context.Quit += Quit;
                Context.Authenticated += () => Server.NotifySessionAuthenticated(this);

                if (Server.SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    var p = Server.ServerContext.CreateServerImplementationWithBinaryAdapter(Context);
                    si = p.Key;
                    var a = p.Value;
                    BinaryCountPacketServer.CheckCommandAllowedDelegate cca = CommandName =>
                    {
                        if (Server.CheckCommandAllowed == null) { return true; }
                        return Server.CheckCommandAllowed(Context, CommandName);
                    };
                    var bcps = new BinaryCountPacketServer(a, cca);
                    vts = bcps;
                }
                else if (Server.SerializationProtocolType == SerializationProtocolType.Json)
                {
                    var p = Server.ServerContext.CreateServerImplementationWithJsonAdapter(Context);
                    si = p.Key;
                    var a = p.Value;
                    JsonLinePacketServer.CheckCommandAllowedDelegate cca = CommandName =>
                    {
                        if (Server.CheckCommandAllowed == null) { return true; }
                        return Server.CheckCommandAllowed(Context, CommandName);
                    };
                    vts = new JsonLinePacketServer(a, cca);
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

                Socket.Shutdown(SocketShutdown.Both);
                Socket.Dispose();

                Context.Dispose();

                IsExitingValue.Update(b => false);

                if (Server.ServerContext.EnableLogSystem)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExit" });
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
                        if (Server.ServerContext.EnableLogSystem)
                        {
                            Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExitAsync" });
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

                    Server.ServerContext.RegisterSession(Context);
                    Server.SessionMappings.DoAction(Mappings => Mappings.Add(Context, this));

                    if (Server.ServerContext.EnableLogSystem)
                    {
                        Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionEnter" });
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
            private class SessionCommandQueue
            {
                public bool IsRunning;
                public Queue<Action> Queue;
            }
            private LockedVariable<SessionCommandQueue> CommandQueue = new LockedVariable<SessionCommandQueue>(new SessionCommandQueue { IsRunning = false, Queue = new Queue<Action>() });
            private AutoResetEvent CommandQueueCompleted = new AutoResetEvent(false);
            private void ExecuteCommandQueue()
            {
                int Count = 0;
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
                    Count += 1;
                    if (Count >= 256)
                    {
                        ThreadPool.QueueUserWorkItem(o => ExecuteCommandQueue());
                        break;
                    }
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
            private void PushCommand(SessionCommand sc)
            {
                LockSessionCommand();

                Action a;
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    a = () =>
                    {
                        try
                        {
                            MessageLoop(sc);
                        }
                        catch (SocketException)
                        {
                            StopAsync();
                        }
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
                        catch (SocketException)
                        {
                            StopAsync();
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
            private static Boolean IsSocketErrorKnown(Exception ex)
            {
                var sex = ex as SocketException;
                if (sex == null) { return false; }
                var se = sex.SocketErrorCode;
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
                    LockSessionCommand();
                    var Locked = false;
                    IsOnWrite.Update(b =>
                    {
                        if (b)
                        {
                            PushCommand(SessionCommand.CreateWrite());
                            return b;
                        }
                        Locked = true;
                        return true;
                    });
                    if (!Locked)
                    {
                        ReleaseSessionCommand();
                        return;
                    }
                    var ByteArrays = vts.TakeWriteBuffer();
                    if (ByteArrays.Length == 0)
                    {
                        IsOnWrite.Update(b =>
                        {
                            if (!b)
                            {
                                throw new InvalidOperationException();
                            }
                            return false;
                        });
                        ReleaseSessionCommand();
                        return;
                    }
                    var TotalLength = ByteArrays.Sum(b => b.Length);
                    if ((WriteBuffer == null) || (TotalLength > WriteBuffer.Length))
                    {
                        WriteBuffer = new Byte[GetMinNotLessPowerOfTwo(TotalLength)];
                    }
                    var Offset = 0;
                    foreach (var b in ByteArrays)
                    {
                        Array.Copy(b, 0, WriteBuffer, Offset, b.Length);
                        Offset += b.Length;
                    }
                    Socket.SendAsync
                    (
                        WriteBuffer,
                        0,
                        TotalLength,
                        () =>
                        {
                            try
                            {
                                IsOnWrite.Update(b =>
                                {
                                    if (!b)
                                    {
                                        throw new InvalidOperationException();
                                    }
                                    return false;
                                });
                            }
                            finally
                            {
                                ReleaseSessionCommand();
                            }
                        },
                        ex =>
                        {
                            try
                            {
                                IsOnWrite.Update(b =>
                                {
                                    if (!b)
                                    {
                                        throw new InvalidOperationException();
                                    }
                                    return false;
                                });
                            }
                            finally
                            {
                                ReleaseSessionCommand();
                            }
                            if (!IsSocketErrorKnown(ex))
                            {
                                OnCriticalError(ex, new StackTrace(true));
                            }
                            StopAsync();
                        }
                    );
                }
                else if (sc.OnReadRaw)
                {
                    //读取不是必须的，所以不锁定
                    Action<int> Completed = Count =>
                    {
                        if (Count <= 0)
                        {
                            StopAsync();
                            return;
                        }
                        PushCommand(SessionCommand.CreateRead(Count));
                    };
                    Action<Exception> Faulted = ex =>
                    {
                        if (!IsSocketErrorKnown(ex))
                        {
                            OnCriticalError(ex, new StackTrace(true));
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

            private static int GetMinNotLessPowerOfTwo(int v)
            {
                //计算不小于TotalLength的最小2的幂
                if (v < 1) { return 1; }
                var n = 0;
                var z = v - 1;
                while (z != 0)
                {
                    z >>= 1;
                    n += 1;
                }
                var Value = 1 << n;
                if (Value == 0) { throw new InvalidOperationException(); }
                return Value;
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
                        var CurrentTime = DateTime.UtcNow;
                        Context.RequestTime = CurrentTime;
                        if (Server.ServerContext.EnableLogPerformance)
                        {
                            var sw = new Stopwatch();
                            sw.Start();
                            r.Command.ExecuteCommand();
                            sw.Stop();
                            Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Message = String.Format("Time {0}ms", sw.ElapsedMilliseconds) });
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
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Unk", Message = Info });
                }
            }

            //线程安全
            private void OnCriticalError(Exception ex, StackTrace s)
            {
                if (Server.ServerContext.EnableLogCriticalError)
                {
                    var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Crtcl", Message = Info });
                }
            }
        }
    }
}
