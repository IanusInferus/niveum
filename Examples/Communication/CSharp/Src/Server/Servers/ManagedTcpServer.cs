using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Json;
using Server.Services;

namespace Server
{
    public enum SerializationProtocolType
    {
        Binary,
        Json
    }

    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class ManagedTcpServer
    {
        private class BindingInfo
        {
            public LockedVariable<Socket> Socket;
            public Task Task;
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
        private ConcurrentBag<Socket> AcceptedSockets = new ConcurrentBag<Socket>();
        private Task AcceptingTask;
        private CancellationTokenSource AcceptingTaskTokenSource;
        private AutoResetEvent AcceptingTaskNotifier;
        private Task PurifieringTask;
        private CancellationTokenSource PurifieringTaskTokenSource;
        private AutoResetEvent PurifieringTaskNotifier;
        private LockedVariable<HashSet<ManagedTcpSession>> Sessions = new LockedVariable<HashSet<ManagedTcpSession>>(new HashSet<ManagedTcpSession>());
        private LockedVariable<Dictionary<IPAddress, int>> IpSessions = new LockedVariable<Dictionary<IPAddress, int>>(new Dictionary<IPAddress, int>());
        private ConcurrentBag<ManagedTcpSession> StoppingSessions = new ConcurrentBag<ManagedTcpSession>();

        private class WorkPart
        {
            public ServerImplementation si;
            public IVirtualTransportServer<SessionContext> vts;
        }
        private ThreadLocal<WorkPart> WorkPartInstance;
        public ServerImplementation ApplicationServer { get { return WorkPartInstance.Value.si; } }
        public IVirtualTransportServer<SessionContext> VirtualTransportServer { get { return WorkPartInstance.Value.vts; } }
        public ServerContext ServerContext { get; private set; }

        private SerializationProtocolType ProtocolTypeValue = SerializationProtocolType.Binary;
        public delegate Boolean CheckCommandAllowedDelegate(SessionContext c, String CommandName);
        private CheckCommandAllowedDelegate CheckCommandAllowedValue = null;
        private int MaxBadCommandsValue = 8;
        private Boolean ClientDebugValue = false;
        private Boolean EnableLogNormalInValue = true;
        private Boolean EnableLogNormalOutValue = true;
        private Boolean EnableLogUnknownErrorValue = true;
        private Boolean EnableLogCriticalErrorValue = true;
        private Boolean EnableLogPerformanceValue = true;
        private Boolean EnableLogSystemValue = true;
        private IPEndPoint[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public SerializationProtocolType ProtocolType
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
        public Boolean ClientDebug
        {
            get
            {
                return ClientDebugValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                ClientDebugValue = value;
            }
        }

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableLogNormalIn
        {
            get
            {
                return EnableLogNormalInValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                EnableLogNormalInValue = value;
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableLogNormalOut
        {
            get
            {
                return EnableLogNormalOutValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                EnableLogNormalOutValue = value;
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableLogUnknownError
        {
            get
            {
                return EnableLogUnknownErrorValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                EnableLogUnknownErrorValue = value;
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableLogCriticalError
        {
            get
            {
                return EnableLogCriticalErrorValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                EnableLogCriticalErrorValue = value;
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableLogPerformance
        {
            get
            {
                return EnableLogPerformanceValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                EnableLogPerformanceValue = value;
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableLogSystem
        {
            get
            {
                return EnableLogSystemValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                EnableLogSystemValue = value;
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

        public LockedVariable<Dictionary<SessionContext, ManagedTcpSession>> SessionMappings = new LockedVariable<Dictionary<SessionContext, ManagedTcpSession>>(new Dictionary<SessionContext, ManagedTcpSession>());

        public ManagedTcpServer(ServerContext sc)
        {
            ServerContext = sc;

            WorkPartInstance = new ThreadLocal<WorkPart>
            (
                () =>
                {
                    var si = new ServerImplementation(ServerContext);
                    var law = new JsonLogAspectWrapper<SessionContext>(si);
                    law.ClientCommandIn += (c, CommandName, Parameters) =>
                    {
                        if (EnableLogNormalIn)
                        {
                            var CommandLine = String.Format(@"{0} {1}", CommandName, Parameters);
                            RaiseSessionLog(new SessionLogEntry { Token = c.SessionTokenString, RemoteEndPoint = c.RemoteEndPoint, Time = DateTime.UtcNow, Type = "In", Message = CommandLine });
                        }
                    };
                    law.ClientCommandOut += (c, CommandName, Parameters) =>
                    {
                        if (EnableLogNormalOut)
                        {
                            var CommandLine = String.Format(@"svr {0} {1}", CommandName, Parameters);
                            RaiseSessionLog(new SessionLogEntry { Token = c.SessionTokenString, RemoteEndPoint = c.RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
                        }
                    };
                    law.ServerCommand += (c, CommandName, Parameters) =>
                    {
                        if (EnableLogNormalOut)
                        {
                            var CommandLine = String.Format(@"svr {0} {1}", CommandName, Parameters);
                            RaiseSessionLog(new SessionLogEntry { Token = c.SessionTokenString, RemoteEndPoint = c.RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
                        }
                    };

                    IVirtualTransportServer<SessionContext> vts;
                    if (ProtocolType == SerializationProtocolType.Binary)
                    {
                        BinaryCountPacketServer<SessionContext>.CheckCommandAllowedDelegate cca = (c, CommandName) =>
                        {
                            if (this.CheckCommandAllowed == null) { return true; }
                            return this.CheckCommandAllowed(c, CommandName);
                        };
                        vts = new BinaryCountPacketServer<SessionContext>(law, c => c.BinaryCountPacketContext, cca);
                    }
                    else if (ProtocolType == SerializationProtocolType.Json)
                    {
                        JsonLinePacketServer<SessionContext>.CheckCommandAllowedDelegate cca = (c, CommandName) =>
                        {
                            if (this.CheckCommandAllowed == null) { return true; }
                            return this.CheckCommandAllowed(c, CommandName);
                        };
                        vts = new JsonLinePacketServer<SessionContext>(law, c => c.JsonLinePacketContext, cca);
                    }
                    else
                    {
                        throw new InvalidOperationException("InvalidSerializationProtocol: " + ProtocolType.ToString());
                    }

                    vts.ServerEvent += OnServerEvent;
                    return new WorkPart { si = si, vts = vts };
                }
            );

            this.MaxConnectionsExceeded += OnMaxConnectionsExceeded;
            this.MaxConnectionsPerIPExceeded += OnMaxConnectionsPerIPExceeded;
        }

        public void RaiseError(SessionContext c, String CommandName, String Message)
        {
            WorkPartInstance.Value.si.RaiseError(c, CommandName, Message);
        }

        private void OnServerEvent(SessionContext c, Byte[] Bytes)
        {
            ManagedTcpSession Session = null;
            SessionMappings.DoAction(Mappings =>
            {
                if (Mappings.ContainsKey(c))
                {
                    Session = Mappings[c];
                }
            });
            if (Session != null)
            {
                Session.WriteCommand(Bytes);
            }
        }

        public event Action<SessionLogEntry> SessionLog;
        public void RaiseSessionLog(SessionLogEntry Entry)
        {
            if (SessionLog != null)
            {
                SessionLog(Entry);
            }
        }

        private void OnMaxConnectionsExceeded(ManagedTcpSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections, please try again later.");
            }
        }
        private void OnMaxConnectionsPerIPExceeded(ManagedTcpSession s)
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
                            var Socket = new Socket(Binding.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

                            try
                            {
                                Socket.Bind(Binding);
                            }
                            catch (SocketException ex)
                            {
                                Exceptions.Add(ex);
                                continue;
                            }
                            Socket.Listen(MaxConnectionsValue.HasValue ? (MaxConnectionsValue.Value + 1) : 128);

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
                                                var a = BindingInfo.Socket.Check(s => s).Accept();
                                                AcceptedSockets.Add(a);
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
                                                            OriginalSocket.Close();
                                                        }
                                                        catch (Exception)
                                                        {
                                                        }
                                                        try
                                                        {
                                                            OriginalSocket.Dispose();
                                                        }
                                                        catch (Exception)
                                                        {
                                                        }
                                                        var NewSocket = new Socket(Binding.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                                                        NewSocket.Bind(Binding);
                                                        NewSocket.Listen(MaxConnectionsValue.HasValue ? (MaxConnectionsValue.Value + 1) : 128);
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

                        Func<ManagedTcpSession, Boolean> Purify = StoppingSession =>
                        {
                            var Removed = false;
                            Sessions.DoAction
                            (
                                ss =>
                                {
                                    if (ss.Contains(StoppingSession))
                                    {
                                        ss.Remove(StoppingSession);
                                        Removed = true;
                                    }
                                }
                            );
                            if (Removed)
                            {
                                var IpAddress = StoppingSession.RemoteEndPoint.Address;
                                IpSessions.DoAction
                                (
                                    iss =>
                                    {
                                        if (iss.ContainsKey(IpAddress))
                                        {
                                            iss[IpAddress] -= 1;
                                            if (iss[IpAddress] == 0)
                                            {
                                                iss.Remove(IpAddress);
                                            }
                                        }
                                    }
                                );
                            }
                            StoppingSession.Dispose();
                            return Removed;
                        };

                        Func<Boolean> PurifyOneInSession = () =>
                        {
                            ManagedTcpSession StoppingSession;
                            while (StoppingSessions.TryTake(out StoppingSession))
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
                                        Socket a;
                                        if (!AcceptedSockets.TryTake(out a))
                                        {
                                            break;
                                        }

                                        if (SessionIdleTimeoutValue.HasValue)
                                        {
                                            a.ReceiveTimeout = SessionIdleTimeoutValue.Value * 1000;
                                        }
                                        var s = new ManagedTcpSession(new StreamedAsyncSocket(a), (IPEndPoint)(a.RemoteEndPoint)) { Server = this };

                                        if (MaxConnectionsValue.HasValue && (Sessions.Check(ss => ss.Count) >= MaxConnectionsValue.Value))
                                        {
                                            PurifyOneInSession();
                                        }
                                        if (MaxConnectionsValue.HasValue && (Sessions.Check(ss => ss.Count) >= MaxConnectionsValue.Value))
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

                                        IPEndPoint e = (IPEndPoint)(a.RemoteEndPoint);
                                        if (MaxConnectionsPerIPValue.HasValue && (IpSessions.Check(iss => iss.ContainsKey(e.Address) ? iss[e.Address] : 0) >= MaxConnectionsPerIPValue.Value))
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
                                                s.Dispose();
                                            }
                                            continue;
                                        }

                                        Sessions.DoAction
                                        (
                                            ss =>
                                            {
                                                ss.Add(s);
                                            }
                                        );
                                        IpSessions.DoAction
                                        (
                                            iss =>
                                            {
                                                if (iss.ContainsKey(e.Address))
                                                {
                                                    iss[e.Address] += 1;
                                                }
                                                else
                                                {
                                                    iss.Add(e.Address, 1);
                                                }
                                            }
                                        );

                                        s.Start();
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

                                    ManagedTcpSession StoppingSession;
                                    while (StoppingSessions.TryTake(out StoppingSession))
                                    {
                                        Purify(StoppingSession);
                                    }
                                }
                            },
                            PurifieringTaskToken,
                            TaskCreationOptions.LongRunning
                        );

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
                                    Socket.Close();
                                }
                                catch (Exception)
                                {
                                }
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
                    if (PurifieringTask != null)
                    {
                        PurifieringTaskTokenSource.Cancel();
                        PurifieringTaskNotifier.Set();
                    }
                    if (AcceptingTask != null)
                    {
                        AcceptingTask.Wait();
                        AcceptingTask.Dispose();
                        AcceptingTaskTokenSource.Dispose();
                        AcceptingTaskTokenSource = null;
                        AcceptingTask = null;
                    }
                    if (PurifieringTask != null)
                    {
                        PurifieringTask.Wait();
                        PurifieringTask.Dispose();
                        PurifieringTaskTokenSource.Dispose();
                        PurifieringTaskTokenSource = null;
                        PurifieringTask = null;
                    }

                    Sessions.DoAction
                    (
                        ss =>
                        {
                            foreach (var s in ss)
                            {
                                s.Dispose();
                            }
                            ss.Clear();
                        }
                    );
                    IpSessions.DoAction
                    (
                        iss =>
                        {
                            iss.Clear();
                        }
                    );

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

        public void NotifySessionQuit(ManagedTcpSession s)
        {
            StoppingSessions.Add(s);
            PurifieringTaskNotifier.Set();
        }

        private event Action<ManagedTcpSession> MaxConnectionsExceeded;
        private event Action<ManagedTcpSession> MaxConnectionsPerIPExceeded;

        public void Dispose()
        {
            Stop();
        }
    }
}
