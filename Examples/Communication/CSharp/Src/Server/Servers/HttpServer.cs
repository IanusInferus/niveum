﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Firefly;
using Communication.BaseSystem;
using Server.Algorithms;

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
        HttpVirtualTransportServerHandleResult Handle(JObject CommandObject);
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
        private ConcurrentQueue<HttpListenerContext> AcceptedListenerContexts = new ConcurrentQueue<HttpListenerContext>();
        private Task AcceptingTask;
        private CancellationTokenSource AcceptingTaskTokenSource;
        private AutoResetEvent AcceptingTaskNotifier;
        private Task PurifieringTask;
        private CancellationTokenSource PurifieringTaskTokenSource;
        private AutoResetEvent PurifieringTaskNotifier;
        private Timer LastActiveTimeCheckTimer;

        private class ServerSessionSets
        {
            public HashSet<HttpSession> Sessions = new HashSet<HttpSession>();
            public Dictionary<IPAddress, int> IpSessions = new Dictionary<IPAddress, int>();
            public Dictionary<String, HttpSession> SessionIdToSession = new Dictionary<String, HttpSession>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<HttpSession, String> SessionToId = new Dictionary<HttpSession, String>();
        }
        private ConcurrentQueue<HttpSession> StoppingSessions = new ConcurrentQueue<HttpSession>();
        private ConcurrentQueue<HttpListenerContext> StoppingListenerContexts = new ConcurrentQueue<HttpListenerContext>();
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

        private int TimeoutCheckPeriodValue = 30;
        private String ServiceVirtualPathValue = null;
        private Optional<HttpStaticContentPath> StaticContentPathValue = Optional<HttpStaticContentPath>.Empty;

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
        public String ServiceVirtualPath
        {
            get
            {
                return ServiceVirtualPathValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        ServiceVirtualPathValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Optional<HttpStaticContentPath> StaticContentPath
        {
            get
            {
                return StaticContentPathValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        StaticContentPathValue = value;
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

        private static String GetBindingName(Uri Url)
        {
            var p = Url.AbsolutePath;
            if (p.StartsWith("/")) { return p.Substring(1); }
            return p;
        }

        private static void SetTimerInner(HttpListener Listener, int Seconds)
        {
            var tm = Listener.TimeoutManager;
            var ts = TimeSpan.FromSeconds(Seconds);
            tm.DrainEntityBody = ts;
            tm.EntityBody = ts;
            tm.HeaderWait = ts;
            tm.IdleConnection = ts;
            tm.RequestQueue = ts;
        }
        private static void SetTimer(HttpListener Listener, int Seconds)
        {
            if (typeof(HttpListener).GetProperty("TimeoutManager", System.Reflection.BindingFlags.Public) != null)
            {
                SetTimerInner(Listener, Seconds);
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
                            SetTimer(Listener, SessionIdleTimeoutValue.Value);
                        }
                        ListeningTask = new Task
                        (
                            () =>
                            {
                                try
                                {
                                    Listener.Start();
                                }
                                catch (HttpListenerException ex)
                                {
                                    String Message;
                                    if (ex.ErrorCode == 5)
                                    {
                                        var l = new List<String>();
                                        l.Add("Under Windows, try run the following as administrator:");
                                        var UserDomainName = Environment.UserDomainName;
                                        var UserName = Environment.UserName;
                                        foreach (var p in BindingsValue)
                                        {
                                            l.Add(@"netsh http add urlacl url={0} user={1}\{2}".Formats(p, UserDomainName, UserName));
                                        }
                                        l.Add("and delete it when you don't need it:");
                                        foreach (var p in BindingsValue)
                                        {
                                            l.Add(@"netsh http delete urlacl url={0}".Formats(p));
                                        }
                                        Message = String.Join("\r\n", l.ToArray());
                                    }
                                    else
                                    {
                                        Message = ExceptionInfo.GetExceptionInfo(ex);
                                    }
                                    RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0), Time = DateTime.UtcNow, Type = "Sys", Message = Message });
                                }
                                catch (Exception ex)
                                {
                                    RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0), Time = DateTime.UtcNow, Type = "Sys", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                }
                                try
                                {
                                    while (true)
                                    {
                                        if (ListeningTaskToken.IsCancellationRequested) { return; }
                                        var ca = Listener.BeginGetContext(ar =>
                                        {
                                            var a = Listener.EndGetContext(ar);
                                            AcceptedListenerContexts.Enqueue(a);
                                            AcceptingTaskNotifier.Set();
                                        }, null);
                                        try
                                        {
                                            WaitHandle.WaitAny(new WaitHandle[] { ca.AsyncWaitHandle, ListeningTaskToken.WaitHandle });
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
                                        HttpListenerContext a;
                                        if (!AcceptedListenerContexts.TryDequeue(out a))
                                        {
                                            break;
                                        }

                                        var e = (IPEndPoint)a.Request.RemoteEndPoint;

                                        try
                                        {
                                            if (EnableLogSystem)
                                            {
                                                RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Message = "RequestIn" });
                                            }

                                            if (a.Request.ContentLength64 < 0)
                                            {
                                                a.Response.StatusCode = 411;
                                                NotifyListenerContextQuit(a);
                                                continue;
                                            }

                                            if (a.Request.ContentLength64 > 8 * 1024)
                                            {
                                                a.Response.StatusCode = 413;
                                                NotifyListenerContextQuit(a);
                                                continue;
                                            }

                                            var BindingName = GetBindingName(a.Request.Url);
                                            if (!BindingName.Equals(ServiceVirtualPathValue, StringComparison.OrdinalIgnoreCase))
                                            {
                                                a.Response.StatusCode = 404;
                                                NotifyListenerContextQuit(a);
                                                continue;
                                            }

                                            var Headers = a.Request.Headers.AllKeys.ToDictionary(k => k, k => a.Request.Headers[k]);
                                            if (Headers.ContainsKey("Range"))
                                            {
                                                a.Response.StatusCode = 400;
                                                NotifyListenerContextQuit(a);
                                                continue;
                                            }
                                            if (Headers.ContainsKey("Accept-Charset"))
                                            {
                                                var AcceptCharsetParts = Headers["Accept-Charset"].Split(';');
                                                if (AcceptCharsetParts.Length == 0)
                                                {
                                                    a.Response.StatusCode = 400;
                                                    NotifyListenerContextQuit(a);
                                                    continue;
                                                }
                                                var EncodingNames = AcceptCharsetParts[0].Split(',').Select(n => n.Trim(' ')).ToArray();
                                                if (!(EncodingNames.Contains("utf-8", StringComparer.OrdinalIgnoreCase) || EncodingNames.Contains("*", StringComparer.OrdinalIgnoreCase)))
                                                {
                                                    a.Response.StatusCode = 400;
                                                    NotifyListenerContextQuit(a);
                                                    continue;
                                                }
                                            }

                                            if (a.Request.QueryString != null)
                                            {
                                                var Keys = a.Request.QueryString.AllKeys.Where(k => k != null && k.Equals("sessionid", StringComparison.OrdinalIgnoreCase)).ToArray();
                                                if (Keys.Count() > 1)
                                                {
                                                    a.Response.StatusCode = 400;
                                                    NotifyListenerContextQuit(a);
                                                    continue;
                                                }

                                                if (Keys.Count() == 1)
                                                {
                                                    HttpSession s = null;

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
                                                            if (!CurrentSession.RemoteEndPoint.Address.Equals(e.Address))
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
                                                        NotifyListenerContextQuit(a);
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
                                                        NotifyListenerContextQuit(a);
                                                        continue;
                                                    }
                                                    continue;
                                                }
                                            }

                                            if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                            {
                                                PurifyOneInSession();
                                            }
                                            if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                            {
                                                a.Response.StatusCode = 503;
                                                NotifyListenerContextQuit(a);
                                                continue;
                                            }

                                            if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? ss.IpSessions[e.Address] : 0) >= MaxConnectionsPerIPValue.Value))
                                            {
                                                a.Response.StatusCode = 503;
                                                NotifyListenerContextQuit(a);
                                                continue;
                                            }

                                            {
                                                var s = new HttpSession(this, e);

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
                                                    NotifyListenerContextQuit(a);
                                                    continue;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            if (EnableLogSystem)
                                            {
                                                RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Message = ExceptionInfo.GetExceptionInfo(ex) });
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

                                    if (SessionIdleTimeout.HasValue)
                                    {
                                        var CheckTime = DateTime.UtcNow.AddSeconds(-SessionIdleTimeoutValue.Value);
                                        SessionSets.DoAction
                                        (
                                            ss =>
                                            {
                                                foreach (var s in ss.Sessions)
                                                {
                                                    if (s.LastActiveTime < CheckTime)
                                                    {
                                                        StoppingSessions.Enqueue(s);
                                                    }
                                                }
                                            }
                                        );
                                    }

                                    HttpSession StoppingSession;
                                    while (StoppingSessions.TryDequeue(out StoppingSession))
                                    {
                                        Purify(StoppingSession);
                                    }

                                    HttpListenerContext ListenerContext;
                                    while (StoppingListenerContexts.TryDequeue(out ListenerContext))
                                    {
                                        ListenerContext.Response.Close();
                                    }
                                }
                            },
                            PurifieringTaskToken,
                            TaskCreationOptions.LongRunning
                        );

                        if (SessionIdleTimeoutValue.HasValue)
                        {
                            var TimePeriod = TimeSpan.FromSeconds(Math.Max(TimeoutCheckPeriodValue, 1));
                            LastActiveTimeCheckTimer = new Timer(state => { PurifieringTaskNotifier.Set(); }, null, TimePeriod, TimePeriod);
                        }

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

                    if (LastActiveTimeCheckTimer != null)
                    {
                        LastActiveTimeCheckTimer.Dispose();
                        LastActiveTimeCheckTimer = null;
                    }

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

        public void NotifyListenerContextQuit(HttpListenerContext ListenerContext)
        {
            StoppingListenerContexts.Enqueue(ListenerContext);
            PurifieringTaskNotifier.Set();
        }
        public void NotifySessionQuit(HttpSession s)
        {
            StoppingSessions.Enqueue(s);
            PurifieringTaskNotifier.Set();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
