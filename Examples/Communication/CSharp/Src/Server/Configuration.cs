using System;
using System.Collections.Generic;

namespace Server
{
    public class Binding
    {
        public String IpAddress;
        public int Port;
    }

    public enum VirtualServerTag
    {
        Tcp = 0,
        Http = 1
    }
    [TaggedUnion]
    public class VirtualServerConfiguration
    {
        [Tag]
        public VirtualServerTag _Tag;
        public TcpServerConfiguration Tcp;
        public HttpServerConfiguration Http;

        public static VirtualServerConfiguration CreateTcp(TcpServerConfiguration Value) { return new VirtualServerConfiguration { _Tag = VirtualServerTag.Tcp, Tcp = Value }; }
        public static VirtualServerConfiguration CreateHttp(HttpServerConfiguration Value) { return new VirtualServerConfiguration { _Tag = VirtualServerTag.Http, Http = Value }; }

        public Boolean OnTcp { get { return _Tag == VirtualServerTag.Tcp; } }
        public Boolean OnHttp { get { return _Tag == VirtualServerTag.Http; } }
    }

    public class TcpServerConfiguration
    {
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

        public SerializationProtocolType SerializationProtocolType;
    }

    public class HttpServerConfiguration
    {
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
        public VirtualServerConfiguration[] Servers;
    }
}
