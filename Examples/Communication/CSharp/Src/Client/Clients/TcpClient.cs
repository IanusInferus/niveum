using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public class TcpVirtualTransportClientHandleResultCommand
    {
        public String CommandName;
        public Action HandleResult;
    }

    public enum TcpVirtualTransportClientHandleResultTag
    {
        Continue = 0,
        Command = 1
    }
    [TaggedUnion]
    public class TcpVirtualTransportClientHandleResult
    {
        [Tag]
        public TcpVirtualTransportClientHandleResultTag _Tag;
        public Unit Continue;
        public TcpVirtualTransportClientHandleResultCommand Command;

        public static TcpVirtualTransportClientHandleResult CreateContinue() { return new TcpVirtualTransportClientHandleResult { _Tag = TcpVirtualTransportClientHandleResultTag.Continue, Continue = new Unit() }; }
        public static TcpVirtualTransportClientHandleResult CreateCommand(TcpVirtualTransportClientHandleResultCommand Value) { return new TcpVirtualTransportClientHandleResult { _Tag = TcpVirtualTransportClientHandleResultTag.Command, Command = Value }; }

        public Boolean OnContinue { get { return _Tag == TcpVirtualTransportClientHandleResultTag.Continue; } }
        public Boolean OnCommand { get { return _Tag == TcpVirtualTransportClientHandleResultTag.Command; } }
    }

    public interface ITcpVirtualTransportClient
    {
        IApplicationClient ApplicationClient { get; }

        ArraySegment<Byte> GetReadBuffer();
        Byte[] TakeWriteBuffer();
        TcpVirtualTransportClientHandleResult Handle(int Count);
        UInt64 Hash { get; }
        event Action ClientMethod;
    }

    public sealed class TcpClient : IDisposable
    {
        public IApplicationClient InnerClient { get { return VirtualTransportClient.ApplicationClient; } }
        public ITcpVirtualTransportClient VirtualTransportClient { get; private set; }

        private IPEndPoint RemoteEndPoint;
        private StreamedAsyncSocket Socket;
        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
        public Boolean IsRunning
        {
            get
            {
                return IsRunningValue.Check(b => b);
            }
        }
        private Boolean Connected = false;

        public TcpClient(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            this.RemoteEndPoint = RemoteEndPoint;
            Socket = new StreamedAsyncSocket(new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp));
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                VirtualTransportClient = new BinaryCountPacketClient();
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                VirtualTransportClient = new JsonLinePacketClient();
            }
            else
            {
                throw new InvalidOperationException("InvalidSerializationProtocol: " + ProtocolType.ToString());
            }
            InnerClient.Error += e => InnerClient.DequeueCallback(e.CommandName);
            VirtualTransportClient.ClientMethod += () =>
            {
                using (var h = new AutoResetEvent(false))
                {
                    var Bytes = VirtualTransportClient.TakeWriteBuffer();

                    var Error = Optional<SocketError>.Empty;
                    Action Completed = () =>
                    {
                        h.Set();
                    };
                    Action<SocketError> Faulted = se =>
                    {
                        Error = se;
                        h.Set();
                    };
                    Socket.SendAsync(Bytes, 0, Bytes.Length, Completed, Faulted);
                    h.WaitOne();
                    if (Error.OnHasValue)
                    {
                        throw new SocketException((int)(Error.HasValue));
                    }
                }
            };
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
            using (var h = new AutoResetEvent(false))
            {
                var Error = Optional<SocketError>.Empty;
                Action Completed = () =>
                {
                    h.Set();
                };
                Action<SocketError> Faulted = se =>
                {
                    Error = se;
                    h.Set();
                };
                Socket.ConnectAsync(RemoteEndPoint, Completed, Faulted);
                h.WaitOne();
                if (Error.OnHasValue)
                {
                    throw new SocketException((int)(Error.HasValue));
                }
                Connected = true;
            }
        }

        private static Boolean IsSocketErrorKnown(SocketError se)
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
        public void ReceiveAsync(Action<Action> DoResultHandle, Action<SocketError> UnknownFaulted)
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
                    if (r.OnContinue)
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
                Socket.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
            };

            {
                var Buffer = VirtualTransportClient.GetReadBuffer();
                var BufferLength = Buffer.Offset + Buffer.Count;
                Socket.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
            }
        }

        private Boolean IsDisposed = false;
        public void Dispose()
        {
            if (IsDisposed) { return; }
            IsDisposed = true;

            IsRunningValue.Update(b => false);
            try
            {
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }
            try
            {
                if (Connected)
                {
                    using (var h = new AutoResetEvent(false))
                    {
                        var Error = Optional<SocketError>.Empty;
                        Action Completed = () =>
                        {
                            h.Set();
                        };
                        Action<SocketError> Faulted = se =>
                        {
                            Error = se;
                            h.Set();
                        };
                        Socket.DisconnectAsync(Completed, Faulted);
                        h.WaitOne();
                        if (Error.OnHasValue)
                        {
                            throw new SocketException((int)(Error.HasValue));
                        }
                    }
                    Connected = false;
                }
            }
            catch
            {
            }
            try
            {
                Socket.Dispose();
            }
            catch
            {
            }
        }
    }
}
