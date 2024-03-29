﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Algorithms;
using BaseSystem;

namespace Client
{
    public sealed class UdpClient : IDisposable
    {
        public IStreamedVirtualTransportClient VirtualTransportClient { get; private set; }

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
        private enum ConnectionState
        {
            Initial,
            Connecting,
            Connected
        }
        private LockedVariable<ConnectionState> ConnectionStateValue = new LockedVariable<ConnectionState>(ConnectionState.Initial);
        public Boolean IsConnected
        {
            get
            {
                return ConnectionStateValue.Check(v => v) == ConnectionState.Connected;
            }
        }
        private LockedVariable<SecureContext> SecureContextValue = new LockedVariable<SecureContext>(null);
        public SecureContext SecureContext
        {
            get
            {
                return SecureContextValue.Check(v => v);
            }
            set
            {
                SecureContextValue.Update(v => value);
            }
        }
        private Socket Socket;
        private TaskFactory Factory;
        private Byte[] ReadBuffer = new Byte[MaxPacketLength];

        public const int MaxPacketLength = 1400;
        public const int ReadingWindowSize = 1024;
        public const int WritingWindowSize = 32;
        public const int IndexSpace = 65536;
        public const int CheckTimeout = 2000;
        public static readonly int[] TimeoutSequences = { 400, 800, 1600, 2000, 3000, 4000 };
        private static int GetTimeoutMilliseconds(int ResentCount)
        {
            if (ResentCount < TimeoutSequences.Length) { return TimeoutSequences[ResentCount]; }
            return TimeoutSequences[TimeoutSequences.Length - 1];
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
            public Dictionary<int, Part> Parts = new Dictionary<int, Part>();
            public Part TryTakeFirstPart()
            {
                if (Parts.Count == 0) { return null; }
                var Successor = GetSuccessor(MaxHandled);
                if (Parts.ContainsKey(Successor))
                {
                    var Value = Parts[Successor];
                    Parts.Remove(Successor);
                    MaxHandled = Successor;
                    return Value;
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
                if (((Index - MaxHandled + IndexSpace) % IndexSpace) >= WindowSize)
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
                if (((Index - MaxHandled + IndexSpace) % IndexSpace) >= WindowSize)
                {
                    return false;
                }
                Parts.Add(Index, new Part { Index = Index, Data = Data, ResendTime = DateTime.UtcNow.AddIntMilliseconds(GetTimeoutMilliseconds(0)), ResentCount = 0 });
                return true;
            }

            public void Acknowledge(int Index, IEnumerable<int> Indices, int MaxWritten)
            {
                // Parts (= [MaxHandled, MaxWritten]
                // Index (- [MaxHandled, MaxWritten]
                // Indices (= (Index, MaxWritten]
                // |[MaxHandled, MaxWritten]| < WindowSize
                // any i (- [0, IndexSpace - 1]

                if (MaxWritten == MaxHandled) { return; }
                if (!IsEqualOrAfter(MaxWritten, MaxHandled)) { return; }
                if ((Index < 0) || (Index >= IndexSpace)) { return; }
                if (!IsEqualOrAfter(Index, MaxHandled)) { return; }
                if (!IsEqualOrAfter(MaxWritten, Index)) { return; }
                foreach (var i in Indices)
                {
                    if ((i < 0) || (i >= IndexSpace)) { return; }
                    if (IsEqualOrAfter(Index, i)) { return; }
                    if (!IsEqualOrAfter(MaxWritten, i)) { return; }
                }

                while (MaxHandled != Index)
                {
                    var i = GetSuccessor(MaxHandled);
                    if (Parts.ContainsKey(i))
                    {
                        Parts.Remove(i);
                    }
                    MaxHandled = i;
                }
                foreach (var i in Indices)
                {
                    if (Parts.ContainsKey(i))
                    {
                        Parts.Remove(i);
                    }
                }
                while (MaxHandled != MaxWritten)
                {
                    var i = GetSuccessor(MaxHandled);
                    if (Parts.ContainsKey(i))
                    {
                        break;
                    }
                    MaxHandled = i;
                }
            }

            public void ForEachTimedoutPacket(int SessionId, DateTime Time, Action<int, Byte[]> f)
            {
                foreach (var p in Parts)
                {
                    if (p.Value.ResendTime <= Time)
                    {
                        f(p.Key, p.Value.Data);
                        p.Value.ResendTime = Time.AddIntMilliseconds(GetTimeoutMilliseconds(p.Value.ResentCount));
                        p.Value.ResentCount += 1;
                        //Debug.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + " Resend SessionId: " + SessionId.ToString("X8") + " Index: " + p.Key.ToString() + " Count: " + p.Value.ResentCount.ToString());
                    }
                }
            }
        }
        private class UdpReadContext
        {
            public PartContext Parts;
            public SortedSet<int> NotAcknowledgedIndices = new SortedSet<int>();
            public DateTime LastCheck = DateTime.UtcNow;
        }
        private class UdpWriteContext
        {
            public PartContext Parts;
            public int WritenIndex;
            public Timer Timer;
        }
        private LockedVariable<UdpReadContext> RawReadingContext = new LockedVariable<UdpReadContext>(new UdpReadContext { Parts = new PartContext(ReadingWindowSize) });
        private LockedVariable<UdpWriteContext> CookedWritingContext = new LockedVariable<UdpWriteContext>(new UdpWriteContext { Parts = new PartContext(WritingWindowSize), WritenIndex = IndexSpace - 1, Timer = null });

        public UdpClient(IPEndPoint RemoteEndPoint, IStreamedVirtualTransportClient VirtualTransportClient, TaskFactory Factory)
        {
            this.RemoteEndPoint = RemoteEndPoint;
            this.VirtualTransportClient = VirtualTransportClient;
            this.Socket = new Socket(RemoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            this.Factory = Factory;

            //在Windows下关闭SIO_UDP_CONNRESET报告，防止接受数据出错
            //http://support.microsoft.com/kb/263823/en-us
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                this.Socket.IOControl(unchecked((int)(SIO_UDP_CONNRESET)), new byte[] { Convert.ToByte(false) }, null);
            }

            VirtualTransportClient.ClientMethod += OnError =>
            {
                OnWrite(VirtualTransportClient, () => { }, se => { throw new SocketException((int)(se)); });
            };
        }

        private void OnWrite(IStreamedVirtualTransportClient vtc, Action OnSuccess, Action<SocketError> OnFailure)
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
            int SessionId = 0;
            var State = ConnectionState.Initial;
            this.ConnectionStateValue.Update(v =>
            {
                SessionId = this.SessionId;
                State = v;
                if (v == ConnectionState.Initial)
                {
                    return ConnectionState.Connecting;
                }
                return v;
            });
            if (State == ConnectionState.Connecting)
            {
                throw new InvalidOperationException();
            }
            var SecureContext = this.SecureContext;
            var Indices = new List<int>();
            RawReadingContext.DoAction(c =>
            {
                if (c.NotAcknowledgedIndices.Count == 0) { return; }
                var MaxHandled = c.Parts.MaxHandled;
                var Acknowledged = new List<int>();
                foreach (var i in c.NotAcknowledgedIndices)
                {
                    if (c.Parts.IsEqualOrAfter(MaxHandled, i))
                    {
                        Acknowledged.Add(i);
                    }
                    else if (PartContext.IsSuccessor(i, MaxHandled))
                    {
                        Acknowledged.Add(i);
                        MaxHandled = i;
                    }
                }
                foreach (var i in Acknowledged)
                {
                    c.NotAcknowledgedIndices.Remove(i);
                }
                Indices.Add(MaxHandled);
                Indices.AddRange(c.NotAcknowledgedIndices);
                c.NotAcknowledgedIndices.Clear();
            });
            if ((ByteArrays.Length == 0) && (Indices.Count == 0))
            {
                OnSuccess();
                return;
            }
            var se = SocketError.Success;
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
                        se = SocketError.NoBufferSpaceAvailable;
                        return;
                    }

                    var IsACK = NumIndex > 0;
                    var Flag = 0;
                    if (State == ConnectionState.Initial)
                    {
                        Flag |= 4; //INI
                        IsACK = false;
                    }

                    var Length = Math.Min(12 + (IsACK ? 2 + NumIndex * 2 : 0) + TotalLength - WritingOffset, MaxPacketLength);
                    var DataLength = Length - (12 + (IsACK ? 2 + NumIndex * 2 : 0));
                    if (DataLength < 0)
                    {
                        se = SocketError.NoBufferSpaceAvailable;
                        return;
                    }
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

                    if (SecureContext != null)
                    {
                        Flag |= 2; //ENC
                    }
                    Buffer[4] = (Byte)(Flag & 0xFF);
                    Buffer[5] = (Byte)((Flag >> 8) & 0xFF);
                    Buffer[6] = (Byte)(Index & 0xFF);
                    Buffer[7] = (Byte)((Index >> 8) & 0xFF);

                    var Verification = 0;
                    if (SecureContext != null)
                    {
                        var Key = SecureContext.ClientToken.Concat(Cryptography.SHA256(Buffer.Skip(4).Take(4)));
                        var HMACBytes = Cryptography.HMACSHA256Simple(Key, Buffer).Take(4).ToArray();
                        Verification = HMACBytes[0] | ((Int32)(HMACBytes[1]) << 8) | ((Int32)(HMACBytes[2]) << 16) | ((Int32)(HMACBytes[3]) << 24);
                    }
                    else
                    {
                        Verification = Cryptography.CRC32(Buffer);
                    }

                    Buffer[8] = (Byte)(Verification & 0xFF);
                    Buffer[9] = (Byte)((Verification >> 8) & 0xFF);
                    Buffer[10] = (Byte)((Verification >> 16) & 0xFF);
                    Buffer[11] = (Byte)((Verification >> 24) & 0xFF);

                    var Part = new Part { Index = Index, ResendTime = Time.AddIntMilliseconds(GetTimeoutMilliseconds(0)), Data = Buffer, ResentCount = 0 };
                    if (!c.Parts.TryPushPart(Index, Buffer))
                    {
                        se = SocketError.NoBufferSpaceAvailable;
                        return;
                    }
                    Parts.Add(Part.Data);
                    //Debug.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + " Send SessionId: " + SessionId.ToString("X8") + " Index: " + Index.ToString());

                    c.WritenIndex = Index;
                }
                if (c.Timer == null)
                {
                    c.Timer = new Timer(o => Check(), null, CheckTimeout, Timeout.Infinite);
                }
            });
            foreach (var p in Parts)
            {
                try
                {
                    SendPacket(RemoteEndPoint, p);
                }
                catch
                {
                    se = SocketError.Interrupted;
                    break;
                }
            }
            if (se != SocketError.Success)
            {
                OnFailure(se);
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
            int SessionId = 0;
            this.ConnectionStateValue.Check(v =>
            {
                SessionId = this.SessionId;
                return v;
            });
            var SecureContext = this.SecureContext;
            var Indices = new List<int>();
            RawReadingContext.DoAction(c =>
            {
                if (c.NotAcknowledgedIndices.Count == 0) { return; }
                var CurrentTime = DateTime.UtcNow;
                if ((CurrentTime - c.LastCheck).TotalMilliseconds < CheckTimeout) { return; }
                c.LastCheck = CurrentTime;
                var NotAcknowledgedIndices = new SortedSet<int>(c.NotAcknowledgedIndices);
                var MaxHandled = c.Parts.MaxHandled;
                var Acknowledged = new List<int>();
                foreach (var i in NotAcknowledgedIndices)
                {
                    if (c.Parts.IsEqualOrAfter(MaxHandled, i))
                    {
                        Acknowledged.Add(i);
                    }
                    else if (PartContext.IsSuccessor(i, MaxHandled))
                    {
                        Acknowledged.Add(i);
                        MaxHandled = i;
                    }
                }
                foreach (var i in Acknowledged)
                {
                    NotAcknowledgedIndices.Remove(i);
                }
                Indices.Add(MaxHandled);
                Indices.AddRange(NotAcknowledgedIndices);
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
                    if (Length > MaxPacketLength)
                    {
                        return;
                    }
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

                    if (SecureContext != null)
                    {
                        Flag |= 2; //ENC
                    }
                    Buffer[4] = (Byte)(Flag & 0xFF);
                    Buffer[5] = (Byte)((Flag >> 8) & 0xFF);
                    Buffer[6] = (Byte)(Index & 0xFF);
                    Buffer[7] = (Byte)((Index >> 8) & 0xFF);

                    var Verification = 0;
                    if (SecureContext != null)
                    {
                        var Key = SecureContext.ClientToken.Concat(Cryptography.SHA256(Buffer.Skip(4).Take(4)));
                        var HMACBytes = Cryptography.HMACSHA256Simple(Key, Buffer).Take(4).ToArray();
                        Verification = HMACBytes[0] | ((Int32)(HMACBytes[1]) << 8) | ((Int32)(HMACBytes[2]) << 16) | ((Int32)(HMACBytes[3]) << 24);
                    }
                    else
                    {
                        Verification = Cryptography.CRC32(Buffer);
                    }

                    Buffer[8] = (Byte)(Verification & 0xFF);
                    Buffer[9] = (Byte)((Verification >> 8) & 0xFF);
                    Buffer[10] = (Byte)((Verification >> 16) & 0xFF);
                    Buffer[11] = (Byte)((Verification >> 24) & 0xFF);

                    Parts.Add(Buffer);
                    //Debug.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + " SendAux SessionId: " + SessionId.ToString("X8") + " Index: " + Index.ToString());
                }

                if (cc.Parts.Parts.Count == 0) { return; }
                var t = DateTime.UtcNow;
                cc.Parts.ForEachTimedoutPacket(SessionId, t, (i, d) => Parts.Add(d));
                var Wait = Math.Max(Convert.ToInt32((cc.Parts.Parts.Min(p => p.Value.ResendTime) - t).TotalMilliseconds), 0);
                cc.Timer = new Timer(o => Check(), null, Wait, Timeout.Infinite);
            });

            foreach (var p in Parts)
            {
                try
                {
                    SendPacket(RemoteEndPoint, p);
                }
                catch
                {
                    break;
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

        public void ReceiveAsync(Action<Exception> UnknownFaulted)
        {
            Action<Exception> Faulted = ex =>
            {
                if (!IsRunningValue.Check(b => b) && IsSocketErrorKnown(ex)) { return; }
                UnknownFaulted(ex);
            };

            Action<Byte[]> CompletedSocket = Buffer =>
            {
                Action a = () =>
                {
                    if (Buffer.Length < 12)
                    {
                        return;
                    }
                    var SessionId = Buffer[0] | ((Int32)(Buffer[1]) << 8) | ((Int32)(Buffer[2]) << 16) | ((Int32)(Buffer[3]) << 24);
                    var Flag = Buffer[4] | ((Int32)(Buffer[5]) << 8);
                    var Index = Buffer[6] | ((Int32)(Buffer[7]) << 8);
                    var Verification = Buffer[8] | ((Int32)(Buffer[9]) << 8) | ((Int32)(Buffer[10]) << 16) | ((Int32)(Buffer[11]) << 24);
                    Buffer[8] = 0;
                    Buffer[9] = 0;
                    Buffer[10] = 0;
                    Buffer[11] = 0;
                    //Debug.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + " Receive SessionId: " + SessionId.ToString("X8") + " Index: " + Index.ToString());

                    var IsEncrypted = (Flag & 2) != 0;
                    var SecureContext = this.SecureContext;
                    if ((SecureContext != null) != IsEncrypted)
                    {
                        return;
                    }

                    if (IsEncrypted)
                    {
                        var Key = SecureContext.ServerToken.Concat(Cryptography.SHA256(Buffer.Skip(4).Take(4)));
                        var HMACBytes = Cryptography.HMACSHA256Simple(Key, Buffer).Take(4).ToArray();
                        var HMAC = HMACBytes[0] | ((Int32)(HMACBytes[1]) << 8) | ((Int32)(HMACBytes[2]) << 16) | ((Int32)(HMACBytes[3]) << 24);
                        if (HMAC != Verification) { return; }
                    }
                    else
                    {
                        //如果Flag中不包含ENC，则验证CRC32
                        if (Cryptography.CRC32(Buffer) != Verification) { return; }

                        //只有尚未连接时可以设定
                        var Close = false;
                        ConnectionStateValue.Update(v =>
                        {
                            if (v == ConnectionState.Connecting)
                            {
                                this.SessionId = SessionId;
                                return ConnectionState.Connected;
                            }
                            else
                            {
                                if (SessionId != this.SessionId)
                                {
                                    Close = true;
                                }
                                return v;
                            }
                        });
                        if (Close)
                        {
                            return;
                        }
                    }

                    var Offset = 12;
                    int[] Indices = null;
                    if ((Flag & 1) != 0)
                    {
                        if (Buffer.Length < 14)
                        {
                            return;
                        }
                        var NumIndex = Buffer[Offset] | ((Int32)(Buffer[Offset + 1]) << 8);
                        if (Buffer.Length < 14 + NumIndex * 2)
                        {
                            return;
                        }
                        if (NumIndex > WritingWindowSize) //若Index数量较大，则丢弃包
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
                            c.Parts.Acknowledge(Indices.First(), Indices.Skip(1), c.WritenIndex);
                        });
                    }

                    var Pushed = false;
                    var Parts = new List<Byte[]>();
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
                            var Acknowledged = new List<int>();
                            foreach (var i in c.NotAcknowledgedIndices)
                            {
                                if (c.Parts.IsEqualOrAfter(c.Parts.MaxHandled, i))
                                {
                                    Acknowledged.Add(i);
                                }
                            }
                            foreach (var i in Acknowledged)
                            {
                                c.NotAcknowledgedIndices.Remove(i);
                            }

                            while (true)
                            {
                                var p = c.Parts.TryTakeFirstPart();
                                if (p == null) { break; }
                                Parts.Add(p.Data);
                            }
                        }
                    });

                    foreach (var p in Parts)
                    {
                        var ReadBuffer = VirtualTransportClient.GetReadBuffer();
                        var ReadBufferLength = ReadBuffer.Offset + ReadBuffer.Count;
                        if (p.Length > ReadBuffer.Array.Length - ReadBufferLength)
                        {
                            Faulted(new InvalidOperationException());
                            return;
                        }
                        Array.Copy(p, 0, ReadBuffer.Array, ReadBufferLength, p.Length);

                        var c = p.Length;
                        while (true)
                        {
                            var r = VirtualTransportClient.Handle(c);
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
                                c = 0;
                            }
                            else
                            {
                                throw new InvalidOperationException();
                            }
                        }
                    }
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
                        UnknownFaulted(ex);
                    }
                }
            };

            Action Receive = null;
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
            Receive = () =>
            {
                if (!IsRunning) { return; }

                var ServerEndPoint = this.RemoteEndPoint;
                var ae = new SocketAsyncEventArgs();
                ae.RemoteEndPoint = ServerEndPoint;
                ae.SetBuffer(ReadBuffer, 0, ReadBuffer.Length);
                ae.Completed += Completed;
                try
                {
                    var willRaiseEvent = Socket.ReceiveFromAsync(ae);
                    if (!willRaiseEvent)
                    {
                        Factory.StartNew(() => Completed(null, ae));
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
