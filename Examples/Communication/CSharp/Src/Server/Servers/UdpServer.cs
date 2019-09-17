using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using Firefly;
using Algorithms;
using BaseSystem;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// UDP数据包
    /// Packet ::= SessionId:Int32 Flag:UInt16 Index:UInt16 Verification:Int32 Inner:Byte*
    /// 所有数据均为little-endian
    /// SessionId，当INI存在时，为初始包，服务器收到包后分配SessionId，建议初始包的SessionId和Index均为0
    /// Flag，标记，1 ACK，表示Inner中包含确认收到的包索引，2 ENC，表示数据已加密，4 INI，表示初始化，8 AUX，表示从客户端发到服务器的包为没有数据的辅助确认包
    /// Index，序列号，当AUX存在时，必须为LowerIndex
    /// Verification，当ENC存在时，为Inner的MAC验证码，否则为CRC32验证码，其中HMAC的验证码的计算方式为
    ///     Key = SessionKey XOR SHA256(Flag :: Index)
    ///     MAC = HMAC(Key, SessionId :: Flag :: Index :: 0 :: Inner).Take(4)
    ///     HMAC = H((K XOR opad) :: H((K XOR ipad) :: Inner))
    ///     H = SHA256
    ///     opad = 0x5C
    ///     ipad = 0x36
    /// Inner ::= NumIndex:UInt16 LowerIndex:UInt16 Index:UInt16{NumIndex - 1} Payload:Byte*，当ACK存在时
    ///         |= Payload:Byte*
    /// </summary>
    public class UdpServer : IServer
    {
        private class BindingInfo
        {
            public IPEndPoint EndPoint;
            public LockedVariable<Socket> Socket;
            public Byte[] ReadBuffer = new Byte[UdpSession.MaxPacketLength];
            public AsyncConsumer<SocketAsyncEventArgs> ListenConsumer;
            public Action Start;
        }

        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);

        private List<BindingInfo> BindingInfos = new List<BindingInfo>();
        private CancellationTokenSource ListeningTaskTokenSource;
        private class AcceptingInfo
        {
            public Socket Socket;
            public Byte[] ReadBuffer;
            public IPEndPoint RemoteEndPoint;
        }
        private AsyncConsumer<AcceptingInfo> AcceptConsumer;
        private AsyncConsumer<UdpSession> PurifyConsumer;
        private Timer LastActiveTimeCheckTimer;

        private class IpSessionInfo
        {
            public int Count = 0;
            public HashSet<UdpSession> Authenticated = new HashSet<UdpSession>();
        }
        private class ServerSessionSets
        {
            public HashSet<UdpSession> Sessions = new HashSet<UdpSession>();
            public Dictionary<IPAddress, IpSessionInfo> IpSessions = new Dictionary<IPAddress, IpSessionInfo>();
            public Dictionary<int, UdpSession> SessionIdToSession = new Dictionary<int, UdpSession>();
        }
        private LockedVariable<ServerSessionSets> SessionSets = new LockedVariable<ServerSessionSets>(new ServerSessionSets());

        public IServerContext ServerContext { get; private set; }
        private Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory;
        private Action<Action> QueueUserWorkItem;
        private Action<Action> PurifierQueueUserWorkItem;

        private int MaxBadCommandsValue = 8;
        private IPEndPoint[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? UnauthenticatedSessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;
        private int? MaxUnauthenticatedPerIPValue = null;

        private int TimeoutCheckPeriodValue = 30;

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int MaxBadCommands
        {
            get
            {
                return MaxBadCommandsValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxBadCommandsValue = value;
                    }
                );
            }
        }

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public IPEndPoint[] Bindings
        {
            get
            {
                return BindingsValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        BindingsValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? SessionIdleTimeout
        {
            get
            {
                return SessionIdleTimeoutValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        SessionIdleTimeoutValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? UnauthenticatedSessionIdleTimeout
        {
            get
            {
                return UnauthenticatedSessionIdleTimeoutValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        UnauthenticatedSessionIdleTimeoutValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxConnections
        {
            get
            {
                return MaxConnectionsValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxConnectionsValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxConnectionsPerIP
        {
            get
            {
                return MaxConnectionsPerIPValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxConnectionsPerIPValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxUnauthenticatedPerIP
        {
            get
            {
                return MaxUnauthenticatedPerIPValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxUnauthenticatedPerIPValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int TimeoutCheckPeriod
        {
            get
            {
                return TimeoutCheckPeriodValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        TimeoutCheckPeriodValue = value;
                    }
                );
            }
        }

        public LockedVariable<Dictionary<ISessionContext, UdpSession>> SessionMappings = new LockedVariable<Dictionary<ISessionContext, UdpSession>>(new Dictionary<ISessionContext, UdpSession>());

        public UdpServer(IServerContext sc, Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory, Action<Action> QueueUserWorkItem, Action<Action> PurifierQueueUserWorkItem)
        {
            ServerContext = sc;
            this.VirtualTransportServerFactory = VirtualTransportServerFactory;
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.PurifierQueueUserWorkItem = PurifierQueueUserWorkItem;
        }

        private void OnMaxConnectionsExceeded(UdpSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections, please try again later.");
            }
        }
        private void OnMaxConnectionsPerIPExceeded(UdpSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections from your IP(" + s.RemoteEndPoint.Address + "), please try again later.");
            }
        }

        public void Start()
        {
            var Success = false;

            try
            {
                IsRunningValue.Update
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }

                        if (BindingsValue.Length == 0)
                        {
                            throw new Exception("NoValidBinding");
                        }

                        ListeningTaskTokenSource = new CancellationTokenSource();

                        var ListeningTaskToken = ListeningTaskTokenSource.Token;

                        Action<UdpSession> Purify = StoppingSession =>
                        {
                            SessionSets.DoAction
                            (
                                ss =>
                                {
                                    if (ss.Sessions.Contains(StoppingSession))
                                    {
                                        ss.Sessions.Remove(StoppingSession);
                                        var IpAddress = StoppingSession.RemoteEndPoint.Address;
                                        var isi = ss.IpSessions[IpAddress];
                                        if (isi.Authenticated.Contains(StoppingSession))
                                        {
                                            isi.Authenticated.Remove(StoppingSession);
                                        }
                                        isi.Count -= 1;
                                        if (isi.Count == 0)
                                        {
                                            ss.IpSessions.Remove(IpAddress);
                                        }
                                        var SessionId = StoppingSession.SessionId;
                                        ss.SessionIdToSession.Remove(SessionId);
                                    }
                                }
                            );
                            StoppingSession.Dispose();
                        };

                        Action<AcceptingInfo> Accept = a =>
                        {
                            var ep = a.RemoteEndPoint;
                            UdpSession s = null;

                            try
                            {
                                var Buffer = a.ReadBuffer;
                                if (Buffer.Length < 12) { return; }
                                var SessionId = Buffer[0] | ((Int32)(Buffer[1]) << 8) | ((Int32)(Buffer[2]) << 16) | ((Int32)(Buffer[3]) << 24);
                                var Flag = Buffer[4] | ((Int32)(Buffer[5]) << 8);
                                var Index = Buffer[6] | ((Int32)(Buffer[7]) << 8);
                                var Verification = Buffer[8] | ((Int32)(Buffer[9]) << 8) | ((Int32)(Buffer[10]) << 16) | ((Int32)(Buffer[11]) << 24);
                                Buffer[8] = 0;
                                Buffer[9] = 0;
                                Buffer[10] = 0;
                                Buffer[11] = 0;
                                if (ServerContext.EnableLogTransport)
                                {
                                    //按Flag中是否包含AUX分别生成日志
                                    if ((Flag & 8) != 0)
                                    {
                                        ServerContext.RaiseSessionLog(new SessionLogEntry { Token = SessionId.ToString("X8"), RemoteEndPoint = ep, Time = DateTime.UtcNow, Type = "UdpTransport", Name = "ReceiveAux", Message = "AckIndex: " + Index.ToInvariantString() + " Length: " + Buffer.Length.ToInvariantString() });
                                    }
                                    else
                                    {
                                        ServerContext.RaiseSessionLog(new SessionLogEntry { Token = SessionId.ToString("X8"), RemoteEndPoint = ep, Time = DateTime.UtcNow, Type = "UdpTransport", Name = "Receive", Message = "Index: " + Index.ToInvariantString() + " Length: " + Buffer.Length.ToInvariantString() });
                                    }
                                }

                                //如果Flag中不包含ENC，则验证CRC32
                                if ((Flag & 2) == 0)
                                {
                                    if (Cryptography.CRC32(Buffer) != Verification)
                                    {
                                        if (ServerContext.EnableLogTransport)
                                        {
                                            ServerContext.RaiseSessionLog(new SessionLogEntry { Token = SessionId.ToString("X8"), RemoteEndPoint = ep, Time = DateTime.UtcNow, Type = "UdpTransport", Name = "Receive", Message = "Index: " + Index.ToInvariantString() + " CRC32Failed" });
                                        }
                                        return;
                                    }
                                }

                                //如果Flag中包含INI，则初始化
                                if ((Flag & 4) != 0)
                                {
                                    if ((Flag & 1) != 0) { return; }
                                    if ((Flag & 2) != 0) { return; }
                                    if ((Flag & 8) != 0) { return; }
                                    var Offset = 12;

                                    s = new UdpSession(this, a.Socket, ep, VirtualTransportServerFactory, QueueUserWorkItem);
                                    SessionId = s.SessionId;

                                    if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                    {
                                        PurifyConsumer.DoOne();
                                    }
                                    if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                    {
                                        try
                                        {
                                            s.Start();
                                            OnMaxConnectionsExceeded(s);
                                        }
                                        finally
                                        {
                                            s.Dispose();
                                        }
                                        return;
                                    }

                                    if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(ep.Address) ? ss.IpSessions[ep.Address].Count : 0) >= MaxConnectionsPerIPValue.Value))
                                    {
                                        try
                                        {
                                            s.Start();
                                            OnMaxConnectionsPerIPExceeded(s);
                                        }
                                        finally
                                        {
                                            PurifyConsumer.Push(s);
                                        }
                                        return;
                                    }

                                    if (MaxUnauthenticatedPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(ep.Address) ? (ss.IpSessions[ep.Address].Count - ss.IpSessions[ep.Address].Authenticated.Count) : 0) >= MaxUnauthenticatedPerIPValue.Value))
                                    {
                                        try
                                        {
                                            s.Start();
                                            OnMaxConnectionsPerIPExceeded(s);
                                        }
                                        finally
                                        {
                                            PurifyConsumer.Push(s);
                                        }
                                        return;
                                    }

                                    SessionSets.DoAction
                                    (
                                        ss =>
                                        {
                                            ss.Sessions.Add(s);
                                            if (ss.IpSessions.ContainsKey(ep.Address))
                                            {
                                                ss.IpSessions[ep.Address].Count += 1;
                                            }
                                            else
                                            {
                                                var isi = new IpSessionInfo();
                                                isi.Count += 1;
                                                ss.IpSessions.Add(ep.Address, isi);
                                            }
                                            while ((SessionId == 0) || ss.SessionIdToSession.ContainsKey(SessionId))
                                            {
                                                s = new UdpSession(this, a.Socket, ep, VirtualTransportServerFactory, QueueUserWorkItem);
                                                SessionId = s.SessionId;
                                            }
                                            ss.SessionIdToSession.Add(SessionId, s);
                                        }
                                    );

                                    s.Start();
                                    s.PrePush(() =>
                                    {
                                        if (!s.Push(ep, Index, null, Buffer, Offset, Buffer.Length - Offset))
                                        {
                                            PurifyConsumer.Push(s);
                                        }
                                    });
                                }
                                else
                                {
                                    var Close = false;
                                    SessionSets.DoAction
                                    (
                                        ss =>
                                        {
                                            if (!ss.SessionIdToSession.ContainsKey(SessionId))
                                            {
                                                Close = true;
                                                return;
                                            }
                                            s = ss.SessionIdToSession[SessionId];
                                        }
                                    );
                                    if (Close)
                                    {
                                        return;
                                    }

                                    s.PrePush(() =>
                                    {
                                        var IsEncrypted = (Flag & 2) != 0;
                                        var NextSecureContext = s.NextSecureContext;
                                        var SecureContext = s.SecureContext;
                                        if ((SecureContext == null) && (NextSecureContext != null))
                                        {
                                            s.SecureContext = NextSecureContext;
                                            s.NextSecureContext = null;
                                            SecureContext = NextSecureContext;
                                            NextSecureContext = null;
                                        }
                                        if ((SecureContext != null) != IsEncrypted)
                                        {
                                            return;
                                        }
                                        if (IsEncrypted)
                                        {
                                            var Key = SecureContext.ClientToken.Concat(Cryptography.SHA256(Buffer.Skip(4).Take(4)));
                                            var HMACBytes = Cryptography.HMACSHA256Simple(Key, Buffer).Take(4).ToArray();
                                            var HMAC = HMACBytes[0] | ((Int32)(HMACBytes[1]) << 8) | ((Int32)(HMACBytes[2]) << 16) | ((Int32)(HMACBytes[3]) << 24);
                                            if (HMAC != Verification)
                                            {
                                                if (ServerContext.EnableLogTransport)
                                                {
                                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = SessionId.ToString("X8"), RemoteEndPoint = ep, Time = DateTime.UtcNow, Type = "UdpTransport", Name = "Receive", Message = "Index: " + Index.ToInvariantString() + " HMACFailed" });
                                                }
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
                                            if (NumIndex > UdpSession.WritingWindowSize) //若Index数量较大，则丢弃包
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

                                        //如果Flag中包含AUX，则判断
                                        if ((Flag & 8) != 0)
                                        {
                                            if (Indices == null) { return; }
                                            if (Indices.Length < 1) { return; }
                                            if (Index != Indices[0]) { return; }
                                            if (Offset != Buffer.Length) { return; }
                                        }

                                        var PreviousRemoteEndPoint = s.RemoteEndPoint;
                                        if (!PreviousRemoteEndPoint.Equals(ep))
                                        {
                                            SessionSets.DoAction
                                            (
                                                ss =>
                                                {
                                                    var Authenticated = false;
                                                    {
                                                        var PreviousIpAddress = PreviousRemoteEndPoint.Address;
                                                        var isi = ss.IpSessions[PreviousIpAddress];
                                                        if (isi.Authenticated.Contains(s))
                                                        {
                                                            isi.Authenticated.Remove(s);
                                                            Authenticated = true;
                                                        }
                                                        isi.Count -= 1;
                                                        if (isi.Count == 0)
                                                        {
                                                            ss.IpSessions.Remove(PreviousIpAddress);
                                                        }
                                                    }

                                                    {
                                                        IpSessionInfo isi;
                                                        if (ss.IpSessions.ContainsKey(ep.Address))
                                                        {
                                                            isi = ss.IpSessions[ep.Address];
                                                            isi.Count += 1;
                                                        }
                                                        else
                                                        {
                                                            isi = new IpSessionInfo();
                                                            isi.Count += 1;
                                                            ss.IpSessions.Add(ep.Address, isi);
                                                        }
                                                        if (Authenticated)
                                                        {
                                                            isi.Authenticated.Add(s);
                                                        }
                                                    }

                                                    s.RemoteEndPoint = ep;
                                                }
                                            );
                                        }

                                        if ((Flag & 8) != 0)
                                        {
                                            if (!s.PushAux(ep, Indices))
                                            {
                                                PurifyConsumer.Push(s);
                                            }
                                        }
                                        else
                                        {
                                            if (!s.Push(ep, Index, Indices, Buffer, Offset, Buffer.Length - Offset))
                                            {
                                                PurifyConsumer.Push(s);
                                            }
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ServerContext.EnableLogSystem)
                                {
                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = ep, Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                }
                                if (s != null)
                                {
                                    PurifyConsumer.Push(s);
                                }
                            }
                        };
                        AcceptConsumer = new AsyncConsumer<AcceptingInfo>(QueueUserWorkItem, a => { Accept(a); return true; }, int.MaxValue);

                        var Exceptions = new List<Exception>();
                        var Bindings = new List<IPEndPoint>();
                        //将所有默认地址换为实际的所有接口地址
                        foreach (var Binding in BindingsValue)
                        {
                            if (IPAddress.Equals(Binding.Address, IPAddress.Any) || IPAddress.Equals(Binding.Address, IPAddress.IPv6Any))
                            {
                                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                                {
                                    foreach (var a in ni.GetIPProperties().UnicastAddresses)
                                    {
                                        if (a.Address.AddressFamily == Binding.Address.AddressFamily)
                                        {
                                            Bindings.Add(new IPEndPoint(a.Address, Binding.Port));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Bindings.Add(Binding);
                            }
                        }
                        foreach (var Binding in Bindings)
                        {
                            Func<Socket> CreateSocket = () =>
                            {
                                var s = new Socket(Binding.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                                //在Windows下关闭SIO_UDP_CONNRESET报告，防止接受数据出错
                                //http://support.microsoft.com/kb/263823/en-us
                                if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
                                {
                                    uint IOC_IN = 0x80000000;
                                    uint IOC_VENDOR = 0x18000000;
                                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                                    s.IOControl(unchecked((int)(SIO_UDP_CONNRESET)), new byte[] { Convert.ToByte(false) }, null);
                                }
                                return s;
                            };

                            var Socket = CreateSocket();

                            try
                            {
                                Socket.Bind(Binding);
                            }
                            catch (SocketException ex)
                            {
                                Exceptions.Add(ex);
                                continue;
                            }

                            var BindingInfo = new BindingInfo();
                            BindingInfo.EndPoint = Binding;
                            BindingInfo.Socket = new LockedVariable<Socket>(Socket);
                            Func<SocketAsyncEventArgs, Boolean> Completed = args =>
                            {
                                try
                                {
                                    if (ListeningTaskToken.IsCancellationRequested) { return false; }
                                    if (args.SocketError == SocketError.Success)
                                    {
                                        var Count = args.BytesTransferred;
                                        var ReadBuffer = new Byte[Count];
                                        Array.Copy(BindingInfo.ReadBuffer, ReadBuffer, Count);
                                        var a = new AcceptingInfo { Socket = BindingInfo.Socket.Check(s => s), ReadBuffer = ReadBuffer, RemoteEndPoint = (IPEndPoint)(args.RemoteEndPoint) };
                                        AcceptConsumer.Push(a);
                                    }
                                    else
                                    {
                                        BindingInfo.Socket.Update
                                        (
                                            OriginalSocket =>
                                            {
                                                try
                                                {
                                                    OriginalSocket.Dispose();
                                                }
                                                catch (Exception)
                                                {
                                                }
                                                var NewSocket = CreateSocket();
                                                NewSocket.Bind(Binding);
                                                return NewSocket;
                                            }
                                        );
                                    }
                                }
                                finally
                                {
                                    args.Dispose();
                                }
                                BindingInfo.Start();
                                return true;
                            };
                            BindingInfo.ListenConsumer = new AsyncConsumer<SocketAsyncEventArgs>(QueueUserWorkItem, Completed, 1);
                            BindingInfo.Start = () =>
                            {
                                var EventArgs = new SocketAsyncEventArgs();
                                EventArgs.RemoteEndPoint = new IPEndPoint(Binding.Address.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                                EventArgs.SetBuffer(BindingInfo.ReadBuffer, 0, BindingInfo.ReadBuffer.Length);
                                var bs = BindingInfo.Socket.Check(s => s);
                                EventArgs.Completed += (o, args) => BindingInfo.ListenConsumer.Push(args);
                                try
                                {
                                    if (!bs.ReceiveFromAsync(EventArgs))
                                    {
                                        BindingInfo.ListenConsumer.Push(EventArgs);
                                    }
                                }
                                catch (ObjectDisposedException)
                                {
                                }
                            };

                            BindingInfos.Add(BindingInfo);
                        }
                        if (BindingInfos.Count == 0)
                        {
                            throw new AggregateException(Exceptions);
                        }

                        PurifyConsumer = new AsyncConsumer<UdpSession>(PurifierQueueUserWorkItem, s => { Purify(s); return true; }, int.MaxValue);

                        if (UnauthenticatedSessionIdleTimeoutValue.HasValue || SessionIdleTimeoutValue.HasValue)
                        {
                            var TimePeriod = TimeSpan.FromSeconds(Math.Max(TimeoutCheckPeriodValue, 1));
                            LastActiveTimeCheckTimer = new Timer(state =>
                            {
                                if (UnauthenticatedSessionIdleTimeoutValue.HasValue)
                                {
                                    var CheckTime = DateTime.UtcNow.AddIntSeconds(-UnauthenticatedSessionIdleTimeoutValue.Value);
                                    SessionSets.DoAction
                                    (
                                        ss =>
                                        {
                                            foreach (var s in ss.Sessions)
                                            {
                                                var IpAddress = s.RemoteEndPoint.Address;
                                                var isi = ss.IpSessions[IpAddress];
                                                if (!isi.Authenticated.Contains(s))
                                                {
                                                    if (s.LastActiveTime < CheckTime)
                                                    {
                                                        PurifyConsumer.Push(s);
                                                    }
                                                }
                                            }
                                        }
                                    );
                                }

                                if (SessionIdleTimeoutValue.HasValue)
                                {
                                    var CheckTime = DateTime.UtcNow.AddIntSeconds(-SessionIdleTimeoutValue.Value);
                                    SessionSets.DoAction
                                    (
                                        ss =>
                                        {
                                            foreach (var s in ss.Sessions)
                                            {
                                                var IpAddress = s.RemoteEndPoint.Address;
                                                var isi = ss.IpSessions[IpAddress];
                                                if (isi.Authenticated.Contains(s))
                                                {
                                                    if (s.LastActiveTime < CheckTime)
                                                    {
                                                        PurifyConsumer.Push(s);
                                                    }
                                                }
                                            }
                                        }
                                    );
                                }
                            }, null, TimePeriod, TimePeriod);
                        }

                        foreach (var BindingInfo in BindingInfos)
                        {
                            BindingInfo.Start();
                        }

                        Success = true;

                        return true;
                    }
                );
            }
            finally
            {
                if (!Success)
                {
                    Stop();
                }
            }
        }

        public void Stop()
        {
            IsRunningValue.Update
            (
                b =>
                {
                    if (!b) { return false; }

                    if (LastActiveTimeCheckTimer != null)
                    {
                        LastActiveTimeCheckTimer.Dispose();
                        LastActiveTimeCheckTimer = null;
                    }

                    if (ListeningTaskTokenSource != null)
                    {
                        ListeningTaskTokenSource.Cancel();
                    }
                    foreach (var BindingInfo in BindingInfos)
                    {
                        BindingInfo.Socket.DoAction
                        (
                            Socket =>
                            {
                                try
                                {
                                    Socket.Dispose();
                                }
                                catch (Exception)
                                {
                                }
                            }
                        );
                    }
                    foreach (var BindingInfo in BindingInfos)
                    {
                        BindingInfo.ListenConsumer.Dispose();
                    }
                    BindingInfos.Clear();
                    if (ListeningTaskTokenSource != null)
                    {
                        ListeningTaskTokenSource = null;
                    }

                    if (AcceptConsumer != null)
                    {
                        AcceptConsumer.Dispose();
                        AcceptConsumer = null;
                    }

                    List<UdpSession> Sessions = null;
                    SessionSets.DoAction
                    (
                        ss =>
                        {
                            Sessions = ss.Sessions.ToList();
                            ss.Sessions.Clear();
                            ss.IpSessions.Clear();
                            ss.SessionIdToSession.Clear();
                        }
                    );
                    foreach (var s in Sessions)
                    {
                        s.Dispose();
                    }

                    if (PurifyConsumer != null)
                    {
                        PurifyConsumer.Dispose();
                        PurifyConsumer = null;
                    }

                    return false;
                }
            );
        }

        public void NotifySessionQuit(UdpSession s)
        {
            PurifyConsumer.Push(s);
        }
        public void NotifySessionAuthenticated(UdpSession s)
        {
            var e = s.RemoteEndPoint;
            SessionSets.DoAction
            (
                ss =>
                {
                    if (ss.IpSessions.ContainsKey(e.Address))
                    {
                        var isi = ss.IpSessions[e.Address];
                        if (!isi.Authenticated.Contains(s))
                        {
                            isi.Authenticated.Add(s);
                        }
                    }
                }
            );
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
