using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Firefly;
using BaseSystem;
using Net;

namespace Client
{
    public partial class Tcp
    {
        public sealed class UdpClient : IDisposable
        {
            public ITcpVirtualTransportClient VirtualTransportClient { get; private set; }

            private IPEndPoint RemoteEndPoint;
            private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
            public Boolean IsRunning
            {
                get
                {
                    return IsRunningValue.Check(b => b);
                }
            }
            private LockedVariable<int> SessionIdValue = new LockedVariable<int>(0);
            public int SessionId
            {
                get
                {
                    return SessionIdValue.Check(v => v);
                }
                private set
                {
                    SessionIdValue.Update(v => value);
                }
            }
            private LockedVariable<Boolean> ConnectedValue = new LockedVariable<Boolean>(false);
            public Boolean Connected
            {
                get
                {
                    return ConnectedValue.Check(v => v);
                }
                private set
                {
                    ConnectedValue.Update(v => value);
                }
            }
            private Socket Socket;
            private Byte[] ReadBuffer = new Byte[MaxPacketLength];

            public const int MaxPacketLength = 1400;
            public const int ReadingWindowSize = 1024;
            public const int WritingWindowSize = 16;
            public const int IndexSpace = 65536;
            public const int InitialPacketTimeoutMilliseconds = 100;
            public const int MaxSquaredPacketResentCount = 4;
            public const int MaxLinearPacketResentCount = 10;
            private static int GetTimeoutMilliseconds(int ResentCount)
            {
                if (ResentCount <= MaxSquaredPacketResentCount) { return InitialPacketTimeoutMilliseconds * (1 << ResentCount); }
                return InitialPacketTimeoutMilliseconds * (1 << MaxSquaredPacketResentCount) * (Math.Min(ResentCount, MaxLinearPacketResentCount) - MaxSquaredPacketResentCount + 1);
            }

            private class Part
            {
                public int Index;
                public Byte[] Data;
                public DateTime ResendTime;
                public int ResentCount;
            }
            private class PartContext
            {
                private int WindowSize;
                public PartContext(int WindowSize)
                {
                    this.WindowSize = WindowSize;
                }

                public int MaxHandled = IndexSpace - 1;
                public SortedDictionary<int, Part> Parts = new SortedDictionary<int, Part>();
                public Part TryTakeFirstPart()
                {
                    if (Parts.Count == 0) { return null; }
                    var First = Parts.First();
                    if (IsSuccessor(First.Key, MaxHandled))
                    {
                        Parts.Remove(First.Key);
                        MaxHandled = First.Key;
                        return First.Value;
                    }
                    return null;
                }
                public Boolean IsEqualOrAfter(int New, int Original)
                {
                    return ((New - Original + IndexSpace) % IndexSpace) < WindowSize;
                }
                public static Boolean IsSuccessor(int New, int Original)
                {
                    return ((New - Original + IndexSpace) % IndexSpace) == 1;
                }
                public static int GetSuccessor(int Original)
                {
                    return (Original + 1) % IndexSpace;
                }
                public Boolean HasPart(int Index)
                {
                    if (IsEqualOrAfter(MaxHandled, Index))
                    {
                        return true;
                    }
                    if (Parts.ContainsKey(Index))
                    {
                        return true;
                    }
                    return false;
                }
                public Boolean TryPushPart(int Index, Byte[] Data, int Offset, int Length)
                {
                    if (((Index - MaxHandled + IndexSpace) % IndexSpace) > WindowSize)
                    {
                        return false;
                    }
                    var b = new Byte[Length];
                    Array.Copy(Data, Offset, b, 0, Length);
                    Parts.Add(Index, new Part { Index = Index, Data = b, ResendTime = DateTime.UtcNow.AddIntMilliseconds(GetTimeoutMilliseconds(0)), ResentCount = 0 });
                    return true;
                }
                public Boolean TryPushPart(int Index, Byte[] Data)
                {
                    if (((Index - MaxHandled + IndexSpace) % IndexSpace) > WindowSize)
                    {
                        return false;
                    }
                    Parts.Add(Index, new Part { Index = Index, Data = Data, ResendTime = DateTime.UtcNow.AddIntMilliseconds(GetTimeoutMilliseconds(0)), ResentCount = 0 });
                    return true;
                }

                public void Acknowledge(int Index, IEnumerable<int> Indices)
                {
                    MaxHandled = Index;
                    while (true)
                    {
                        if (Parts.Count == 0) { return; }
                        var First = Parts.First();
                        if (First.Key <= Index)
                        {
                            Parts.Remove(First.Key);
                        }
                        if (First.Key >= Index)
                        {
                            break;
                        }
                    }
                    foreach (var i in Indices)
                    {
                        if (Parts.ContainsKey(i))
                        {
                            Parts.Remove(i);
                        }
                    }
                }

                public void ForEachTimedoutPacket(DateTime Time, Action<int, Byte[]> f)
                {
                    foreach (var p in Parts)
                    {
                        if (p.Value.ResendTime <= Time)
                        {
                            f(p.Key, p.Value.Data);
                            p.Value.ResendTime = Time.AddIntMilliseconds(GetTimeoutMilliseconds(p.Value.ResentCount));
                            p.Value.ResentCount += 1;
                        }
                    }
                }
            }
            private class UdpReadContext
            {
                public PartContext Parts;
                public SortedSet<int> NotAcknowledgedIndices = new SortedSet<int>();
            }
            private class UdpWriteContext
            {
                public PartContext Parts;
                public int WritenIndex;
                public Timer Timer;
            }
            private LockedVariable<UdpReadContext> RawReadingContext = new LockedVariable<UdpReadContext>(new UdpReadContext { Parts = new PartContext(ReadingWindowSize) });
            private LockedVariable<UdpWriteContext> CookedWritingContext = new LockedVariable<UdpWriteContext>(new UdpWriteContext { Parts = new PartContext(WritingWindowSize), WritenIndex = IndexSpace - 1, Timer = null });

            public UdpClient(IPEndPoint RemoteEndPoint, ITcpVirtualTransportClient VirtualTransportClient)
            {
                this.RemoteEndPoint = RemoteEndPoint;
                this.VirtualTransportClient = VirtualTransportClient;
                this.Socket = new Socket(RemoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                //在Windows下关闭SIO_UDP_CONNRESET报告，防止接受数据出错
                //http://support.microsoft.com/kb/263823/en-us
                if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    uint IOC_IN = 0x80000000;
                    uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    this.Socket.IOControl(unchecked((int)(SIO_UDP_CONNRESET)), new byte[] { Convert.ToByte(false) }, null);
                }

                VirtualTransportClient.ClientMethod += () =>
                {
                    OnWrite(VirtualTransportClient, () => { }, () => { throw new InvalidOperationException(); });
                };
            }
            public UdpClient(IPEndPoint RemoteEndPoint, IBinarySerializationClientAdapter BinarySerializationClientAdapter)
                : this(RemoteEndPoint, new Client.Tcp.BinaryCountPacketClient(BinarySerializationClientAdapter))
            {
            }
            public UdpClient(IPEndPoint RemoteEndPoint, IJsonSerializationClientAdapter JsonSerializationClientAdapter)
                : this(RemoteEndPoint, new Client.Tcp.JsonLinePacketClient(JsonSerializationClientAdapter))
            {
            }

            private void OnWrite(ITcpVirtualTransportClient vtc, Action OnSuccess, Action OnFailure)
            {
                var ByteArrays = vtc.TakeWriteBuffer();
                var TotalLength = ByteArrays.Sum(b => b.Length);
                var WriteBuffer = new Byte[GetMinNotLessPowerOfTwo(TotalLength)];
                var Offset = 0;
                foreach (var b in ByteArrays)
                {
                    Array.Copy(b, 0, WriteBuffer, Offset, b.Length);
                    Offset += b.Length;
                }
                var RemoteEndPoint = this.RemoteEndPoint;
                var SessionId = this.SessionId;
                var Connected = this.Connected;
                var IsSecureConnectionRequired = false; //TODO
                var Indices = new List<int>();
                RawReadingContext.DoAction(c =>
                {
                    if (c.NotAcknowledgedIndices.Count == 0) { return; }
                    while (c.NotAcknowledgedIndices.Count > 0)
                    {
                        var First = c.NotAcknowledgedIndices.First();
                        if (c.Parts.IsEqualOrAfter(c.Parts.MaxHandled, First))
                        {
                            c.NotAcknowledgedIndices.Remove(First);
                        }
                        else
                        {
                            break;
                        }
                    }
                    Indices.Add(c.Parts.MaxHandled);
                    Indices.AddRange(c.NotAcknowledgedIndices);
                    c.NotAcknowledgedIndices.Clear();
                });
                if ((ByteArrays.Length == 0) && (Indices.Count == 0))
                {
                    OnSuccess();
                    return;
                }
                var Success = true;
                var Parts = new List<Byte[]>();
                CookedWritingContext.DoAction(c =>
                {
                    var Time = DateTime.UtcNow;
                    var WritingOffset = 0;
                    while (WritingOffset < TotalLength)
                    {
                        var Index = PartContext.GetSuccessor(c.WritenIndex);

                        var NumIndex = Indices.Count;
                        if (NumIndex > 0xFFFF)
                        {
                            Success = false;
                            return;
                        }

                        var IsACK = NumIndex > 0;
                        var Flag = 0;
                        if (!Connected)
                        {
                            Flag |= 4; //INI
                            IsACK = false;
                        }

                        var Length = Math.Min(12 + (IsACK ? 2 + NumIndex * 2 : 0) + TotalLength - WritingOffset, MaxPacketLength);
                        var DataLength = Length - (12 + (IsACK ? 2 + NumIndex * 2 : 0));
                        var Buffer = new Byte[Length];
                        Buffer[0] = (Byte)(SessionId & 0xFF);
                        Buffer[1] = (Byte)((SessionId >> 8) & 0xFF);
                        Buffer[2] = (Byte)((SessionId >> 16) & 0xFF);
                        Buffer[3] = (Byte)((SessionId >> 24) & 0xFF);

                        if (IsACK)
                        {
                            Flag |= 1; //ACK
                            Buffer[12] = (Byte)(NumIndex & 0xFF);
                            Buffer[13] = (Byte)((NumIndex >> 8) & 0xFF);
                            var j = 0;
                            foreach (var i in Indices)
                            {
                                Buffer[14 + j * 2] = (Byte)(i & 0xFF);
                                Buffer[14 + j * 2 + 1] = (Byte)((i >> 8) & 0xFF);
                                j += 1;
                            }
                            Indices.Clear();
                        }

                        Array.Copy(WriteBuffer, WritingOffset, Buffer, 12 + (IsACK ? 2 + NumIndex * 2 : 0), DataLength);
                        WritingOffset += DataLength;

                        var Verification = 0;
                        if (IsSecureConnectionRequired)
                        {
                            Flag |= 2; //ENC
                            //TODO
                        }
                        else
                        {
                            var CRC32 = new CRC32();
                            for (int k = 12; k < Length; k += 1)
                            {
                                CRC32.PushData(Buffer[k]);
                            }
                            Verification = CRC32.GetCRC32();
                        }

                        Buffer[4] = (Byte)(Flag & 0xFF);
                        Buffer[5] = (Byte)((Flag >> 8) & 0xFF);
                        Buffer[6] = (Byte)(Index & 0xFF);
                        Buffer[7] = (Byte)((Index >> 8) & 0xFF);
                        Buffer[8] = (Byte)(Verification & 0xFF);
                        Buffer[9] = (Byte)((Verification >> 8) & 0xFF);
                        Buffer[10] = (Byte)((Verification >> 16) & 0xFF);
                        Buffer[11] = (Byte)((Verification >> 24) & 0xFF);

                        var Part = new Part { Index = Index, ResendTime = Time.AddIntMilliseconds(GetTimeoutMilliseconds(0)), Data = Buffer, ResentCount = 0 };
                        if (!c.Parts.TryPushPart(Index, Buffer))
                        {
                            Success = false;
                            return;
                        }
                        Parts.Add(Part.Data);

                        c.WritenIndex = Index;
                    }
                    if (c.Timer == null)
                    {
                        c.Timer = new Timer(o => Check(), null, GetTimeoutMilliseconds(0), Timeout.Infinite);
                    }
                });
                foreach (var p in Parts)
                {
                    SendPacket(RemoteEndPoint, p);
                }
                if (!Success)
                {
                    OnFailure();
                }
                else
                {
                    OnSuccess();
                }
            }

            private void Check()
            {
                var IsRunning = this.IsRunning;

                var RemoteEndPoint = this.RemoteEndPoint;
                var SessionId = this.SessionId;
                var Connected = this.Connected;
                var IsSecureConnectionRequired = false; //TODO
                var Indices = new List<int>();
                RawReadingContext.DoAction(c =>
                {
                    if (c.NotAcknowledgedIndices.Count == 0) { return; }
                    while (c.NotAcknowledgedIndices.Count > 0)
                    {
                        var First = c.NotAcknowledgedIndices.First();
                        if (c.Parts.IsEqualOrAfter(c.Parts.MaxHandled, First))
                        {
                            c.NotAcknowledgedIndices.Remove(First);
                        }
                        else
                        {
                            break;
                        }
                    }
                    Indices.Add(c.Parts.MaxHandled);
                    Indices.AddRange(c.NotAcknowledgedIndices);
                });

                var Parts = new List<Byte[]>();
                CookedWritingContext.DoAction(cc =>
                {
                    if (cc.Timer == null) { return; }
                    cc.Timer.Dispose();
                    cc.Timer = null;
                    if (!IsRunning) { return; }

                    if (Indices.Count > 0)
                    {
                        var Index = Indices[0];

                        var NumIndex = Indices.Count;
                        if (NumIndex > 0xFFFF)
                        {
                            return;
                        }

                        var Flag = 8; //AUX

                        var Length = 12 + 2 + NumIndex * 2;
                        var Buffer = new Byte[Length];
                        Buffer[0] = (Byte)(SessionId & 0xFF);
                        Buffer[1] = (Byte)((SessionId >> 8) & 0xFF);
                        Buffer[2] = (Byte)((SessionId >> 16) & 0xFF);
                        Buffer[3] = (Byte)((SessionId >> 24) & 0xFF);

                        Flag |= 1; //ACK
                        Buffer[12] = (Byte)(NumIndex & 0xFF);
                        Buffer[13] = (Byte)((NumIndex >> 8) & 0xFF);
                        var j = 0;
                        foreach (var i in Indices)
                        {
                            Buffer[14 + j * 2] = (Byte)(i & 0xFF);
                            Buffer[14 + j * 2 + 1] = (Byte)((i >> 8) & 0xFF);
                            j += 1;
                        }
                        Indices.Clear();

                        var Verification = 0;
                        if (IsSecureConnectionRequired)
                        {
                            Flag |= 2; //ENC
                            //TODO
                        }
                        else
                        {
                            var CRC32 = new CRC32();
                            for (int k = 12; k < Length; k += 1)
                            {
                                CRC32.PushData(Buffer[k]);
                            }
                            Verification = CRC32.GetCRC32();
                        }

                        Buffer[4] = (Byte)(Flag & 0xFF);
                        Buffer[5] = (Byte)((Flag >> 8) & 0xFF);
                        Buffer[6] = (Byte)(Index & 0xFF);
                        Buffer[7] = (Byte)((Index >> 8) & 0xFF);
                        Buffer[8] = (Byte)(Verification & 0xFF);
                        Buffer[9] = (Byte)((Verification >> 8) & 0xFF);
                        Buffer[10] = (Byte)((Verification >> 16) & 0xFF);
                        Buffer[11] = (Byte)((Verification >> 24) & 0xFF);

                        Parts.Add(Buffer);
                    }

                    if (cc.Parts.Parts.Count == 0) { return; }
                    var t = DateTime.UtcNow;
                    cc.Parts.ForEachTimedoutPacket(t, (i, d) => Parts.Add(d));
                    var Wait = Math.Max(Convert.ToInt32((cc.Parts.Parts.Min(p => p.Value.ResendTime) - t).TotalMilliseconds), 0);
                    cc.Timer = new Timer(o => Check(), null, Wait, Timeout.Infinite);
                });

                foreach (var p in Parts)
                {
                    try
                    {
                        SendPacket(this.RemoteEndPoint, p);
                    }
                    catch
                    {
                        return;
                    }
                }
            }

            private void SendPacket(IPEndPoint RemoteEndPoint, Byte[] Data)
            {
                Socket.SendTo(Data, RemoteEndPoint);
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
                if (RemoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    Socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                }
                else
                {
                    Socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
                }
            }

            private static Boolean IsSocketErrorKnown(Exception ex)
            {
                if (ex is ObjectDisposedException) { return true; }
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

                Action<Byte[]> CompletedSocket = Buffer =>
                {
                    try
                    {
                        if (Buffer.Length < 12)
                        {
                            return;
                        }
                        var SessionId = Buffer[0] | ((Int32)(Buffer[1]) << 8) | ((Int32)(Buffer[2]) << 16) | ((Int32)(Buffer[3]) << 24);
                        var Flag = Buffer[4] | ((Int32)(Buffer[5]) << 8);
                        var Index = Buffer[6] | ((Int32)(Buffer[7]) << 8);
                        var Verification = Buffer[8] | ((Int32)(Buffer[9]) << 8) | ((Int32)(Buffer[10]) << 16) | ((Int32)(Buffer[11]) << 24);

                        //如果Flag中不包含ENC，则验证CRC32
                        if ((Flag & 2) == 0)
                        {
                            var CRC32 = new CRC32();
                            for (int k = 12; k < Buffer.Length; k += 1)
                            {
                                CRC32.PushData(Buffer[k]);
                            }
                            if (CRC32.GetCRC32() != Verification)
                            {
                                return;
                            }
                        }

                        if (true) //TODO 如果加密则只能设定一次
                        {
                            this.SessionId = SessionId;
                        }
                        var Connected = false;
                        ConnectedValue.Update(v =>
                        {
                            Connected = v;
                            return true;
                        });

                        var Offset = 12;
                        int[] Indices = null;
                        if ((Flag & 1) != 0)
                        {
                            var NumIndex = Buffer[Offset] | ((Int32)(Buffer[Offset + 1]) << 8);
                            if (NumIndex > ReadingWindowSize) //若Index数量较大，则丢弃包
                            {
                                return;
                            }
                            Offset += 2;
                            Indices = new int[NumIndex];
                            for (int k = 0; k < NumIndex; k += 1)
                            {
                                Indices[k] = Buffer[Offset + k * 2] | ((Int32)(Buffer[Offset + k * 2 + 1]) << 8);
                            }
                            Offset += NumIndex * 2;
                        }

                        var Length = Buffer.Length - Offset;

                        if ((Indices != null) && (Indices.Length > 0))
                        {
                            CookedWritingContext.DoAction(c =>
                            {
                                c.Parts.Acknowledge(Indices.First(), Indices.Skip(1));
                            });
                        }

                        var Pushed = false;
                        var Parts = new List<Part>();
                        RawReadingContext.DoAction(c =>
                        {
                            if (c.Parts.HasPart(Index))
                            {
                                Pushed = true;
                                return;
                            }
                            Pushed = c.Parts.TryPushPart(Index, Buffer, Offset, Length);
                            if (Pushed)
                            {
                                c.NotAcknowledgedIndices.Add(Index);
                                while (c.NotAcknowledgedIndices.Count > 0)
                                {
                                    var First = c.NotAcknowledgedIndices.First();
                                    if (c.Parts.IsEqualOrAfter(c.Parts.MaxHandled, First))
                                    {
                                        c.NotAcknowledgedIndices.Remove(First);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                while (true)
                                {
                                    var p = c.Parts.TryTakeFirstPart();
                                    if (p == null) { break; }
                                    Parts.Add(p);
                                }
                            }
                        });

                        foreach (var p in Parts)
                        {
                            var ReadBuffer = VirtualTransportClient.GetReadBuffer();
                            var ReadBufferLength = ReadBuffer.Offset + ReadBuffer.Count;
                            if (p.Data.Length > ReadBuffer.Array.Length - ReadBufferLength)
                            {
                                Faulted(new InvalidOperationException());
                                return;
                            }
                            Array.Copy(p.Data, 0, ReadBuffer.Array, ReadBufferLength, p.Data.Length);

                            var c = p.Data.Length;
                            while (true)
                            {
                                var r = VirtualTransportClient.Handle(c);
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
                                    c = 0;
                                }
                                else
                                {
                                    throw new InvalidOperationException();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        UnknownFaulted(ex);
                    }
                };

                Action Receive = null;
                Receive = () =>
                {
                    if (!IsRunning) { return; }

                    var ServerEndPoint = this.RemoteEndPoint;
                    EventHandler<SocketAsyncEventArgs> Completed = (sender, e) =>
                    {
                        if (e.SocketError != SocketError.Success)
                        {
                            e.Dispose();
                            Faulted(new SocketException((int)(e.SocketError)));
                            return;
                        }
                        var Count = e.BytesTransferred;
                        var Buffer = new Byte[Count];
                        Array.Copy(e.Buffer, Buffer, Count);
                        e.Dispose();
                        CompletedSocket(Buffer);
                        Buffer = null;
                        Receive();
                    };
                    var ae = new SocketAsyncEventArgs();
                    ae.RemoteEndPoint = ServerEndPoint;
                    ae.SetBuffer(ReadBuffer, 0, ReadBuffer.Length);
                    ae.Completed += Completed;
                    try
                    {
                        var willRaiseEvent = Socket.ReceiveFromAsync(ae);
                        if (!willRaiseEvent)
                        {
                            ThreadPool.QueueUserWorkItem(o => Completed(null, ae));
                        }
                    }
                    catch (Exception ex)
                    {
                        ae.Dispose();
                        Faulted(ex);
                        return;
                    }
                };

                Receive();
            }

            private Boolean IsDisposed = false;
            public void Dispose()
            {
                if (IsDisposed) { return; }
                IsDisposed = true;

                IsRunningValue.Update(b => false);
                Connected = false;
                try
                {
                    Socket.Dispose();
                }
                catch
                {
                }
                CookedWritingContext.DoAction(c =>
                {
                    if (c.Timer != null)
                    {
                        c.Timer.Dispose();
                        c.Timer = null;
                    }
                });
            }
        }
    }
}
