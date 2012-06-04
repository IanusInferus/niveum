using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Json;

namespace Client
{
    public sealed class JsonSocketClient : IJsonSender, IDisposable
    {
        private IClientImplementation<ClientContext> ci;
        public JsonClient<ClientContext> InnerClient { get; private set; }
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

        public JsonSocketClient(IPEndPoint RemoteEndPoint, IClientImplementation<ClientContext> ci)
        {
            this.RemoteEndPoint = RemoteEndPoint;
            Socket = new LockedVariable<StreamedAsyncSocket>(new StreamedAsyncSocket(new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)));
            this.ci = ci;
            InnerClient = new JsonClient<ClientContext>(this, ci);
            Context = new ClientContext();
            Context.DequeueCallback = InnerClient.DequeueCallback;
        }

        public void Connect()
        {
            Socket.DoAction(sock => sock.InnerSocket.Connect(RemoteEndPoint));
            IsRunningValue.Update
            (
                b =>
                {
                    if (b) { throw new InvalidOperationException(); }
                    return true;
                }
            );
        }

        public Socket GetSocket()
        {
            return Socket.Check(ss => ss).Branch(ss => ss != null, ss => ss.InnerSocket, ss => null);
        }

        void IJsonSender.Send(String CommandName, String Parameters)
        {
            var Message = "/" + CommandName + " " + Parameters + "\r\n";
            var Bytes = Encoding.UTF8.GetBytes(Message);
            Socket.DoAction(sock => sock.InnerSocket.Send(Bytes));
        }

        private Byte[] Buffer = new Byte[8 * 1024];
        private int BufferLength = 0;

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
                var FirstPosition = 0;
                var CheckPosition = BufferLength;
                BufferLength += Count;
                while (true)
                {
                    var LineFeedPosition = -1;
                    for (int i = CheckPosition; i < BufferLength; i += 1)
                    {
                        Byte b = Buffer[i];
                        if (b == '\n')
                        {
                            LineFeedPosition = i;
                            break;
                        }
                    }
                    if (LineFeedPosition >= 0)
                    {
                        var LineBytes = Buffer.Skip(FirstPosition).Take(LineFeedPosition - FirstPosition).Where(b => b != '\r').ToArray();
                        var Line = Encoding.UTF8.GetString(LineBytes, 0, LineBytes.Length);

                        var triple = Line.Split(new Char[] { ' ' }, 3);
                        if (triple.Length != 3) { throw new InvalidOperationException(); }
                        if (triple[0] != "/svr") { throw new InvalidOperationException(); }
                        DoResultHandle(() => InnerClient.HandleResult(Context, triple[1], triple[2]));

                        FirstPosition = LineFeedPosition + 1;
                        CheckPosition = FirstPosition;
                    }
                    else
                    {
                        break;
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
                Socket.DoAction
                (
                    sock =>
                    {
                        if (sock == null) { return; }
                        sock.ReceiveAsync(Buffer, BufferLength, Buffer.Length - BufferLength, Completed, Faulted);
                    }
                );
            };

            Socket.DoAction
            (
                sock =>
                {
                    if (sock == null) { return; }
                    sock.ReceiveAsync(Buffer, BufferLength, Buffer.Length - BufferLength, Completed, Faulted);
                }
            );
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
