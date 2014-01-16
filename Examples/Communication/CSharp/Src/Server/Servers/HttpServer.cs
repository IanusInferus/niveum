using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Firefly;
using BaseSystem;
using Algorithms;

namespace Server
{
    public partial class Http<TServerContext>
        where TServerContext : IServerContext
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
            private ConcurrentQueue<HttpSession> StoppingSessions = new ConcurrentQueue<HttpSession>();
            private ConcurrentQueue<HttpListenerContext> StoppingListenerContexts = new ConcurrentQueue<HttpListenerContext>();
            private LockedVariable<ServerSessionSets> SessionSets = new LockedVariable<ServerSessionSets>(new ServerSessionSets());

            public TServerContext ServerContext { get; private set; }

            public delegate Boolean CheckCommandAllowedDelegate(ISessionContext c, String CommandName);
            private CheckCommandAllowedDelegate CheckCommandAllowedValue = null;
            private int MaxBadCommandsValue = 8;
            private String[] BindingsValue = { };
            private int? SessionIdleTimeoutValue = null;
            private int? MaxConnectionsValue = null;
            private int? MaxConnectionsPerIPValue = null;
            private int? MaxUnauthenticatedPerIPValue = null;

            private int TimeoutCheckPeriodValue = 30;
            private String ServiceVirtualPathValue = null;

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

            public HttpServer(TServerContext sc)
            {
                ServerContext = sc;
            }

            private static String GetBindingName(Uri Url)
            {
                var p = Url.AbsolutePath;
                if (p.StartsWith("/")) { return p.Substring(1); }
                return p;
            }

            private static void SetTimer(HttpListener Listener, int Seconds)
            {
                if (typeof(HttpListener).GetProperty("TimeoutManager") != null)
                {
                    var ts = TimeSpan.FromSeconds(Seconds);
                    var p = typeof(HttpListener).GetProperty("TimeoutManager");
                    var tm = p.GetValue(Listener, null);
                    var tmt = p.PropertyType;
                    tmt.GetProperty("DrainEntityBody").SetValue(tm, ts, null);
                    tmt.GetProperty("EntityBody").SetValue(tm, ts, null);
                    tmt.GetProperty("HeaderWait").SetValue(tm, ts, null);
                    tmt.GetProperty("IdleConnection").SetValue(tm, ts, null);
                    tmt.GetProperty("RequestQueue").SetValue(tm, ts, null);
                }
                else
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
                                        ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0), Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = Message });
                                        return;
                                    }
                                    catch (Exception ex)
                                    {
                                        ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0), Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                        return;
                                    }
                                    Boolean ListenerStopped = false;
                                    try
                                    {
                                        while (true)
                                        {
                                            if (ListeningTaskToken.IsCancellationRequested) { return; }
                                            var l = Listener;
                                            using (var Finished = new AutoResetEvent(false))
                                            {
                                                var ca = l.BeginGetContext(ar =>
                                                {
                                                    try
                                                    {
                                                        var a = l.EndGetContext(ar);
                                                        AcceptedListenerContexts.Enqueue(a);
                                                        AcceptingTaskNotifier.Set();
                                                    }
                                                    catch (HttpListenerException)
                                                    {
                                                    }
                                                    finally
                                                    {
                                                        Finished.Set();
                                                    }
                                                }, null);
                                                var Index = WaitHandle.WaitAny(new WaitHandle[] { Finished, ListeningTaskToken.WaitHandle });
                                                if (Index == 1)
                                                {
                                                    l.Stop();
                                                    ListenerStopped = true;
                                                    Finished.WaitOne();
                                                }
                                                ca.AsyncWaitHandle.WaitOne();
                                                ca.AsyncWaitHandle.Dispose();
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (!ListenerStopped)
                                        {
                                            Listener.Stop();
                                        }
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
                                                if (ServerContext.EnableLogSystem)
                                                {
                                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Name = "RequestIn", Message = "" });
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

                                                if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? ss.IpSessions[e.Address].Count : 0) >= MaxConnectionsPerIPValue.Value))
                                                {
                                                    a.Response.StatusCode = 503;
                                                    NotifyListenerContextQuit(a);
                                                    continue;
                                                }

                                                if (MaxUnauthenticatedPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(e.Address) ? (ss.IpSessions[e.Address].Count - ss.IpSessions[e.Address].Authenticated.Count) : 0) >= MaxUnauthenticatedPerIPValue.Value))
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
                                                        continue;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (ServerContext.EnableLogSystem)
                                                {
                                                    ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                                }
                                                NotifyListenerContextQuit(a);
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
                                            var CheckTime = DateTime.UtcNow.AddIntSeconds(-SessionIdleTimeoutValue.Value);
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
                                            try
                                            {
                                                ListenerContext.Response.Close();
                                            }
                                            catch
                                            {
                                            }
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
}
