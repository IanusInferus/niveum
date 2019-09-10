using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Niveum.Json;
using Firefly;
using BaseSystem;
using Algorithms;

namespace Server
{
    public class HttpVirtualTransportServerHandleResultCommand
    {
        public String CommandName;
        public Action<Action, Action<Exception>> ExecuteCommand;
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

    public static class HttpListenerRequestExtension
    {
        public static Dictionary<String, String> GetQuery(HttpListenerRequest r)
        {
            var q = r.Url.Query;
            if (q.StartsWith("?"))
            {
                q = q.Substring(1);
            }
            var d = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in q.Split('&'))
            {
                var Parts = p.Split('=');
                if (Parts.Length != 2) { continue; }
                var Key = Uri.UnescapeDataString(Parts[0]);
                var Value = Uri.UnescapeDataString(Parts[1]);
                if (d.ContainsKey(Key))
                {
                    d.Remove(Key);
                }
                d.Add(Key, Value);
            }
            return d;
        }
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
        private CancellationTokenSource ListeningTaskTokenSource;
        private AsyncConsumer<int> ListenConsumer;
        private AsyncConsumer<HttpListenerContext> AcceptConsumer;
        private AsyncConsumer<HttpListenerContext> ContextPurifyConsumer;
        private AsyncConsumer<HttpSession> PurifyConsumer;
        private Timer LastActiveTimeCheckTimer;

        private class IpSessionInfo
        {
            public int Count = 0;
            public HashSet<HttpSession> Authenticated = new HashSet<HttpSession>();
        }
        private class ServerSessionSets
        {
            public HashSet<HttpSession> Sessions = new HashSet<HttpSession>();
            public Dictionary<IPAddress, IpSessionInfo> IpSessions = new Dictionary<IPAddress, IpSessionInfo>();
            public Dictionary<String, HttpSession> SessionIdToSession = new Dictionary<String, HttpSession>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<HttpSession, String> SessionToId = new Dictionary<HttpSession, String>();
        }
        private LockedVariable<ServerSessionSets> SessionSets = new LockedVariable<ServerSessionSets>(new ServerSessionSets());

        public IServerContext ServerContext { get; private set; }
        private Func<ISessionContext, KeyValuePair<IServerImplementation, IHttpVirtualTransportServer>> VirtualTransportServerFactory;
        private Action<Action> QueueUserWorkItem;
        private Action<Action> PurifierQueueUserWorkItem;
        private int ReadBufferSize;

        private int MaxBadCommandsValue = 8;
        private String[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? UnauthenticatedSessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;
        private int? MaxUnauthenticatedPerIPValue = null;

        private int TimeoutCheckPeriodValue = 30;
        private String ServiceVirtualPathValue = null;

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

        public LockedVariable<Dictionary<ISessionContext, HttpSession>> SessionMappings = new LockedVariable<Dictionary<ISessionContext, HttpSession>>(new Dictionary<ISessionContext, HttpSession>());

        public HttpServer(IServerContext sc, Func<ISessionContext, KeyValuePair<IServerImplementation, IHttpVirtualTransportServer>> VirtualTransportServerFactory, Action<Action> QueueUserWorkItem, Action<Action> PurifierQueueUserWorkItem, int ReadBufferSize = 128 * 1024)
        {
            ServerContext = sc;
            this.VirtualTransportServerFactory = VirtualTransportServerFactory;
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.PurifierQueueUserWorkItem = PurifierQueueUserWorkItem;
            this.ReadBufferSize = ReadBufferSize;
        }

        private Boolean IsMatchBindingName(Uri Url)
        {
            foreach (var b in Bindings)
            {
                var u = new Uri((b + ServiceVirtualPathValue).Replace("*", "localhost").Replace("+", "localhost"));
                if (u.AbsolutePath.Equals(Url.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SetTimer(HttpListener Listener, int Seconds)
        {
            try
            {
                var ts = TimeSpan.FromSeconds(Seconds);
                var tm = Listener.TimeoutManager;
                tm.DrainEntityBody = ts;
                tm.EntityBody = ts;
                tm.HeaderWait = ts;
                tm.IdleConnection = ts;
                tm.RequestQueue = ts;
            }
            catch (NotImplementedException)
            {
                Console.WriteLine("SetTimerFailed");
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

                        foreach (var Binding in BindingsValue)
                        {
                            Listener.Prefixes.Add(Binding);
                        }
                        if (UnauthenticatedSessionIdleTimeoutValue.HasValue)
                        {
                            SetTimer(Listener, UnauthenticatedSessionIdleTimeoutValue.Value);
                        }

                        Action<HttpListenerContext> PurifyContext = ListenerContext =>
                        {
                            try
                            {
                                ListenerContext.Response.Close();
                            }
                            catch
                            {
                            }
                        };
                        Action<HttpSession> Purify = StoppingSession =>
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
                                        var SessionId = ss.SessionToId[StoppingSession];
                                        ss.SessionIdToSession.Remove(SessionId);
                                        ss.SessionToId.Remove(StoppingSession);
                                    }
                                }
                            );
                            StoppingSession.Dispose();
                        };

                        Action<HttpListenerContext> Accept = a =>
                        {
                            IPEndPoint e = null;
                            try
                            {
                                e = (IPEndPoint)a.Request.RemoteEndPoint;
                                var XForwardedFor = a.Request.Headers["X-Forwarded-For"];
                                var Address = e.Address;
                                if ((XForwardedFor != null) && (XForwardedFor != ""))
                                {
                                    try
                                    {
                                        IPAddress addr;
                                        if (IPAddress.TryParse(XForwardedFor.Split(',')[0].Trim(' '), out addr))
                                        {
                                            Address = addr;
                                        }
                                    }
                                    catch
                                    {
                                    }
                                }
                                var XForwardedPort = a.Request.Headers["X-Forwarded-Port"];
                                var Port = e.Port;
                                if ((XForwardedPort != null) && (XForwardedPort != ""))
                                {
                                    try
                                    {
                                        int p;
                                        if (int.TryParse(XForwardedPort.Split(',')[0].Trim(' '), out p))
                                        {
                                            Port = p;
                                        }
                                    }
                                    catch
                                    {
                                    }
                                }
                                e = new IPEndPoint(Address, Port);

                                if (ServerContext.EnableLogSystem)
                                {
                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Name = "RequestIn", Message = "" });
                                }

                                if (a.Request.ContentLength64 < 0)
                                {
                                    a.Response.StatusCode = 411;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }

                                if (a.Request.ContentLength64 > ReadBufferSize)
                                {
                                    a.Response.StatusCode = 413;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }

                                if (!IsMatchBindingName(a.Request.Url))
                                {
                                    a.Response.StatusCode = 404;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }

                                var Headers = a.Request.Headers.AllKeys.ToDictionary(k => k, k => a.Request.Headers[k]);
                                if (Headers.ContainsKey("Range"))
                                {
                                    a.Response.StatusCode = 400;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }
                                if (Headers.ContainsKey("Accept-Charset"))
                                {
                                    var AcceptCharsetParts = Headers["Accept-Charset"].Split(';');
                                    if (AcceptCharsetParts.Length == 0)
                                    {
                                        a.Response.StatusCode = 400;
                                        NotifyListenerContextQuit(a);
                                        return;
                                    }
                                    var EncodingNames = AcceptCharsetParts[0].Split(',').Select(n => n.Trim(' ')).ToArray();
                                    if (!(EncodingNames.Contains("utf-8", StringComparer.OrdinalIgnoreCase) || EncodingNames.Contains("*", StringComparer.OrdinalIgnoreCase)))
                                    {
                                        a.Response.StatusCode = 400;
                                        NotifyListenerContextQuit(a);
                                        return;
                                    }
                                }

                                {
                                    var Query = HttpListenerRequestExtension.GetQuery(a.Request);

                                    if (Query.ContainsKey("sessionid"))
                                    {
                                        HttpSession s = null;

                                        var SessionId = Query["sessionid"];
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
                                            return;
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
                                            return;
                                        }
                                        return;
                                    }
                                }

                                if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                {
                                    ContextPurifyConsumer.DoOne();
                                    PurifyConsumer.DoOne();
                                }
                                if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                                {
                                    a.Response.StatusCode = 503;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }

                                if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? ss.IpSessions[e.Address].Count : 0) >= MaxConnectionsPerIPValue.Value))
                                {
                                    a.Response.StatusCode = 503;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }

                                if (MaxUnauthenticatedPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? (ss.IpSessions[e.Address].Count - ss.IpSessions[e.Address].Authenticated.Count) : 0) >= MaxUnauthenticatedPerIPValue.Value))
                                {
                                    a.Response.StatusCode = 503;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }

                                {
                                    var s = new HttpSession(this, e, VirtualTransportServerFactory, QueueUserWorkItem);

                                    var SessionId = Convert.ToBase64String(Cryptography.CreateRandom(64));
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
                                            ss.SessionIdToSession.Add(SessionId, s);
                                            ss.SessionToId.Add(s, SessionId);
                                        }
                                    );

                                    s.Start();
                                    if (!s.Push(a, SessionId))
                                    {
                                        NotifyListenerContextQuit(a);
                                        return;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ServerContext.EnableLogSystem)
                                {
                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e ?? new IPEndPoint(IPAddress.Any, 0), Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                }
                                try
                                {
                                    a.Response.StatusCode = 500;
                                }
                                catch
                                {
                                }
                                NotifyListenerContextQuit(a);
                            }
                        };
                        AcceptConsumer = new AsyncConsumer<HttpListenerContext>(QueueUserWorkItem, a => { Accept(a); return true; }, int.MaxValue);

                        ContextPurifyConsumer = new AsyncConsumer<HttpListenerContext>(QueueUserWorkItem, l => { PurifyContext(l); return true; }, int.MaxValue);
                        PurifyConsumer = new AsyncConsumer<HttpSession>(PurifierQueueUserWorkItem, s => { Purify(s); return true; }, int.MaxValue);

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
                            throw new AggregateException(Message, ex);
                        }
                        Action Listen = () =>
                        {
                            if (ListeningTaskToken.IsCancellationRequested) { return; }
                            var l = Listener;
                            var lc = ListenConsumer;
                            l.BeginGetContext(ar =>
                            {
                                if (!l.IsListening) { return; }
                                try
                                {
                                    var a = l.EndGetContext(ar);
                                    AcceptConsumer.Push(a);
                                }
                                catch (HttpListenerException)
                                {
                                }
                                catch (ObjectDisposedException)
                                {
                                }
                                lc.Push(0);
                            }, null);
                        };
                        ListenConsumer = new AsyncConsumer<int>(QueueUserWorkItem, i => { Listen(); return true; }, 1);

                        Listen();

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
                    if (ListenConsumer != null)
                    {
                        Listener.Stop();
                        ListenConsumer.Dispose();
                        Listener.Close();
                        Listener = null;
                        ListenConsumer = null;
                    }
                    if (ListeningTaskTokenSource != null)
                    {
                        ListeningTaskTokenSource = null;
                    }

                    if (AcceptConsumer != null)
                    {
                        AcceptConsumer.Dispose();
                        AcceptConsumer = null;
                    }

                    List<HttpSession> Sessions = null;
                    SessionSets.DoAction
                    (
                        ss =>
                        {
                            Sessions = ss.Sessions.ToList();
                            ss.Sessions.Clear();
                            ss.IpSessions.Clear();
                        }
                    );
                    foreach (var s in Sessions)
                    {
                        s.Dispose();
                    }

                    if (ContextPurifyConsumer != null)
                    {
                        ContextPurifyConsumer.Dispose();
                        ContextPurifyConsumer = null;
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

        public void NotifyListenerContextQuit(HttpListenerContext ListenerContext)
        {
            ContextPurifyConsumer.Push(ListenerContext);
        }
        public void NotifySessionQuit(HttpSession s)
        {
            PurifyConsumer.Push(s);
        }
        public void NotifySessionAuthenticated(HttpSession s)
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
