using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace Communication.Net
{
    public sealed class StreamedAsyncSocket : IDisposable
    {
        public Socket InnerSocket { get; private set; }

        public StreamedAsyncSocket(Socket InnerSocket)
        {
            this.InnerSocket = InnerSocket;
        }

        public void ConnectAsync(EndPoint RemoteEndPoint, Action Completed, Action<SocketError> Faulted)
        {
            Action<SocketAsyncEventArgs> a =
                e =>
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
                };
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.Completed += (sender, e) => a(e);
            socketEventArg.RemoteEndPoint = RemoteEndPoint;
            bool willRaiseEvent = InnerSocket.ConnectAsync(socketEventArg);
            if (!willRaiseEvent)
            {
                ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
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
            Action<SocketAsyncEventArgs> a =
                e =>
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
                };
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.Completed += (sender, e) => a(e);
            bool willRaiseEvent = InnerSocket.AcceptAsync(socketEventArg);
            if (!willRaiseEvent)
            {
                ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
            }
        }

        public void DisconnectAsync(Action Completed, Action<SocketError> Faulted)
        {
            Action<SocketAsyncEventArgs> a =
                e =>
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
                };
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.Completed += (sender, e) => a(e);
            bool willRaiseEvent = InnerSocket.DisconnectAsync(socketEventArg);
            if (!willRaiseEvent)
            {
                ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
            }
        }

        public void SendAsync(Byte[] SendBuffer, int Offset, int Count, Action Completed, Action<SocketError> Faulted)
        {
            Action<SocketAsyncEventArgs> a =
                e =>
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
                };
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.Completed += (sender, e) => a(e);
            socketEventArg.SetBuffer(SendBuffer, Offset, Count);
            bool willRaiseEvent = InnerSocket.SendAsync(socketEventArg);
            if (!willRaiseEvent)
            {
                ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
            }
        }

        public void ReceiveAsync(Byte[] ReceiveBuffer, int Offset, int Count, Action<int> Completed, Action<SocketError> Faulted)
        {
            Action<SocketAsyncEventArgs> a =
                e =>
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
                };
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.Completed += (sender, e) => a(e);
            socketEventArg.SetBuffer(ReceiveBuffer, Offset, Count);
            bool willRaiseEvent = InnerSocket.ReceiveAsync(socketEventArg);
            if (!willRaiseEvent)
            {
                ThreadPool.QueueUserWorkItem(o => a(o as SocketAsyncEventArgs), socketEventArg);
            }
        }

        public void Shutdown(SocketShutdown How)
        {
            InnerSocket.Shutdown(How);
        }

        public void Close()
        {
            InnerSocket.Close();
        }

        public void Dispose()
        {
            InnerSocket.Dispose();
        }
    }
}
