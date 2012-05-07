using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Communication.BaseSystem;

namespace Communication.Net
{
    public abstract class TcpServer<TServer, TSession> : IDisposable
        where TServer : TcpServer<TServer, TSession>
        where TSession : TcpSession<TServer, TSession>, new()
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
        private LockedVariable<HashSet<TSession>> Sessions = new LockedVariable<HashSet<TSession>>(new HashSet<TSession>());
        private LockedVariable<Dictionary<IPAddress, int>> IpSessions = new LockedVariable<Dictionary<IPAddress, int>>(new Dictionary<IPAddress, int>());
        private ConcurrentBag<TSession> StoppingSessions = new ConcurrentBag<TSession>();

        public TcpServer()
        {
        }

        private IPEndPoint[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;

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

                                        var s = new TSession()
                                        {
                                            Server = (TServer)this,
                                            RemoteEndPoint = (IPEndPoint)(a.RemoteEndPoint)
                                        };
                                        if (SessionIdleTimeoutValue.HasValue)
                                        {
                                            a.ReceiveTimeout = SessionIdleTimeoutValue.Value * 1000;
                                        }
                                        s.SetSocket(a);

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
                                                s.Stop();
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
                                                s.Stop();
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
                                    TSession StoppingSession;
                                    while (StoppingSessions.TryTake(out StoppingSession))
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
                                        StoppingSession.Stop();
                                        StoppingSession.Dispose();
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
                                s.Stop();
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

        public void NotifySessionQuit(TSession s)
        {
            StoppingSessions.Add(s);
            PurifieringTaskNotifier.Set();
        }

        public event Action<TSession> MaxConnectionsExceeded;
        public event Action<TSession> MaxConnectionsPerIPExceeded;

        public void Dispose()
        {
            Stop();
        }
    }
}
