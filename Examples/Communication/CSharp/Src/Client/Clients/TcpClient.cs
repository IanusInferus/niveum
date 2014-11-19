using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using BaseSystem;
using Net;

namespace Client
{
    public partial class Streamed
    {
        public sealed class TcpClient : IDisposable
        {
            public IStreamedVirtualTransportClient VirtualTransportClient { get; private set; }

            private IPEndPoint RemoteEndPoint;
            private StreamedAsyncSocket Socket;
            //private Action<Action> QueueUserWorkItem;
            private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
            public Boolean IsRunning
            {
                get
                {
                    return IsRunningValue.Check(b => b);
                }
            }
            private Byte[] WriteBuffer;

            public TcpClient(IPEndPoint RemoteEndPoint, IStreamedVirtualTransportClient VirtualTransportClient, Action<Action> QueueUserWorkItem)
            {
                this.RemoteEndPoint = RemoteEndPoint;
                Socket = new StreamedAsyncSocket(new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp), null, QueueUserWorkItem);
                //this.QueueUserWorkItem = QueueUserWorkItem;
                this.VirtualTransportClient = VirtualTransportClient;
                VirtualTransportClient.ClientMethod += () =>
                {
                    using (var h = new AutoResetEvent(false))
                    {
                        var ByteArrays = VirtualTransportClient.TakeWriteBuffer();
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

                        Exception Exception = null;
                        Action Completed = () =>
                        {
                            h.Set();
                        };
                        Action<Exception> Faulted = ex =>
                        {
                            Exception = ex;
                            h.Set();
                        };
                        Socket.SendAsync(WriteBuffer, 0, TotalLength, Completed, Faulted);
                        h.WaitOne();
                        if (Exception != null)
                        {
                            throw new AggregateException(Exception);
                        }
                    }
                };
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
                    Exception Exception = null;
                    Action Completed = () =>
                    {
                        h.Set();
                    };
                    Action<Exception> Faulted = ex =>
                    {
                        Exception = ex;
                        h.Set();
                    };
                    Socket.ConnectAsync(RemoteEndPoint, Completed, Faulted);
                    h.WaitOne();
                    if (Exception != null)
                    {
                        throw new AggregateException(Exception);
                    }
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
                if (se == SocketError.NotConnected) { return true; }
                return false;
            }

            /// <summary>接收消息</summary>
            /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
            /// <param name="UnknownFaulted">未知错误处理函数</param>
            public void ReceiveAsync(Action<Action> DoResultHandle, Action<Exception> UnknownFaulted)
            {
                Action<Exception> Faulted = ex =>
                {
                    if (!IsRunningValue.Check(b => b) && IsSocketErrorKnown(ex)) { return; }
                    UnknownFaulted(ex);
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
                    IsRunningValue.DoAction(b =>
                    {
                        if (b)
                        {
                            Socket.ReceiveAsync(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength, Completed, Faulted);
                        }
                    });
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

                var Connected = false;
                IsRunningValue.Update(b =>
                {
                    Connected = b;
                    return false;
                });
                try
                {
                    if (Connected)
                    {
                        Socket.Shutdown(SocketShutdown.Both);
                    }
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
                            Exception Exception = null;
                            Action Completed = () =>
                            {
                                h.Set();
                            };
                            Action<Exception> Faulted = ex =>
                            {
                                Exception = ex;
                                h.Set();
                            };
                            Socket.DisconnectAsync(Completed, Faulted);
                            h.WaitOne();
                            if (Exception != null)
                            {
                                throw new AggregateException(Exception);
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
}
