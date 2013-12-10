using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BaseSystem;
using Net;

namespace Server
{
    public partial class Tcp<TServerContext>
        where TServerContext : IServerContext
    {
        public class TcpVirtualTransportServerHandleResultCommand
        {
            public String CommandName;
            public Action ExecuteCommand;
        }

        public class TcpVirtualTransportServerHandleResultBadCommand
        {
            public String CommandName;
        }

        public class TcpVirtualTransportServerHandleResultBadCommandLine
        {
            public String CommandLine;
        }

        public enum TcpVirtualTransportServerHandleResultTag
        {
            Continue = 0,
            Command = 1,
            BadCommand = 2,
            BadCommandLine = 3
        }
        [TaggedUnion]
        public class TcpVirtualTransportServerHandleResult
        {
            [Tag]
            public TcpVirtualTransportServerHandleResultTag _Tag;
            public Unit Continue;
            public TcpVirtualTransportServerHandleResultCommand Command;
            public TcpVirtualTransportServerHandleResultBadCommand BadCommand;
            public TcpVirtualTransportServerHandleResultBadCommandLine BadCommandLine;

            public static TcpVirtualTransportServerHandleResult CreateContinue() { return new TcpVirtualTransportServerHandleResult { _Tag = TcpVirtualTransportServerHandleResultTag.Continue, Continue = new Unit() }; }
            public static TcpVirtualTransportServerHandleResult CreateCommand(TcpVirtualTransportServerHandleResultCommand Value) { return new TcpVirtualTransportServerHandleResult { _Tag = TcpVirtualTransportServerHandleResultTag.Command, Command = Value }; }
            public static TcpVirtualTransportServerHandleResult CreateBadCommand(TcpVirtualTransportServerHandleResultBadCommand Value) { return new TcpVirtualTransportServerHandleResult { _Tag = TcpVirtualTransportServerHandleResultTag.BadCommand, BadCommand = Value }; }
            public static TcpVirtualTransportServerHandleResult CreateBadCommandLine(TcpVirtualTransportServerHandleResultBadCommandLine Value) { return new TcpVirtualTransportServerHandleResult { _Tag = TcpVirtualTransportServerHandleResultTag.BadCommandLine, BadCommandLine = Value }; }

            public Boolean OnContinue { get { return _Tag == TcpVirtualTransportServerHandleResultTag.Continue; } }
            public Boolean OnCommand { get { return _Tag == TcpVirtualTransportServerHandleResultTag.Command; } }
            public Boolean OnBadCommand { get { return _Tag == TcpVirtualTransportServerHandleResultTag.BadCommand; } }
            public Boolean OnBadCommandLine { get { return _Tag == TcpVirtualTransportServerHandleResultTag.BadCommandLine; } }
        }

        public interface ITcpVirtualTransportServer
        {
            ArraySegment<Byte> GetReadBuffer();
            Byte[][] TakeWriteBuffer();
            TcpVirtualTransportServerHandleResult Handle(int Count);
            UInt64 Hash { get; }
            event Action ServerEvent;
        }

        public enum SerializationProtocolType
        {
            Binary,
            Json
        }

        /// <summary>
        /// 本类的所有非继承的公共成员均是线程安全的。
        /// </summary>
        public class TcpServer : IServer
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

            private class IpSessionInfo
            {
                public int Count = 0;
                public HashSet<TcpSession> Authenticated = new HashSet<TcpSession>();
            }

            private Dictionary<IPEndPoint, BindingInfo> BindingInfos = new Dictionary<IPEndPoint, BindingInfo>();
            private CancellationTokenSource ListeningTaskTokenSource;
            private ConcurrentQueue<Socket> AcceptedSockets = new ConcurrentQueue<Socket>();
            private Task AcceptingTask;
            private CancellationTokenSource AcceptingTaskTokenSource;
            private AutoResetEvent AcceptingTaskNotifier;
            private Task PurifieringTask;
            private CancellationTokenSource PurifieringTaskTokenSource;
            private AutoResetEvent PurifieringTaskNotifier;
            private LockedVariable<HashSet<TcpSession>> Sessions = new LockedVariable<HashSet<TcpSession>>(new HashSet<TcpSession>());
            private LockedVariable<Dictionary<IPAddress, IpSessionInfo>> IpSessions = new LockedVariable<Dictionary<IPAddress, IpSessionInfo>>(new Dictionary<IPAddress, IpSessionInfo>());
            private ConcurrentQueue<TcpSession> StoppingSessions = new ConcurrentQueue<TcpSession>();

            public TServerContext ServerContext { get; private set; }

            public delegate Boolean CheckCommandAllowedDelegate(ISessionContext c, String CommandName);
            private CheckCommandAllowedDelegate CheckCommandAllowedValue = null;
            private int MaxBadCommandsValue = 8;
            private IPEndPoint[] BindingsValue = { };
            private int? SessionIdleTimeoutValue = null;
            private int? MaxConnectionsValue = null;
            private int? MaxConnectionsPerIPValue = null;
            private int? MaxUnauthenticatedPerIPValue = null;
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

            public LockedVariable<Dictionary<ISessionContext, TcpSession>> SessionMappings = new LockedVariable<Dictionary<ISessionContext, TcpSession>>(new Dictionary<ISessionContext, TcpSession>());

            public TcpServer(TServerContext sc)
            {
                ServerContext = sc;

                this.MaxConnectionsExceeded += OnMaxConnectionsExceeded;
                this.MaxConnectionsPerIPExceeded += OnMaxConnectionsPerIPExceeded;
            }

            private void OnMaxConnectionsExceeded(TcpSession s)
            {
                if (s != null && s.IsRunning)
                {
                    s.RaiseError("", "Client host rejected: too many connections, please try again later.");
                }
            }
            private void OnMaxConnectionsPerIPExceeded(TcpSession s)
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
                                var Socket = new Socket(Binding.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

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
                                                    AcceptedSockets.Enqueue(a);
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
                                                            var NewSocket = new Socket(Binding.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
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

                            Func<TcpSession, Boolean> Purify = StoppingSession =>
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
                                                var isi = iss[IpAddress];
                                                if (isi.Authenticated.Contains(StoppingSession))
                                                {
                                                    isi.Authenticated.Remove(StoppingSession);
                                                }
                                                isi.Count -= 1;
                                                if (isi.Count == 0)
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
                                TcpSession StoppingSession;
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
                                            Socket a;
                                            if (!AcceptedSockets.TryDequeue(out a))
                                            {
                                                break;
                                            }

                                            var s = new TcpSession(this, new StreamedAsyncSocket(a, SessionIdleTimeoutValue), (IPEndPoint)(a.RemoteEndPoint));

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
                                            if (MaxConnectionsPerIPValue.HasValue && (IpSessions.Check(iss => iss.ContainsKey(e.Address) ? iss[e.Address].Count : 0) >= MaxConnectionsPerIPValue.Value))
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

                                            if (MaxUnauthenticatedPerIPValue.HasValue && (IpSessions.Check(iss => iss.ContainsKey(e.Address) ? (iss[e.Address].Count - iss[e.Address].Authenticated.Count) : 0) >= MaxUnauthenticatedPerIPValue.Value))
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
                                                        iss[e.Address].Count += 1;
                                                    }
                                                    else
                                                    {
                                                        var isi = new IpSessionInfo();
                                                        isi.Count += 1;
                                                        iss.Add(e.Address, isi);
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

                                        TcpSession StoppingSession;
                                        while (StoppingSessions.TryDequeue(out StoppingSession))
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

            public void NotifySessionQuit(TcpSession s)
            {
                StoppingSessions.Enqueue(s);
                PurifieringTaskNotifier.Set();
            }
            public void NotifySessionAuthenticated(TcpSession s)
            {
                var e = s.RemoteEndPoint;
                IpSessions.DoAction
                (
                    iss =>
                    {
                        if (iss.ContainsKey(e.Address))
                        {
                            var isi = iss[e.Address];
                            if (!isi.Authenticated.Contains(s))
                            {
                                isi.Authenticated.Add(s);
                            }
                        }
                    }
                );
            }

            private event Action<TcpSession> MaxConnectionsExceeded;
            private event Action<TcpSession> MaxConnectionsPerIPExceeded;

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
