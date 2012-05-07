using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Json;
using Server.Services;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class JsonSocketServer : TcpServer<JsonSocketServer, JsonSocketSession>
    {
        public Action Shutdown;

        private class WorkPart
        {
            public ServerImplementation si;
            public JsonServer<SessionContext> js;
        }
        private ThreadLocal<WorkPart> WorkPartInstance;
        public JsonServer<SessionContext> InnerServer { get { return WorkPartInstance.Value.js; } }
        public ServerContext ServerContext { get; private set; }

        private int MaxBadCommandsValue = 8;
        private Boolean ClientDebugValue = false;
        private Boolean EnableLogNormalInValue = true;
        private Boolean EnableLogNormalOutValue = true;
        private Boolean EnableLogUnknownErrorValue = true;
        private Boolean EnableLogCriticalErrorValue = true;
        private Boolean EnableLogPerformanceValue = true;
        private Boolean EnableLogSystemValue = true;

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

        public ConcurrentDictionary<SessionContext, JsonSocketSession> SessionMappings = new ConcurrentDictionary<SessionContext, JsonSocketSession>();

        public JsonSocketServer()
        {
            ServerContext = new ServerContext();
            ServerContext.Shutdown += () =>
            {
                if (Shutdown != null)
                {
                    Shutdown();
                }
            };
            ServerContext.GetSessions = () => SessionMappings.Keys;

            WorkPartInstance = new ThreadLocal<WorkPart>
            (
                () =>
                {
                    var si = new ServerImplementation(ServerContext);
                    var srv = new JsonServer<SessionContext>(si);
                    srv.ServerEvent += OnServerEvent;
                    return new WorkPart { si = si, js = srv };
                }
            );
            ServerContext.SchemaHash = InnerServer.Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);

            base.MaxConnectionsExceeded += OnMaxConnectionsExceeded;
            base.MaxConnectionsPerIPExceeded += OnMaxConnectionsPerIPExceeded;
        }

        public void RaiseError(SessionContext c, String CommandName, String Message)
        {
            WorkPartInstance.Value.si.RaiseError(c, CommandName, Message);
        }

        private void OnServerEvent(SessionContext c, String CommandName, String Parameters)
        {
            JsonSocketSession Session = null;
            SessionMappings.TryGetValue(c, out Session);
            if (Session != null)
            {
                Session.WriteLine(CommandName, Parameters);
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

        private void OnMaxConnectionsExceeded(JsonSocketSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections, please try again later.");
            }
        }
        private void OnMaxConnectionsPerIPExceeded(JsonSocketSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections from your IP(" + s.RemoteEndPoint.Address + "), please try again later.");
            }
        }
    }
}
