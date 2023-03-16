using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using BaseSystem;

namespace Client
{
    public sealed class TcpClient : IDisposable
    {
        public IStreamedVirtualTransportClient VirtualTransportClient { get; private set; }

        private IPEndPoint RemoteEndPoint;
        private Socket Socket;
        private TaskFactory Factory;
        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
        public Boolean IsRunning
        {
            get
            {
                return IsRunningValue.Check(b => b);
            }
        }
        private Byte[] WriteBuffer;

        public TcpClient(IPEndPoint RemoteEndPoint, IStreamedVirtualTransportClient VirtualTransportClient, TaskFactory Factory)
        {
            this.RemoteEndPoint = RemoteEndPoint;
            Socket = new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.Factory = Factory;
            this.VirtualTransportClient = VirtualTransportClient;
            VirtualTransportClient.ClientMethod += OnError =>
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

                    var t = Socket.SendAsync(new ArraySegment<Byte>(WriteBuffer, 0, TotalLength), SocketFlags.None);
                    t.ContinueWith(tt => Factory.StartNew(() => OnError(tt.Exception)), TaskContinuationOptions.OnlyOnFaulted);
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
            Socket.Connect(RemoteEndPoint);
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

        public void ReceiveAsync(Action<Exception> UnknownFaulted)
        {
            Action<Exception> Faulted = ex =>
            {
                if (!IsRunningValue.Check(b => b) && IsSocketErrorKnown(ex)) { return; }
                UnknownFaulted(ex);
            };

            Action<int> Completed = null;
            Completed = Count =>
            {
                Action a = () =>
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
                            Factory.StartNew(r.Command.HandleResult);
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
                            var t = Socket.ReceiveAsync(new ArraySegment<byte>(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength), SocketFlags.None);
                            t.ContinueWith(tt => Factory.StartNew(() => Faulted(tt.Exception)), TaskContinuationOptions.OnlyOnFaulted);
                            t.ContinueWith(tt => Factory.StartNew(() => Completed(tt.Result)), TaskContinuationOptions.OnlyOnRanToCompletion);
                        }
                    });
                };
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    a();
                }
                else
                {
                    try
                    {
                        a();
                    }
                    catch (Exception ex)
                    {
                        Faulted(ex);
                    }
                }
            };

            {
                var Buffer = VirtualTransportClient.GetReadBuffer();
                var BufferLength = Buffer.Offset + Buffer.Count;
                var t = Socket.ReceiveAsync(new ArraySegment<byte>(Buffer.Array, BufferLength, Buffer.Array.Length - BufferLength), SocketFlags.None);
                t.ContinueWith(tt => Factory.StartNew(() => Faulted(tt.Exception)), TaskContinuationOptions.OnlyOnFaulted);
                t.ContinueWith(tt => Factory.StartNew(() => Completed(tt.Result)), TaskContinuationOptions.OnlyOnRanToCompletion);
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
            if (Connected)
            {
                try
                {
                    Socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
            }
            if (Connected)
            {
                try
                {
                    Socket.Disconnect(true);
                }
                catch
                {
                }
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
