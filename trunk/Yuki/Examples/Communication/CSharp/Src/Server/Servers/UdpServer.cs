using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Firefly;
using Algorithms;
using BaseSystem;

namespace Server
{
    public partial class Tcp<TServerContext>
        where TServerContext : IServerContext
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
        ///     Key = SessionKey XOR SHA1(Flag :: Index)
        ///     MAC = HMAC(Key, Inner).Take(4)
        ///     HMAC = H((K XOR opad) :: H((K XOR ipad) :: Inner))
        ///     H = SHA1
        ///     opad = 0x5C
        ///     ipad = 0x36
        /// Inner ::= RC4(Key, InnerData)，当ENC存在时
        ///        |= InnerData
        /// InnerData ::= NumIndex:UInt16 LowerIndex:UInt16 Index:UInt16{NumIndex - 1} Payload:Byte*，当ACK存在时
        ///            |= Payload:Byte*
        /// </summary>
        public class UdpServer : IServer
        {
            private class BindingInfo
            {
                public LockedVariable<Socket> Socket;
                public Task Task;
                public Byte[] ReadBuffer = new Byte[UdpSession.MaxPacketLength];
            }

            private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
            public Boolean IsRunning
            {
                get
                {
                    return IsRunningValue.Check(b => b);
                }
            }

            private Dictionary<IPEndPoint, BindingInfo> BindingInfos = new Dictionary<IPEndPoint, BindingInfo>();
            private CancellationTokenSource ListeningTaskTokenSource;

            private class AcceptingInfo
            {
                public Socket Socket;
                public Byte[] ReadBuffer;
                public IPEndPoint RemoteEndPoint;
            }

            private ConcurrentQueue<AcceptingInfo> AcceptedSockets = new ConcurrentQueue<AcceptingInfo>();
            private Task AcceptingTask;
            private CancellationTokenSource AcceptingTaskTokenSource;
            private AutoResetEvent AcceptingTaskNotifier;
            private Task PurifieringTask;
            private CancellationTokenSource PurifieringTaskTokenSource;
            private AutoResetEvent PurifieringTaskNotifier;
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
            private ConcurrentQueue<UdpSession> StoppingSessions = new ConcurrentQueue<UdpSession>();
            private LockedVariable<ServerSessionSets> SessionSets = new LockedVariable<ServerSessionSets>(new ServerSessionSets());

            public TServerContext ServerContext { get; private set; }

            public delegate Boolean CheckCommandAllowedDelegate(ISessionContext c, String CommandName);
            private CheckCommandAllowedDelegate CheckCommandAllowedValue = null;
            private int MaxBadCommandsValue = 8;
            private IPEndPoint[] BindingsValue = { };
            private int? SessionIdleTimeoutValue = null;
            private int? UnauthenticatedSessionIdleTimeoutValue = null;
            private int? MaxConnectionsValue = null;
            private int? MaxConnectionsPerIPValue = null;
            private int? MaxUnauthenticatedPerIPValue = null;

            private int TimeoutCheckPeriodValue = 30;
            private SerializationProtocolType ProtocolTypeValue = SerializationProtocolType.Binary;

            /// <summary>只能在启动前修改，以保证线程安全</summary>
            public CheckCommandAllowedDelegate CheckCommandAllowed
            {
                get
                {
                    return CheckCommandAllowedValue;
                }
                set
                {
                    if (IsRunning) { throw new InvalidOperationException(); }
                    CheckCommandAllowedValue = value;
                }
            }

            /// <summary>只能在启动前修改，以保证线程安全</summary>
            public int MaxBadCommands
            {
                get
                {
                    return MaxBadCommandsValue;
                }
                set
                {
                    if (IsRunning) { throw new InvalidOperationException(); }
                    MaxBadCommandsValue = value;
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
            /// <summary>只能在启动前修改，以保证线程安全</summary>
            public SerializationProtocolType SerializationProtocolType
            {
                get
                {
                    return ProtocolTypeValue;
                }
                set
                {
                    if (IsRunning) { throw new InvalidOperationException(); }
                    ProtocolTypeValue = value;
                }
            }

            public LockedVariable<Dictionary<ISessionContext, UdpSession>> SessionMappings = new LockedVariable<Dictionary<ISessionContext, UdpSession>>(new Dictionary<ISessionContext, UdpSession>());

            public UdpServer(TServerContext sc)
            {
                ServerContext = sc;

                this.MaxConnectionsExceeded += OnMaxConnectionsExceeded;
                this.MaxConnectionsPerIPExceeded += OnMaxConnectionsPerIPExceeded;
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
                            AcceptingTaskTokenSource = new CancellationTokenSource();
                            AcceptingTaskNotifier = new AutoResetEvent(false);
                            PurifieringTaskTokenSource = new CancellationTokenSource();
                            PurifieringTaskNotifier = new AutoResetEvent(false);

                            var ListeningTaskToken = ListeningTaskTokenSource.Token;
                            var AcceptingTaskToken = AcceptingTaskTokenSource.Token;
                            var PurifieringTaskToken = PurifieringTaskTokenSource.Token;

                            var Exceptions = new List<Exception>();
                            foreach (var Binding in BindingsValue)
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

                                var BindingInfo = new BindingInfo
                                {
                                    Socket = new LockedVariable<Socket>(Socket),
                                    Task = null
                                };
                                var Task = new Task
                                (
                                    () =>
                                    {
                                        try
                                        {
                                            while (true)
                                            {
                                                if (ListeningTaskToken.IsCancellationRequested) { return; }
                                                try
                                                {
                                                    var RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                                    var EndPoint = (EndPoint)(RemoteEndPoint);
                                                    var bs = BindingInfo.Socket.Check(s => s);
                                                    var Count = bs.ReceiveFrom(BindingInfo.ReadBuffer, ref EndPoint);
                                                    var ReadBuffer = new Byte[Count];
                                                    Array.Copy(BindingInfo.ReadBuffer, ReadBuffer, Count);
                                                    AcceptedSockets.Enqueue(new AcceptingInfo { Socket = bs, ReadBuffer = ReadBuffer, RemoteEndPoint = (IPEndPoint)(EndPoint) });
                                                    AcceptingTaskNotifier.Set();
                                                }
                                                catch (SocketException)
                                                {
                                                    if (ListeningTaskToken.IsCancellationRequested) { return; }
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
                                                            Socket = NewSocket;
                                                            return NewSocket;
                                                        }
                                                    );
                                                }
                                            }
                                        }
                                        catch (ObjectDisposedException)
                                        {
                                        }
                                    },
                                    TaskCreationOptions.LongRunning
                                );

                                BindingInfo.Task = Task;

                                BindingInfos.Add(Binding, BindingInfo);
                            }
                            if (BindingInfos.Count == 0)
                            {
                                throw new AggregateException(Exceptions);
                            }

                            Func<UdpSession, Boolean> Purify = StoppingSession =>
                            {
                                var Removed = false;
                                SessionSets.DoAction
                                (
                                    ss =>
                                    {
                                        if (ss.Sessions.Contains(StoppingSession))
                                        {
                                            ss.Sessions.Remove(StoppingSession);
                                            Removed = true;
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
                                return Removed;
                            };

                            Func<Boolean> PurifyOneInSession = () =>
                            {
                                UdpSession StoppingSession;
                                while (StoppingSessions.TryDequeue(out StoppingSession))
                                {
                                    var Removed = Purify(StoppingSession);
                                    if (Removed) { return true; }
                                }
                                return false;
                            };

                            AcceptingTask = new Task
                            (
                                () =>
                                {
                                    while (true)
                                    {
                                        if (AcceptingTaskToken.IsCancellationRequested) { return; }
                                        AcceptingTaskNotifier.WaitOne();
                                        while (true)
                                        {
                                            AcceptingInfo a;
                                            if (!AcceptedSockets.TryDequeue(out a))
                                            {
                                                break;
                                            }

                                            var e = a.RemoteEndPoint;
                                            UdpSession s = null;

                                            try
                                            {
                                                var Buffer = a.ReadBuffer;
                                                if (Buffer.Length < 12) { continue; }
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
                                                    if (CRC32.GetCRC32() != Verification) { continue; }
                                                }

                                                //如果Flag中包含INI，则初始化
                                                if ((Flag & 4) != 0)
                                                {
                                                    if ((Flag & 1) != 0) { continue; }
                                                    if ((Flag & 2) != 0) { continue; }
                                                    if ((Flag & 8) != 0) { continue; }
                                                    var Offset = 12;

                                                    s = new UdpSession(this, a.Socket, e);
                                                    SessionId = s.SessionId;

                                                    if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                                    {
                                                        PurifyOneInSession();
                                                    }
                                                    if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                                    {
                                                        try
                                                        {
                                                            s.Start();
                                                            if (MaxConnectionsExceeded != null)
                                                            {
                                                                MaxConnectionsExceeded(s);
                                                            }
                                                        }
                                                        finally
                                                        {
                                                            s.Dispose();
                                                        }
                                                        continue;
                                                    }

                                                    if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? ss.IpSessions[e.Address].Count : 0) >= MaxConnectionsPerIPValue.Value))
                                                    {
                                                        try
                                                        {
                                                            s.Start();
                                                            if (MaxConnectionsPerIPExceeded != null)
                                                            {
                                                                MaxConnectionsPerIPExceeded(s);
                                                            }
                                                        }
                                                        finally
                                                        {
                                                            StoppingSessions.Enqueue(s);
                                                            PurifieringTaskNotifier.Set();
                                                        }
                                                        continue;
                                                    }

                                                    if (MaxUnauthenticatedPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? (ss.IpSessions[e.Address].Count - ss.IpSessions[e.Address].Authenticated.Count) : 0) >= MaxUnauthenticatedPerIPValue.Value))
                                                    {
                                                        try
                                                        {
                                                            s.Start();
                                                            if (MaxConnectionsPerIPExceeded != null)
                                                            {
                                                                MaxConnectionsPerIPExceeded(s);
                                                            }
                                                        }
                                                        finally
                                                        {
                                                            StoppingSessions.Enqueue(s);
                                                            PurifieringTaskNotifier.Set();
                                                        }
                                                        continue;
                                                    }

                                                    SessionSets.DoAction
                                                    (
                                                        ss =>
                                                        {
                                                            ss.Sessions.Add(s);
                                                            if (ss.IpSessions.ContainsKey(e.Address))
                                                            {
                                                                ss.IpSessions[e.Address].Count += 1;
                                                            }
                                                            else
                                                            {
                                                                var isi = new IpSessionInfo();
                                                                isi.Count += 1;
                                                                ss.IpSessions.Add(e.Address, isi);
                                                            }
                                                            while (ss.SessionIdToSession.ContainsKey(SessionId))
                                                            {
                                                                s = new UdpSession(this, a.Socket, e);
                                                                SessionId = s.SessionId;
                                                            }
                                                            ss.SessionIdToSession.Add(SessionId, s);
                                                        }
                                                    );

                                                    s.Start();
                                                    if (!s.Push(e, Index, null, Buffer, Offset, Buffer.Length - Offset))
                                                    {
                                                        StoppingSessions.Enqueue(s);
                                                        PurifieringTaskNotifier.Set();
                                                    }
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
                                                        continue;
                                                    }

                                                    var IsEncrypted = false;
                                                    if ((Flag & 2) != 0)
                                                    {
                                                        IsEncrypted = true;
                                                        //TODO
                                                    }
                                                    if (s.IsSecureConnectionRequired != IsEncrypted)
                                                    {
                                                        continue;
                                                    }

                                                    var Offset = 12;
                                                    int[] Indices = null;
                                                    if ((Flag & 1) != 0)
                                                    {
                                                        var NumIndex = Buffer[Offset] | ((Int32)(Buffer[Offset + 1]) << 8);
                                                        if (NumIndex > UdpSession.ReadingWindowSize) { continue; } //若Index数量较大，则丢弃包
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
                                                        if (Indices == null) { continue; }
                                                        if (Indices.Length < 1) { continue; }
                                                        if (Index != Indices[0]) { continue; }
                                                        if (Offset != Buffer.Length) { continue; }
                                                        if (!s.PushAux(e, Indices))
                                                        {
                                                            StoppingSessions.Enqueue(s);
                                                            PurifieringTaskNotifier.Set();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (!s.Push(e, Index, Indices, Buffer, Offset, Buffer.Length - Offset))
                                                        {
                                                            StoppingSessions.Enqueue(s);
                                                            PurifieringTaskNotifier.Set();
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (ServerContext.EnableLogSystem)
                                                {
                                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                                }
                                                if (s != null)
                                                {
                                                    StoppingSessions.Enqueue(s);
                                                    PurifieringTaskNotifier.Set();
                                                }
                                            }
                                        }
                                    }
                                },
                                AcceptingTaskToken,
                                TaskCreationOptions.LongRunning
                            );

                            PurifieringTask = new Task
                            (
                                () =>
                                {
                                    while (true)
                                    {
                                        if (PurifieringTaskToken.IsCancellationRequested) { return; }

                                        PurifieringTaskNotifier.WaitOne();

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
                                                                StoppingSessions.Enqueue(s);
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
                                                                StoppingSessions.Enqueue(s);
                                                            }
                                                        }
                                                    }
                                                }
                                            );
                                        }

                                        UdpSession StoppingSession;
                                        while (StoppingSessions.TryDequeue(out StoppingSession))
                                        {
                                            Purify(StoppingSession);
                                        }
                                    }
                                },
                                PurifieringTaskToken,
                                TaskCreationOptions.LongRunning
                            );

                            if (UnauthenticatedSessionIdleTimeoutValue.HasValue || SessionIdleTimeoutValue.HasValue)
                            {
                                var TimePeriod = TimeSpan.FromSeconds(Math.Max(TimeoutCheckPeriodValue, 1));
                                LastActiveTimeCheckTimer = new Timer(state => { PurifieringTaskNotifier.Set(); }, null, TimePeriod, TimePeriod);
                            }

                            AcceptingTask.Start();
                            PurifieringTask.Start();

                            foreach (var BindingInfo in BindingInfos.Values)
                            {
                                BindingInfo.Task.Start();
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
                        foreach (var BindingInfo in BindingInfos.Values)
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
                        foreach (var BindingInfo in BindingInfos.Values)
                        {
                            if (BindingInfo.Task.Status != TaskStatus.Created)
                            {
                                BindingInfo.Task.Wait();
                            }
                            BindingInfo.Task.Dispose();
                        }
                        BindingInfos.Clear();
                        if (ListeningTaskTokenSource != null)
                        {
                            ListeningTaskTokenSource = null;
                        }

                        if (AcceptingTask != null)
                        {
                            AcceptingTaskTokenSource.Cancel();
                            AcceptingTaskNotifier.Set();
                        }
                        if (AcceptingTask != null)
                        {
                            AcceptingTask.Wait();
                            AcceptingTask.Dispose();
                            AcceptingTaskTokenSource.Dispose();
                            AcceptingTaskTokenSource = null;
                            AcceptingTask = null;
                        }

                        SessionSets.DoAction
                        (
                            ss =>
                            {
                                foreach (var s in ss.Sessions)
                                {
                                    s.Dispose();
                                }
                                ss.Sessions.Clear();
                                ss.IpSessions.Clear();
                            }
                        );

                        if (PurifieringTask != null)
                        {
                            PurifieringTaskTokenSource.Cancel();
                            PurifieringTaskNotifier.Set();
                        }
                        if (PurifieringTask != null)
                        {
                            PurifieringTask.Wait();
                            PurifieringTask.Dispose();
                            PurifieringTaskTokenSource.Dispose();
                            PurifieringTaskTokenSource = null;
                            PurifieringTask = null;
                        }

                        if (AcceptingTaskNotifier != null)
                        {
                            AcceptingTaskNotifier.Dispose();
                            AcceptingTaskNotifier = null;
                        }
                        if (PurifieringTaskNotifier != null)
                        {
                            PurifieringTaskNotifier.Dispose();
                            PurifieringTaskNotifier = null;
                        }

                        return false;
                    }
                );
            }

            public void NotifySessionQuit(UdpSession s)
            {
                StoppingSessions.Enqueue(s);
                PurifieringTaskNotifier.Set();
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

            private event Action<UdpSession> MaxConnectionsExceeded;
            private event Action<UdpSession> MaxConnectionsPerIPExceeded;

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
