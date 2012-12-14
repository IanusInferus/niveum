using System;
using System.Collections.Generic;

namespace Server
{
    public class Binding
    {
        public String IpAddress;
        public int Port;
    }

    public class VirtualServer
    {
        public SerializationProtocolType ProtocolType;
        public Binding[] Bindings;
        public Boolean EnableLogNormalIn;
        public Boolean EnableLogNormalOut;
        public Boolean EnableLogUnknownError;
        public Boolean EnableLogCriticalError;
        public Boolean EnableLogPerformance;
        public Boolean EnableLogSystem;
        public Boolean EnableLogConsole;
        public int SessionIdleTimeout;
        public int MaxConnections;
        public int MaxConnectionsPerIP;
        public int MaxBadCommands;
        public Boolean ClientDebug;
    }

    public class Configuration
    {
        public VirtualServer[] Servers;
    }
}
