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
    public partial class Streamed<TServerContext>
    {
        /// <summary>
        /// 本类的所有公共成员均是线程安全的。
        /// </summary>
        public class TcpSession
        {
            public TcpServer Server { get; private set; }
            private StreamedAsyncSocket Socket;
            public IPEndPoint RemoteEndPoint { get; private set; }

            private ISessionContext Context;
            private IServerImplementation si;
            private IStreamedVirtualTransportServer vts;
            private int NumBadCommands = 0;
            private Boolean IsDisposed = false;

            private Byte[] WriteBuffer;
            private SessionStateMachine<StreamedVirtualTransportServerHandleResult, Unit> ssm;

            public TcpSession(TcpServer Server, StreamedAsyncSocket Socket, IPEndPoint RemoteEndPoint, Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory, Action<Action> QueueUserWorkItem)
            {
                this.Server = Server;
                this.Socket = Socket;
                this.RemoteEndPoint = RemoteEndPoint;
                ssm = new SessionStateMachine<StreamedVirtualTransportServerHandleResult, Unit>(ex => ex is SocketException, OnCriticalError, OnShutdownRead, OnShutdownWrite, OnWrite, OnExecute, OnStartRawRead, OnExit, QueueUserWorkItem);
                Socket.TimedOut += ssm.NotifyFailure;

                Context = Server.ServerContext.CreateSessionContext();
                Context.Quit += ssm.NotifyExit;
                Context.Authenticated += () =>
                {
                    Socket.TimeoutSeconds = Server.SessionIdleTimeout;
                    Server.NotifySessionAuthenticated(this);
                };

                var rpst = new Rc4PacketServerTransformer();
                var Pair = VirtualTransportServerFactory(Context, rpst);
                si = Pair.Key;
                vts = Pair.Value;
                Context.SecureConnectionRequired += c =>
                {
                    rpst.SetSecureContext(c);
                };
                vts.ServerEvent += () => ssm.NotifyWrite(new Unit());
                vts.InputByteLengthReport += (CommandName, ByteLength) => Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "InBytes", Name = CommandName, Message = ByteLength.ToInvariantString() });
                vts.OutputByteLengthReport += (CommandName, ByteLength) => Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "OutBytes", Name = CommandName, Message = ByteLength.ToInvariantString() });
            }

            private void OnShutdownRead()
            {
                Socket.Shutdown(SocketShutdown.Receive);
            }
            private void OnShutdownWrite()
            {
                Socket.Shutdown(SocketShutdown.Send);
                Socket.Dispose();
            }
            private void OnWrite(Unit w, Action OnSuccess, Action OnFailure)
            {
                var ByteArrays = vts.TakeWriteBuffer();
                if (ByteArrays.Length == 0)
                {
                    OnSuccess();
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
                    OnSuccess,
                    ex =>
                    {
                        if (!IsSocketErrorKnown(ex))
                        {
                            OnCriticalError(ex, new StackTrace(true));
                        }
                        OnFailure();
                    }
                );
            }
            private void OnExecute(StreamedVirtualTransportServerHandleResult r, Action OnSuccess, Action OnFailure)
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
                                Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Name = CommandName, Message = String.Format("{0}μs", (sw.ElapsedTicks * 1000000) / Stopwatch.Frequency) });
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
            private void OnStartRawRead(Action<StreamedVirtualTransportServerHandleResult[]> OnSuccess, Action OnFailure)
            {
                Action<int> Completed = Count =>
                {
                    if (Count <= 0)
                    {
                        OnFailure();
                        return;
                    }
                    if (ssm.IsExited()) { return; }
                    var Results = new List<StreamedVirtualTransportServerHandleResult>();
                    var c = Count;
                    while (true)
                    {
                        StreamedVirtualTransportServerHandleResult Result;
                        try
                        {
                            Result = vts.Handle(c);
                        }
                        catch (Exception ex)
                        {
                            if ((ex is InvalidOperationException) && (ex.Message != ""))
                            {
                                Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Known", Name = "Exception", Message = ex.Message });
                            }
                            else if (!IsSocketErrorKnown(ex))
                            {
                                OnCriticalError(ex, new StackTrace(true));
                            }
                            OnFailure();
                            return;
                        }
                        c = 0;
                        if (Result.OnContinue)
                        {
                            break;
                        }
                        Results.Add(Result);
                    }
                    if (Results.Count == 0)
                    {
                        OnStartRawRead(OnSuccess, OnFailure);
                        return;
                    }
                    OnSuccess(Results.ToArray());
                };
                Action<Exception> Faulted = ex =>
                {
                    if (!IsSocketErrorKnown(ex))
                    {
                        OnCriticalError(ex, new StackTrace(true));
                    }
                    OnFailure();
                };
                var Buffer = vts.GetReadBuffer();
                var BufferLength = Buffer.Offset + Buffer.Count;
                Socket.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
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

            private void OnExit()
            {
                IsExitingValue.Update(b =>
                {
                    if (!IsRunningValue.Check(bb => bb)) { return b; }
                    if (!b)
                    {
                        Server.NotifySessionQuit(this);
                    }
                    return true;
                });
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

            private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
            private LockedVariable<Boolean> IsExitingValue = new LockedVariable<Boolean>(false);
            public Boolean IsRunning
            {
                get
                {
                    return IsRunningValue.Check(b => b);
                }
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
