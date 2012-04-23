//==========================================================================
//
//  File:        Configuration.cs
//  Location:    Server.GameServer <Visual C#>
//  Description: 配置文件
//  Version:     2012.02.20.
//  Copyright(C) 上海幻达网络科技有限公司 2011-2012
//
//==========================================================================

using System;
using System.Collections.Generic;

namespace Server
{
    public class Binding
    {
        public String IpAddress;
        public int Port;
    }

    public enum ProtocolType
    {
        Binary,
        Json
    }

    public class Configuration
    {
        public ProtocolType ProtocolType;
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
}
