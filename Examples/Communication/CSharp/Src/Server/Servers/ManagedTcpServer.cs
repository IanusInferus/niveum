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
    public class ManagedTcpServer : TcpServer<ManagedTcpServer, ManagedTcpSession>
    {
        private class WorkPart
        {
            public ServerImplementation si;
            public IVirtualTransportServer<SessionContext> vts;
        }
        private ThreadLocal<WorkPart> WorkPartInstance;
        public ServerImplementation ApplicationServer { get { return WorkPartInstance.Value.si; } }
        public IVirtualTransportServer<SessionContext> VirtualTransportServer { get { return WorkPartInstance.Value.vts; } }
        public ServerContext ServerContext { get; private set; }

        private ProtocolType ProtocolTypeValue = ProtocolType.Binary;
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

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public ProtocolType ProtocolType
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
                    if (ProtocolType == ProtocolType.Binary)
                    {
                        BinaryCountPacketServer<SessionContext>.CheckCommandAllowedDelegate cca = (c, CommandName) =>
                        {
                            if (this.CheckCommandAllowed == null) { return true; }
                            return this.CheckCommandAllowed(c, CommandName);
                        };
                        vts = new BinaryCountPacketServer<SessionContext>(law, c => c.BinaryCountPacketContext, cca);
                    }
                    else if (ProtocolType == ProtocolType.Json)
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
                        throw new InvalidOperationException("未知协议类型: " + ProtocolType.ToString());
                    }

                    vts.ServerEvent += OnServerEvent;
                    return new WorkPart { si = si, vts = vts };
                }
            );

            base.MaxConnectionsExceeded += OnMaxConnectionsExceeded;
            base.MaxConnectionsPerIPExceeded += OnMaxConnectionsPerIPExceeded;
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
    }
}
