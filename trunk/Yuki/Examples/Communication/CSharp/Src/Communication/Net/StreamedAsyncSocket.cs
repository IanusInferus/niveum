using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Communication.BaseSystem;

namespace Communication.Net
{
    public sealed class StreamedAsyncSocket : IDisposable
    {
        private AutoResetEvent NumAsyncOperationUpdated = new AutoResetEvent(false);
        private LockedVariable<int> NumAsyncOperation = new LockedVariable<int>(0);
        private Socket InnerSocket;

        public StreamedAsyncSocket(Socket InnerSocket)
        {
            this.InnerSocket = InnerSocket;
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

        public void ConnectAsync(EndPoint RemoteEndPoint, Action Completed, Action<SocketError> Faulted)
        {
            LockAsyncOperation();
            var Success = false;
            try
            {
                Action<SocketAsyncEventArgs> a = e =>
                {
                    try
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            Completed();
                        }
                        else
                        {
                            Faulted(e.SocketError);
                        }
                        e.Dispose();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                };
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.Completed += (sender, e) => a(e);
                socketEventArg.RemoteEndPoint = RemoteEndPoint;
                bool willRaiseEvent = InnerSocket.ConnectAsync(socketEventArg);
                if (!willRaiseEvent)
                {
                    ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
                }
                Success = true;
            }
            finally
            {
                if (!Success) { ReleaseAsyncOperation(); }
            }
        }

        public void Bind(EndPoint LocalEndPoint)
        {
            InnerSocket.Bind(LocalEndPoint);
        }

        public void Listen(int Backlog)
        {
            InnerSocket.Listen(Backlog);
        }

        public void AcceptAsync(Action<StreamedAsyncSocket> Completed, Action<SocketError> Faulted)
        {
            LockAsyncOperation();
            var Success = false;
            try
            {
                Action<SocketAsyncEventArgs> a = e =>
                {
                    try
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            Completed(new StreamedAsyncSocket(e.AcceptSocket));
                        }
                        else
                        {
                            Faulted(e.SocketError);
                        }
                        e.Dispose();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                };
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.Completed += (sender, e) => a(e);
                bool willRaiseEvent = InnerSocket.AcceptAsync(socketEventArg);
                if (!willRaiseEvent)
                {
                    ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
                }
                Success = true;
            }
            finally
            {
                if (!Success) { ReleaseAsyncOperation(); }
            }
        }

        public void DisconnectAsync(Action Completed, Action<SocketError> Faulted)
        {
            LockAsyncOperation();
            var Success = false;
            try
            {
                Action<SocketAsyncEventArgs> a = e =>
                {
                    try
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            Completed();
                        }
                        else
                        {
                            Faulted(e.SocketError);
                        }
                        e.Dispose();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                };
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.Completed += (sender, e) => a(e);
                bool willRaiseEvent = InnerSocket.DisconnectAsync(socketEventArg);
                if (!willRaiseEvent)
                {
                    ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
                }
                Success = true;
            }
            finally
            {
                if (!Success) { ReleaseAsyncOperation(); }
            }
        }

        public void SendAsync(Byte[] SendBuffer, int Offset, int Count, Action Completed, Action<SocketError> Faulted)
        {
            LockAsyncOperation();
            var Success = false;
            try
            {
                Action<SocketAsyncEventArgs> a = e =>
                {
                    try
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            Completed();
                        }
                        else
                        {
                            Faulted(e.SocketError);
                        }
                        e.Dispose();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                };
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.Completed += (sender, e) => a(e);
                socketEventArg.SetBuffer(SendBuffer, Offset, Count);
                bool willRaiseEvent = InnerSocket.SendAsync(socketEventArg);
                if (!willRaiseEvent)
                {
                    ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
                }
                Success = true;
            }
            finally
            {
                if (!Success) { ReleaseAsyncOperation(); }
            }
        }

        public void ReceiveAsync(Byte[] ReceiveBuffer, int Offset, int Count, Action<int> Completed, Action<SocketError> Faulted)
        {
            LockAsyncOperation();
            var Success = false;
            try
            {
                Action<SocketAsyncEventArgs> a = e =>
                {
                    try
                    {
                        if (e.SocketError == SocketError.Success)
                        {
                            Completed(e.BytesTransferred);
                        }
                        else
                        {
                            Faulted(e.SocketError);
                        }
                        e.Dispose();
                    }
                    finally
                    {
                        ReleaseAsyncOperation();
                    }
                };
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.Completed += (sender, e) => a(e);
                socketEventArg.SetBuffer(ReceiveBuffer, Offset, Count);
                bool willRaiseEvent = InnerSocket.ReceiveAsync(socketEventArg);
                if (!willRaiseEvent)
                {
                    ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
                }
                Success = true;
            }
            finally
            {
                if (!Success) { ReleaseAsyncOperation(); }
            }
        }

        public void Shutdown(SocketShutdown How)
        {
            InnerSocket.Shutdown(How);
        }

        public void Dispose()
        {
            try
            {
                InnerSocket.Close();
            }
            finally
            {
                InnerSocket.Dispose();
            }
            while (NumAsyncOperation.Check(n => n != 0))
            {
                NumAsyncOperationUpdated.WaitOne();
            }
        }
    }
}
