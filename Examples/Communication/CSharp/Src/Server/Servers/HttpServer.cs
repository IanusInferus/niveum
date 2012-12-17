using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Json;
using Server.Algorithms;
using Server.Services;

namespace Server
{
    public class HttpVirtualTransportServerHandleResultCommand
    {
        public String CommandName;
        public Action ExecuteCommand;
    }

    public class HttpVirtualTransportServerHandleResultBadCommand
    {
        public String CommandName;
    }

    public class HttpVirtualTransportServerHandleResultBadCommandLine
    {
        public String CommandLine;
    }

    public enum HttpVirtualTransportServerHandleResultTag
    {
        Command = 1,
        BadCommand = 2,
        BadCommandLine = 3
    }
    [TaggedUnion]
    public class HttpVirtualTransportServerHandleResult
    {
        [Tag]
        public HttpVirtualTransportServerHandleResultTag _Tag;
        public HttpVirtualTransportServerHandleResultCommand Command;
        public HttpVirtualTransportServerHandleResultBadCommand BadCommand;
        public HttpVirtualTransportServerHandleResultBadCommandLine BadCommandLine;

        public static HttpVirtualTransportServerHandleResult CreateCommand(HttpVirtualTransportServerHandleResultCommand Value) { return new HttpVirtualTransportServerHandleResult { _Tag = HttpVirtualTransportServerHandleResultTag.Command, Command = Value }; }
        public static HttpVirtualTransportServerHandleResult CreateBadCommand(HttpVirtualTransportServerHandleResultBadCommand Value) { return new HttpVirtualTransportServerHandleResult { _Tag = HttpVirtualTransportServerHandleResultTag.BadCommand, BadCommand = Value }; }
        public static HttpVirtualTransportServerHandleResult CreateBadCommandLine(HttpVirtualTransportServerHandleResultBadCommandLine Value) { return new HttpVirtualTransportServerHandleResult { _Tag = HttpVirtualTransportServerHandleResultTag.BadCommandLine, BadCommandLine = Value }; }

        public Boolean OnCommand { get { return _Tag == HttpVirtualTransportServerHandleResultTag.Command; } }
        public Boolean OnBadCommand { get { return _Tag == HttpVirtualTransportServerHandleResultTag.BadCommand; } }
        public Boolean OnBadCommandLine { get { return _Tag == HttpVirtualTransportServerHandleResultTag.BadCommandLine; } }
    }

    public interface IHttpVirtualTransportServer
    {
        JObject[] TakeWriteBuffer();
        HttpVirtualTransportServerHandleResult Handle(String CommandString);
        UInt64 Hash { get; }
        event Action ServerEvent;
    }

    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class HttpServer : IServer
    {
        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
        public Boolean IsRunning
        {
            get
            {
                return IsRunningValue.Check(b => b);
            }
        }

        private HttpListener Listener = new HttpListener();
        private Task ListeningTask;
        private CancellationTokenSource ListeningTaskTokenSource;
        private ConcurrentBag<HttpListenerContext> AcceptedSockets = new ConcurrentBag<HttpListenerContext>();
        private Task AcceptingTask;
        private CancellationTokenSource AcceptingTaskTokenSource;
        private AutoResetEvent AcceptingTaskNotifier;
        private Task PurifieringTask;
        private CancellationTokenSource PurifieringTaskTokenSource;
        private AutoResetEvent PurifieringTaskNotifier;

        private class ServerSessionSets
        {
            public HashSet<HttpSession> Sessions = new HashSet<HttpSession>();
            public Dictionary<IPAddress, int> IpSessions = new Dictionary<IPAddress, int>();
            public Dictionary<String, HttpSession> SessionIdToSession = new Dictionary<String, HttpSession>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<HttpSession, String> SessionToId = new Dictionary<HttpSession, String>();
        }
        private ConcurrentBag<HttpSession> StoppingSessions = new ConcurrentBag<HttpSession>();
        private ConcurrentBag<HttpListenerContext> StoppingSockets = new ConcurrentBag<HttpListenerContext>();
        private LockedVariable<ServerSessionSets> SessionSets = new LockedVariable<ServerSessionSets>(new ServerSessionSets());

        public ServerContext ServerContext { get; private set; }

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
        private String[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;

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
        public String[] Bindings
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

        public LockedVariable<Dictionary<SessionContext, HttpSession>> SessionMappings = new LockedVariable<Dictionary<SessionContext, HttpSession>>(new Dictionary<SessionContext, HttpSession>());

        public HttpServer(ServerContext sc)
        {
            ServerContext = sc;
        }

        public event Action<SessionLogEntry> SessionLog;
        public void RaiseSessionLog(SessionLogEntry Entry)
        {
            if (SessionLog != null)
            {
                SessionLog(Entry);
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

                        foreach (var Binding in BindingsValue)
                        {
                            Listener.Prefixes.Add(Binding);
                        }
                        if (SessionIdleTimeoutValue.HasValue)
                        {
                            var tm = Listener.TimeoutManager;
                            var ts = TimeSpan.FromSeconds(SessionIdleTimeoutValue.Value);
                            tm.DrainEntityBody = ts;
                            tm.EntityBody = ts;
                            tm.HeaderWait = ts;
                            tm.IdleConnection = ts;
                            tm.RequestQueue = ts;
                        }
                        ListeningTask = new Task
                        (
                            () =>
                            {
                                Listener.Start();
                                try
                                {
                                    while (true)
                                    {
                                        if (ListeningTaskToken.IsCancellationRequested) { return; }
                                        var ca = Listener.GetContextAsync();
                                        try
                                        {
                                            ca.Wait(ListeningTaskToken);
                                            var a = ca.Result;
                                            AcceptedSockets.Add(a);
                                            AcceptingTaskNotifier.Set();
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            return;
                                        }
                                    }
                                }
                                finally
                                {
                                    Listener.Stop();
                                }
                            },
                            TaskCreationOptions.LongRunning
                        );

                        Func<HttpSession, Boolean> Purify = StoppingSession =>
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
                                        ss.IpSessions[IpAddress] -= 1;
                                        if (ss.IpSessions[IpAddress] == 0)
                                        {
                                            ss.IpSessions.Remove(IpAddress);
                                        }
                                        var SessionId = ss.SessionToId[StoppingSession];
                                        ss.SessionIdToSession.Remove(SessionId);
                                        ss.SessionToId.Remove(StoppingSession);
                                    }
                                }
                            );
                            StoppingSession.Dispose();
                            return Removed;
                        };

                        Func<Boolean> PurifyOneInSession = () =>
                        {
                            HttpSession StoppingSession;
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
                                        HttpListenerContext a;
                                        if (!AcceptedSockets.TryTake(out a))
                                        {
                                            break;
                                        }

                                        if (a.Request.ContentLength64 < 0)
                                        {
                                            a.Response.StatusCode = 411;
                                            StoppingSockets.Add(a);
                                            continue;
                                        }

                                        if (a.Request.ContentLength64 > 8 * 1024)
                                        {
                                            a.Response.StatusCode = 413;
                                            StoppingSockets.Add(a);
                                            continue;
                                        }

                                        var Keys = a.Request.QueryString.AllKeys.Where(k => k.Equals("sessionid", StringComparison.OrdinalIgnoreCase)).ToArray();
                                        if (Keys.Count() > 1)
                                        {
                                            a.Response.StatusCode = 400;
                                            StoppingSockets.Add(a);
                                            continue;
                                        }

                                        var e = (IPEndPoint)a.Request.RemoteEndPoint;
                                        HttpSession s = null;

                                        if (Keys.Count() == 1)
                                        {
                                            var SessionId = a.Request.QueryString[Keys.Single()];
                                            var Close = false;
                                            SessionSets.DoAction
                                            (
                                                ss =>
                                                {
                                                    if (!ss.SessionIdToSession.ContainsKey(SessionId))
                                                    {
                                                        a.Response.StatusCode = 403;
                                                        Close = true;
                                                        return;
                                                    }
                                                    var CurrentSession = ss.SessionIdToSession[SessionId];
                                                    if (CurrentSession.RemoteEndPoint != e)
                                                    {
                                                        a.Response.StatusCode = 403;
                                                        Close = true;
                                                        return;
                                                    }
                                                    s = ss.SessionIdToSession[SessionId];
                                                }
                                            );
                                            if (Close)
                                            {
                                                StoppingSockets.Add(a);
                                                continue;
                                            }
                                            var NewSessionId = Convert.ToBase64String(Cryptography.CreateRandom(64));
                                            SessionSets.DoAction
                                            (
                                                ss =>
                                                {
                                                    ss.SessionIdToSession.Remove(SessionId);
                                                    ss.SessionIdToSession.Add(NewSessionId, s);
                                                    ss.SessionToId[s] = NewSessionId;
                                                }
                                            );
                                            if (!s.Push(a, NewSessionId))
                                            {
                                                StoppingSockets.Add(a);
                                            }
                                            continue;
                                        }

                                        if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                        {
                                            PurifyOneInSession();
                                        }
                                        if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                        {
                                            a.Response.StatusCode = 503;
                                            StoppingSockets.Add(a);
                                            continue;
                                        }

                                        if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? ss.IpSessions[e.Address] : 0) >= MaxConnectionsPerIPValue.Value))
                                        {
                                            a.Response.StatusCode = 503;
                                            StoppingSockets.Add(a);
                                            continue;
                                        }

                                        s = new HttpSession(e) { Server = this };

                                        {
                                            var SessionId = Convert.ToBase64String(Cryptography.CreateRandom(64));
                                            SessionSets.DoAction
                                            (
                                                ss =>
                                                {
                                                    ss.Sessions.Add(s);
                                                    if (ss.IpSessions.ContainsKey(e.Address))
                                                    {
                                                        ss.IpSessions[e.Address] += 1;
                                                    }
                                                    else
                                                    {
                                                        ss.IpSessions.Add(e.Address, 1);
                                                    }
                                                    ss.SessionIdToSession.Add(SessionId, s);
                                                    ss.SessionToId.Add(s, SessionId);
                                                }
                                            );

                                            s.Start();
                                            if (!s.Push(a, SessionId))
                                            {
                                                StoppingSockets.Add(a);
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

                                    HttpSession StoppingSession;
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
                        ListeningTask.Start();

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
                        ListeningTask.Wait();
                        Listener.Close();
                        Listener = null;
                        ListeningTaskTokenSource = null;
                        ListeningTask = null;
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

        public void NotifySessionQuit(HttpSession s)
        {
            StoppingSessions.Add(s);
            PurifieringTaskNotifier.Set();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
