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
        private Object Lockee = new Object();
        private Dictionary<SocketAsyncOperation, AsyncOperationContext> AsyncOperations = new Dictionary<SocketAsyncOperation, AsyncOperationContext>();
        private Boolean IsDisposed = false;

        private Socket InnerSocket;
        private LockedVariable<int?> TimeoutSecondsValue = new LockedVariable<int?>(null);
        public int? TimeoutSeconds
        {
            get
            {
                return TimeoutSecondsValue.Check(v => v);
            }
            set
            {
                TimeoutSecondsValue.Update(v => value);
            }
        }
        public event Action TimedOut;
        private Action<Action> QueueUserWorkItem;

        private class AsyncOperationContext : IDisposable
        {
            private Object Lockee = new Object();
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
                Func<SocketAsyncEventArgs, Action> ResultToCompleted;
                Action<Exception> Faulted;
                Action ReleaseAsyncOperation;
                lock (Lockee)
                {
                    if (this.ResultToCompleted == null) { return; }

                    ResultToCompleted = this.ResultToCompleted;
                    Faulted = this.Faulted;
                    ReleaseAsyncOperation = this.ReleaseAsyncOperation;
                    this.ResultToCompleted = null;
                    this.Faulted = null;
                    this.ReleaseAsyncOperation = null;
                }

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
                SocketAsyncEventArgs EventArgs = null;
                Action<Exception> Faulted = null;
                Action ReleaseAsyncOperation = null;
                lock (Lockee)
                {
                    if (this.EventArgs != null)
                    {
                        EventArgs = this.EventArgs;
                        this.EventArgs = null;
                    }
                    if (this.ResultToCompleted != null)
                    {
                        Faulted = this.Faulted;
                        ReleaseAsyncOperation = this.ReleaseAsyncOperation;
                        this.ResultToCompleted = null;
                        this.Faulted = null;
                        this.ReleaseAsyncOperation = null;
                    }
                }
                if (EventArgs != null)
                {
                    EventArgs.Completed -= EventArgs_Completed;
                    EventArgs.Dispose();
                }
                if (ReleaseAsyncOperation != null)
                {
                    ReleaseAsyncOperation();
                }
                if (Faulted != null)
                {
                    var Exception = new SocketException((int)(SocketError.OperationAborted));
                    Faulted(Exception);
                }
            }
        }

        public StreamedAsyncSocket(Socket InnerSocket, int? TimeoutSeconds, Action<Action> QueueUserWorkItem)
        {
            this.InnerSocket = InnerSocket;
            this.TimeoutSeconds = TimeoutSeconds;
            this.QueueUserWorkItem = QueueUserWorkItem;
        }

        private Boolean TryLockAsyncOperation(SocketAsyncOperation OperationIdentifier, AsyncOperationContext Context)
        {
            var Success = false;
            while (!Success)
            {
                lock (Lockee)
                {
                    if (IsDisposed) { return false; }
                    if (AsyncOperations.ContainsKey(OperationIdentifier))
                    {
                        Success = false;
                    }
                    else
                    {
                        AsyncOperations.Add(OperationIdentifier, Context);
                        Success = true;
                    }
                }
                Thread.SpinWait(10);
            }
            return true;
        }
        private void ReleaseAsyncOperation(SocketAsyncOperation OperationIdentifier)
        {
            lock (Lockee)
            {
                var Context = AsyncOperations[OperationIdentifier];
                if (!AsyncOperations.Remove(OperationIdentifier))
                {
                    throw new InvalidOperationException();
                }
                Context.Dispose();
            }
        }

        private void DoAsync(SocketAsyncOperation OperationIdentifier, Func<SocketAsyncEventArgs, Boolean> Operation, Func<SocketAsyncEventArgs, Action> ResultToCompleted, Action<Exception> Faulted)
        {
            var Context = new AsyncOperationContext();
            if (!TryLockAsyncOperation(OperationIdentifier, Context))
            {
                Context.Dispose();
                Faulted(new SocketException((int)(SocketError.Shutdown)));
                return;
            }
            var Success = false;
            try
            {
                Context.ResultToCompleted = ResultToCompleted;
                Context.Faulted = Faulted;
                var TimeoutSeconds = this.TimeoutSeconds;
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
                    QueueUserWorkItem(Context.DoOnCompletion);
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

        public void ConnectAsync(EndPoint RemoteEndPoint, Action Completed, Action<Exception> Faulted)
        {
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                socketEventArg.RemoteEndPoint = RemoteEndPoint;
                return InnerSocket.ConnectAsync(socketEventArg);
            };
            DoAsync(SocketAsyncOperation.Connect, Operation, e => Completed, Faulted);
        }

        public void Bind(EndPoint LocalEndPoint)
        {
            InnerSocket.Bind(LocalEndPoint);
        }

        public void Listen(int Backlog)
        {
            InnerSocket.Listen(Backlog);
        }

        public void AcceptAsync(Action<StreamedAsyncSocket> Completed, Action<Exception> Faulted)
        {
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                return InnerSocket.AcceptAsync(socketEventArg);
            };
            Func<SocketAsyncEventArgs, Action> ResultToCompleted = e =>
            {
                var a = e.AcceptSocket;
                if (Completed == null) { return null; }
                return () => Completed(new StreamedAsyncSocket(a, TimeoutSeconds, QueueUserWorkItem));
            };
            DoAsync(SocketAsyncOperation.Accept, Operation, ResultToCompleted, Faulted);
        }

        public void DisconnectAsync(Action Completed, Action<Exception> Faulted)
        {
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                return InnerSocket.DisconnectAsync(socketEventArg);
            };
            DoAsync(SocketAsyncOperation.Disconnect, Operation, e => Completed, Faulted);
        }

        public void SendAsync(Byte[] SendBuffer, int Offset, int Count, Action Completed, Action<Exception> Faulted)
        {
            Func<SocketAsyncEventArgs, Boolean> Operation = socketEventArg =>
            {
                socketEventArg.SetBuffer(SendBuffer, Offset, Count);
                return InnerSocket.SendAsync(socketEventArg);
            };
            DoAsync(SocketAsyncOperation.Send, Operation, e => Completed, Faulted);
        }

        public void ReceiveAsync(Byte[] ReceiveBuffer, int Offset, int Count, Action<int> Completed, Action<Exception> Faulted)
        {
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
            DoAsync(SocketAsyncOperation.Receive, Operation, ResultToCompleted, Faulted);
        }

        public void Shutdown(SocketShutdown How)
        {
            lock (Lockee)
            {
                if (IsDisposed) { return; }
                //Mono（3.0.4）上在对方先Shutdown的时候后，某个时候Connected会变为False，但时机不确定，所以需要判断和捕捉异常。
                try
                {
                    if (InnerSocket.Connected)
                    {
                        InnerSocket.Shutdown(How);
                    }
                }
                catch (SocketException)
                {
                }
            }
        }

        public void Dispose()
        {
            lock (Lockee)
            {
                if (IsDisposed) { return; }
                IsDisposed = true;
                try
                {
                    InnerSocket.Close();
                }
                finally
                {
                    InnerSocket.Dispose();
                }
            }
        }
    }
}
