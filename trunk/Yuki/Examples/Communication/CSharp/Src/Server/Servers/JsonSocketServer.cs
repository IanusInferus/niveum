using System;
using System.Collections.Generic;
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
        private class WorkPart
        {
            public ServerImplementation si;
            public JsonServer<SessionContext> js;
        }
        private ThreadLocal<WorkPart> WorkPartInstance;
        public JsonServer<SessionContext> InnerServer { get { return WorkPartInstance.Value.js; } }
        public ServerContext ServerContext { get; private set; }

        public delegate Boolean CheckCommandAllowedDelegate(SessionContext c, String CommandName);
        private CheckCommandAllowedDelegate CheckCommandAllowedValue = null;
        private Action ShutdownValue = null;
        private int MaxBadCommandsValue = 8;
        private Boolean ClientDebugValue = false;
        private Boolean EnableLogNormalInValue = true;
        private Boolean EnableLogNormalOutValue = true;
        private Boolean EnableLogUnknownErrorValue = true;
        private Boolean EnableLogCriticalErrorValue = true;
        private Boolean EnableLogPerformanceValue = true;
        private Boolean EnableLogSystemValue = true;

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
        public Action Shutdown
        {
            get
            {
                return ShutdownValue;
            }
            set
            {
                if (IsRunning) { throw new InvalidOperationException(); }
                ShutdownValue = value;
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

        public LockedVariable<Dictionary<SessionContext, JsonSocketSession>> SessionMappings = new LockedVariable<Dictionary<SessionContext, JsonSocketSession>>(new Dictionary<SessionContext,JsonSocketSession>());

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
            ServerContext.GetSessions = () => SessionMappings.Check(Mappings => Mappings.Keys.ToList());

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

                    var srv = new JsonServer<SessionContext>(law);
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

        private void OnServerEvent(SessionContext c, String CommandName, UInt32 CommandHash, String Parameters)
        {
            JsonSocketSession Session = null;
            SessionMappings.DoAction(Mappings =>
            {
                if (Mappings.ContainsKey(c))
                {
                    Session = Mappings[c];
                }
            });
            if (Session != null)
            {
                Session.WriteLine(CommandName, CommandHash, Parameters);
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
