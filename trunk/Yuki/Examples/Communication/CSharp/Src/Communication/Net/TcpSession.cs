using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Communication.BaseSystem;

namespace Communication.Net
{
    public abstract class TcpSession<TServer, TSession> : IDisposable
        where TServer : TcpServer<TServer, TSession>
        where TSession : TcpSession<TServer, TSession>, new()
    {
        public TServer Server { get; set; }
        private LockedVariable<StreamedAsyncSocket> Socket = new LockedVariable<StreamedAsyncSocket>(null);
        public IPEndPoint RemoteEndPoint { get; set; }

        public TcpSession()
        {
        }

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
            StreamedAsyncSocket s = null;
            Socket.Update
            (
                ss =>
                {
                    s = ss;
                    return null;
                }
            );

            if (s != null)
            {
                try
                {
                    s.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
                try
                {
                    s.Close();
                }
                catch
                {
                }
                try
                {
                    s.Dispose();
                }
                catch
                {
                }
                Server.NotifySessionQuit((TSession)this);
            }
        }

        public void SetSocket(Socket s)
        {
            Socket.Update
            (
                ss =>
                {
                    if (ss != null) { throw new InvalidOperationException(); }
                    return new StreamedAsyncSocket(s);
                }
            );
        }
        public Socket GetSocket()
        {
            return Socket.Check(ss => ss).Branch(ss => ss != null, ss => ss.InnerSocket, ss => null);
        }

        protected void SendAsync(Byte[] Bytes, int Offset, int Count, Action Completed, Action<SocketError> Faulted)
        {
            StreamedAsyncSocket s = null;
            Socket.DoAction
            (
                ss =>
                {
                    s = ss;
                }
            );
            if (s == null) { return; }
            s.SendAsync(Bytes, 0, Bytes.Length, Completed, Faulted);
        }

        protected void ReceiveAsync(Byte[] ReceiveBuffer, int Offset, int Count, Action<int> Completed, Action<SocketError> Faulted)
        {
            StreamedAsyncSocket s = null;
            Socket.DoAction
            (
                ss =>
                {
                    s = ss;
                }
            );
            if (s == null) { return; }
            s.ReceiveAsync(ReceiveBuffer, Offset, Count, Completed, Faulted);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
