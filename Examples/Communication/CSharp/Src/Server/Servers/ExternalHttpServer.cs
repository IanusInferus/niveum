using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using Firefly;
using BaseSystem;

namespace Server
{
    public static class ExternalHttpListenerRequestExtension
    {
        public static Dictionary<String, String> GetQuery(String q)
        {
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
        public static Optional<String> ReadUtf8StringInLength(Stream s, int MaxLength)
        {
            var Bytes = new List<Byte>();
            for (int k = 0; k <= MaxLength + 1; k += 1)
            {
                if (k == MaxLength + 1) { return Optional<String>.Empty; }
                var b = s.ReadByte();
                if (b == -1)
                {
                    break;
                }
                Bytes.Add((Byte)(b));
            }
            return System.Text.Encoding.UTF8.GetString(Bytes.ToArray());
        }
    }

    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class ExternalHttpServer : IServer
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

        public IServerContext ServerContext { get; private set; }
        private Action<Action> QueueUserWorkItem;
        private int ReadBufferSize;

        private Action<String, HttpListenerContext, IPEndPoint, Action, Action<Exception>> RequestHandler;
        private int MaxBadCommandsValue = 8;
        private String[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;
        private int? MaxUnauthenticatedPerIPValue = null;

        private String ServiceVirtualPathValue = null;
        private Regex rServiceVirtualPath = null;

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
                        rServiceVirtualPath = new Regex("^" + value + "$", RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    }
                );
            }
        }

        public ExternalHttpServer(IServerContext sc, Action<String, HttpListenerContext, IPEndPoint, Action, Action<Exception>> RequestHandler, Action<Action> QueueUserWorkItem, int ReadBufferSize = 8 * 1024)
        {
            ServerContext = sc;
            this.RequestHandler = RequestHandler;
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.ReadBufferSize = ReadBufferSize;
        }

        private Optional<String> MatchBindingNameAndGetRelativePath(Uri Url)
        {
            foreach (var b in Bindings)
            {
                var u = new Uri(b.Replace("*", "localhost").Replace("+", "localhost"));
                if (!Url.AbsolutePath.StartsWith(u.AbsolutePath, StringComparison.OrdinalIgnoreCase)) { continue; }
                var RelativePath = Url.AbsolutePath.Substring(u.AbsolutePath.Length);
                if (rServiceVirtualPath.Match(RelativePath).Success)
                {
                    return RelativePath;
                }
            }
            return Optional<String>.Empty;
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
                        if (SessionIdleTimeoutValue.HasValue)
                        {
                            SetTimer(Listener, SessionIdleTimeoutValue.Value);
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

                        Action<HttpListenerContext> Accept = a =>
                        {
                            IPEndPoint e = null;
                            try
                            {
                                e = (IPEndPoint)a.Request.RemoteEndPoint;
                                var XForwardedFor = a.Request.Headers["X-Forwarded-For"];
                                var Address = e.Address;
                                if (XForwardedFor != null)
                                {
                                    try
                                    {
                                        Address = IPAddress.Parse(XForwardedFor.Split(',')[0].Trim(' '));
                                    }
                                    catch
                                    {
                                    }
                                }
                                var XForwardedPort = a.Request.Headers["X-Forwarded-Port"];
                                var Port = e.Port;
                                if (XForwardedPort != null)
                                {
                                    try
                                    {
                                        Port = int.Parse(XForwardedPort.Split(',')[0].Trim(' '));
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

                                var oRelativePath = MatchBindingNameAndGetRelativePath(a.Request.Url);
                                if (oRelativePath.OnNone)
                                {
                                    a.Response.StatusCode = 404;
                                    NotifyListenerContextQuit(a);
                                    return;
                                }
                                var RelativePath = oRelativePath.Value;

                                var Headers = a.Request.Headers.AllKeys.ToDictionary(k => k, k => a.Request.Headers[k]);
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

                                if (RequestHandler != null)
                                {
                                    RequestHandler(RelativePath, a, e, () => NotifyListenerContextQuit(a), ex =>
                                    {
                                        if (ServerContext.EnableLogSystem)
                                        {
                                            ServerContext.RaiseSessionLog(new SessionLogEntry { Token = "", RemoteEndPoint = e, Time = DateTime.UtcNow, Type = "Sys", Name = "Exception", Message = ExceptionInfo.GetExceptionInfo(ex) });
                                        }
                                        NotifyListenerContextQuit(a);
                                    });
                                }
                                else
                                {
                                    NotifyListenerContextQuit(a);
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

                    if (ContextPurifyConsumer != null)
                    {
                        ContextPurifyConsumer.Dispose();
                        ContextPurifyConsumer = null;
                    }

                    return false;
                }
            );
        }

        public void NotifyListenerContextQuit(HttpListenerContext ListenerContext)
        {
            ContextPurifyConsumer.Push(ListenerContext);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
