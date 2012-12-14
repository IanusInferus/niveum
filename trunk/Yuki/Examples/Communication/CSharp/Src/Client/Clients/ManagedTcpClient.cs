using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Binary;

namespace Client
{
    public enum SerializationProtocolType
    {
        Binary,
        Json
    }

    public sealed class ManagedTcpClient : IDisposable
    {
        private IClientImplementation<ClientContext> ci;
        public IClient InnerClient { get { return VirtualTransportClient.GetApplicationClient; } }
        public IVirtualTransportClient VirtualTransportClient { get; private set; }
        public ClientContext Context { get; private set; }

        private IPEndPoint RemoteEndPoint;
        private LockedVariable<StreamedAsyncSocket> Socket = new LockedVariable<StreamedAsyncSocket>(null);
        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
        public Boolean IsRunning
        {
            get
            {
                return IsRunningValue.Check(b => b);
            }
        }

        public ManagedTcpClient(IPEndPoint RemoteEndPoint, IClientImplementation<ClientContext> ci, SerializationProtocolType ProtocolType)
        {
            this.RemoteEndPoint = RemoteEndPoint;
            Socket = new LockedVariable<StreamedAsyncSocket>(new StreamedAsyncSocket(new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp)));
            this.ci = ci;
            Context = new ClientContext();
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                VirtualTransportClient = new BinaryCountPacketClient<ClientContext>(Context, ci, new BinaryCountPacketClientContext());
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                VirtualTransportClient = new JsonLinePacketClient<ClientContext>(Context, ci, new JsonLinePacketClientContext());
            }
            else
            {
                throw new InvalidOperationException("InvalidSerializationProtocol: " + ProtocolType.ToString());
            }
            Context.DequeueCallback = InnerClient.DequeueCallback;
            VirtualTransportClient.ClientMethod += Bytes => Socket.DoAction(sock => sock.InnerSocket.Send(Bytes));
        }

        public void Connect()
        {
            IsRunningValue.Update
            (
                b =>
                {
                    if (b) { throw new InvalidOperationException(); }
                    return true;
                }
            );
            Socket.DoAction(sock => sock.InnerSocket.Connect(RemoteEndPoint));
        }

        public Socket GetSocket()
        {
            return Socket.Check(ss => ss).Branch(ss => ss != null, ss => ss.InnerSocket, ss => null);
        }

        private Boolean IsSocketErrorKnown(SocketError se)
        {
            if (se == SocketError.ConnectionAborted) { return true; }
            if (se == SocketError.ConnectionReset) { return true; }
            if (se == SocketError.Shutdown) { return true; }
            if (se == SocketError.OperationAborted) { return true; }
            if (se == SocketError.Interrupted) { return true; }
            if (se == SocketError.NotConnected) { return true; }
            return false;
        }

        /// <summary>接收消息</summary>
        /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
        /// <param name="UnknownFaulted">未知错误处理函数</param>
        public void Receive(Action<Action> DoResultHandle, Action<SocketError> UnknownFaulted)
        {
            Action<SocketError> Faulted = se =>
            {
                if (!IsRunningValue.Check(b => b) && IsSocketErrorKnown(se)) { return; }
                UnknownFaulted(se);
            };

            Action<int> Completed = null;
            Completed = Count =>
            {
                if (Count == 0)
                {
                    return;
                }

                while (true)
                {
                    var r = VirtualTransportClient.Handle(Count);
                    if (r.OnRead)
                    {
                        break;
                    }
                    else if (r.OnCommand)
                    {
                        DoResultHandle(r.Command.HandleResult);
                        var RemainCount = VirtualTransportClient.GetReadBuffer().Count;
                        if (RemainCount <= 0)
                        {
                            break;
                        }
                        Count = 0;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                var Buffer = VirtualTransportClient.GetReadBuffer();
                var BufferLength = Buffer.Offset + Buffer.Count;
                Socket.DoAction
                (
                    sock =>
                    {
                        if (sock == null) { return; }
                        sock.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
                    }
                );
            };

            {
                var Buffer = VirtualTransportClient.GetReadBuffer();
                var BufferLength = Buffer.Offset + Buffer.Count;
                Socket.DoAction
                (
                    sock =>
                    {
                        if (sock == null) { return; }
                        sock.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
                    }
                );
            }
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            IsRunningValue.Update(b => false);
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
                            if (s.InnerSocket.Connected)
                            {
                                s.InnerSocket.Disconnect(false);
                            }
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
                    }
                    return null;
                }
            );
        }
    }
}
