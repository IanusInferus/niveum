using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using BaseSystem;

namespace Net
{
    public sealed class StreamedAsyncSocket : IDisposable
    {
        private AutoResetEvent NumAsyncOperationUpdated = new AutoResetEvent(false);
        private LockedVariable<HashSet<SocketAsyncOperation>> AsyncOperationLock = new LockedVariable<HashSet<SocketAsyncOperation>>(new HashSet<SocketAsyncOperation>());
        private Socket InnerSocket;
        private int? TimeoutSeconds;
        private LockedVariable<Boolean> IsDisposed = new LockedVariable<Boolean>(false);
        public event Action TimedOut;

        private class AsyncOperationContext : IDisposable
        {
            public SocketAsyncEventArgs EventArgs;
            public Func<SocketAsyncEventArgs, Action> ResultToCompleted;
            public Action<Exception> Faulted;
            public Action ReleaseAsyncOperation;

            public AsyncOperationContext()
            {
                EventArgs = new SocketAsyncEventArgs();
                EventArgs.Completed += EventArgs_Completed;
            }

            public void DoOnCompletion()
            {
                Exception Exception = null;
                Action Completed = null;
                try
                {
                    if (EventArgs.SocketError == SocketError.Success)
                    {
                        if (Debugger.IsAttached)
                        {
                            if (ResultToCompleted != null)
                            {
                                Completed = ResultToCompleted(EventArgs);
                            }
                        }
                        else
                        {
                            try
                            {
                                if (ResultToCompleted != null)
                                {
                                    Completed = ResultToCompleted(EventArgs);
                                }
                            }
                            catch (Exception ex)
                            {
                                Exception = ex;
                            }
                        }
                    }
                    else
                    {
                        Exception = new SocketException((int)(EventArgs.SocketError));
                    }
                }
                finally
                {
                    ReleaseAsyncOperation();
                }
                if (Exception != null)
                {
                    Faulted(Exception);
                    return;
                }
                if (Completed != null)
                {
                    Completed();
                }
            }

            private void EventArgs_Completed(Object sender, SocketAsyncEventArgs e)
            {
                DoOnCompletion();
            }

            public void Dispose()
            {
                if (EventArgs != null)
                {
                    EventArgs.Completed -= EventArgs_Completed;
                    EventArgs.Dispose();
                    EventArgs = null;
                }
            }
        }

        public StreamedAsyncSocket(Socket InnerSocket, int? TimeoutSeconds)
        {
            this.InnerSocket = InnerSocket;
            this.TimeoutSeconds = TimeoutSeconds;
        }

        private void LockAsyncOperation(SocketAsyncOperation OperationIdentifier)
        {
            var Success = false;
            while (!Success)
            {
                AsyncOperationLock.DoAction(h =>
                {
                    Success = h.Add(OperationIdentifier);
                });
                Thread.SpinWait(10);
            }
            NumAsyncOperationUpdated.Set();
        }
        private void ReleaseAsyncOperation(SocketAsyncOperation OperationIdentifier)
        {
            AsyncOperationLock.DoAction(h =>
            {
                if (!h.Remove(OperationIdentifier))
                {
                    throw new InvalidOperationException();
                }
                NumAsyncOperationUpdated.Set();
            });
        }

        private void DoAsync(SocketAsyncOperation OperationIdentifier, Func<AsyncOperationContext> GetContext, Func<SocketAsyncEventArgs, Boolean> Operation, Func<SocketAsyncEventArgs, Action> ResultToCompleted, Action<Exception> Faulted)
        {
            if (IsDisposed.Check(b => b))
            {
                Faulted(new SocketException((int)(SocketError.Shutdown)));
                return;
            }
            LockAsyncOperation(OperationIdentifier);
            var Success = false;
            AsyncOperationContext Context;
            try
            {
                Context = GetContext();
                Context.ResultToCompleted = ResultToCompleted;
                Context.Faulted = Faulted;
                if (TimeoutSeconds.HasValue)
                {
                    var IsCompleted = new LockedVariable<Boolean>(false);
                    var Timer = new Timer(o => { if (!IsCompleted.Check(b => b)) { if (TimedOut != null) { TimedOut(); } } }, null, TimeoutSeconds.Value * 1000, Timeout.Infinite);
                    Context.ReleaseAsyncOperation = () =>
                    {
                        IsCompleted.Update(b => true);
                        if (Timer != null)
                        {
                            Timer.Dispose();
                            Timer = null;
                        }
                        ReleaseAsyncOperation(OperationIdentifier);
                    };
                }
                else
                {
                    Context.ReleaseAsyncOperation = () => ReleaseAsyncOperation(OperationIdentifier);
                }
                Success = true;
            }
            finally
            {
                if (!Success) { ReleaseAsyncOperation(OperationIdentifier); }
            }
            Success = false;
            Exception Exception = null;
            try
            {
                bool willRaiseEvent = Operation(Context.EventArgs);
                if (!willRaiseEvent)
                {
                    ThreadPool.QueueUserWorkItem(o => Context.DoOnCompletion());
                }
                Success = true;
            }
            catch (ObjectDisposedException)
            {
                Exception = new SocketException((int)(SocketError.OperationAborted));
            }
            finally
            {
                if (!Success) { Context.ReleaseAsyncOperation(); }
            }
            if (Exception != null)
            {
                Faulted(Exception);
            }
        }

        private AsyncOperationContext ConnectContext;
        public void ConnectAsync(EndPoint RemoteEndPoint, Action Completed, Action<Exception> Faulted)
        {
            Func<AsyncOperationContext> GetContext = () =>
            {
                if (ConnectContext == null)
                {
                    ConnectContext = new AsyncOperationContext();
                }
                return ConnectContext;
            };
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                socketEventArg.RemoteEndPoint = RemoteEndPoint;
                return InnerSocket.ConnectAsync(socketEventArg);
            };
            DoAsync(SocketAsyncOperation.Connect, GetContext, Operation, e => Completed, Faulted);
        }

        public void Bind(EndPoint LocalEndPoint)
        {
            InnerSocket.Bind(LocalEndPoint);
        }

        public void Listen(int Backlog)
        {
            InnerSocket.Listen(Backlog);
        }

        private AsyncOperationContext AcceptContext;
        public void AcceptAsync(Action<StreamedAsyncSocket> Completed, Action<Exception> Faulted)
        {
            Func<AsyncOperationContext> GetContext = () =>
            {
                if (AcceptContext == null)
                {
                    AcceptContext = new AsyncOperationContext();
                }
                return AcceptContext;
            };
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                return InnerSocket.AcceptAsync(socketEventArg);
            };
            Func<SocketAsyncEventArgs, Action> ResultToCompleted = e =>
            {
                var a = e.AcceptSocket;
                if (Completed == null) { return null; }
                return () => Completed(new StreamedAsyncSocket(a, TimeoutSeconds));
            };
            DoAsync(SocketAsyncOperation.Accept, GetContext, Operation, ResultToCompleted, Faulted);
        }

        private AsyncOperationContext DisconnectContext;
        public void DisconnectAsync(Action Completed, Action<Exception> Faulted)
        {
            Func<AsyncOperationContext> GetContext = () =>
            {
                if (DisconnectContext == null)
                {
                    DisconnectContext = new AsyncOperationContext();
                }
                return DisconnectContext;
            };
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                return InnerSocket.DisconnectAsync(socketEventArg);
            };
            DoAsync(SocketAsyncOperation.Disconnect, GetContext, Operation, e => Completed, Faulted);
        }

        private AsyncOperationContext SendContext;
        public void SendAsync(Byte[] SendBuffer, int Offset, int Count, Action Completed, Action<Exception> Faulted)
        {
            Func<AsyncOperationContext> GetContext = () =>
            {
                if (SendContext == null)
                {
                    SendContext = new AsyncOperationContext();
                }
                return SendContext;
            };
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                socketEventArg.SetBuffer(SendBuffer, Offset, Count);
                return InnerSocket.SendAsync(socketEventArg);
            };
            DoAsync(SocketAsyncOperation.Send, GetContext, Operation, e => Completed, Faulted);
        }

        private AsyncOperationContext ReceiveContext;
        public void ReceiveAsync(Byte[] ReceiveBuffer, int Offset, int Count, Action<int> Completed, Action<Exception> Faulted)
        {
            Func<AsyncOperationContext> GetContext = () =>
            {
                if (ReceiveContext == null)
                {
                    ReceiveContext = new AsyncOperationContext();
                }
                return ReceiveContext;
            };
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                socketEventArg.SetBuffer(ReceiveBuffer, Offset, Count);
                return InnerSocket.ReceiveAsync(socketEventArg);
            };
            Func<SocketAsyncEventArgs, Action> ResultToCompleted = e =>
            {
                var c = e.BytesTransferred;
                if (Completed == null) { return null; }
                return () => Completed(c);
            };
            DoAsync(SocketAsyncOperation.Receive, GetContext, Operation, ResultToCompleted, Faulted);
        }

        public void Shutdown(SocketShutdown How)
        {
            if (IsDisposed.Check(b => b)) { return; }
            try
            {
                if (!InnerSocket.Connected)
                {
                    InnerSocket.Shutdown(How);
                }
            }
            catch (SocketException ex)
            {
                //Mono上没有考虑到多线程的情况，所以在对方先Shutdown的时候Shutdown会抛出异常。
                Console.WriteLine("****************");
                Console.WriteLine("MonoSocketShutdownByRemote");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("****************");
            }
            catch (ObjectDisposedException)
            {
                //有时候会有已经释放的问题，则捕捉异常
            }
        }

        public void Dispose()
        {
            var Disposed = false;
            IsDisposed.Update(b => { Disposed = b; return true; });
            if (Disposed) { return; }
            try
            {
                InnerSocket.Close();
            }
            finally
            {
                InnerSocket.Dispose();
            }
            while (AsyncOperationLock.Check(h => h.Count != 0))
            {
                NumAsyncOperationUpdated.WaitOne();
            }
            NumAsyncOperationUpdated.Dispose();
            if (ConnectContext != null)
            {
                ConnectContext.Dispose();
                ConnectContext = null;
            }
            if (AcceptContext != null)
            {
                AcceptContext.Dispose();
                AcceptContext = null;
            }
            if (DisconnectContext != null)
            {
                DisconnectContext.Dispose();
                DisconnectContext = null;
            }
            if (SendContext != null)
            {
                SendContext.Dispose();
                SendContext = null;
            }
            if (ReceiveContext != null)
            {
                ReceiveContext.Dispose();
                ReceiveContext = null;
            }
        }
    }
}
