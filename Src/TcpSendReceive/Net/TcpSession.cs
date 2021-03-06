﻿using System;
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

        protected virtual void StartInner()
        {
        }

        protected virtual void StopInner()
        {
        }

        public void Start()
        {
            StartInner();
        }

        public void Stop()
        {
            StopInner();
            Boolean Done = false;
            Socket.Update
            (
                s =>
                {
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
                        Done = true;
                    }
                    return null;
                }
            );
            if (Done)
            {
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
            Socket.DoAction
            (
                ss =>
                {
                    if (ss == null) { return; }
                    ss.SendAsync(Bytes, 0, Bytes.Length, Completed, Faulted);
                }
            );
        }

        protected void ReceiveAsync(Byte[] ReceiveBuffer, int Offset, int Count, Action<int> Completed, Action<SocketError> Faulted)
        {
            Socket.DoAction
            (
                ss =>
                {
                    if (ss == null) { return; }
                    ss.ReceiveAsync(ReceiveBuffer, Offset, Count, Completed, Faulted);
                }
            );
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
