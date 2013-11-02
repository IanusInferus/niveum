using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Communication;
using Communication.Binary;
using Communication.Json;

namespace Server
{
    public class BinarySerializationServerAdapter : IBinarySerializationServerAdapter
    {
        private IApplicationServer s;
        private static ThreadLocal<BinarySerializationServer> sss = new ThreadLocal<BinarySerializationServer>(() => new BinarySerializationServer());
        private BinarySerializationServer ss = sss.Value;
        private BinarySerializationServerEventDispatcher ssed;

        public BinarySerializationServerAdapter(IApplicationServer ApplicationServer)
        {
            this.s = ApplicationServer;
            this.ssed = new BinarySerializationServerEventDispatcher(ApplicationServer);
            this.ssed.ServerEvent += (CommandName, CommandHash, Parameters) =>
            {
                if (ServerEvent != null)
                {
                    ServerEvent(CommandName, CommandHash, Parameters);
                }
            };
        }

        public UInt64 Hash { get { return ss.Hash; } }
        public Boolean HasCommand(String CommandName, UInt32 CommandHash) { return ss.HasCommand(CommandName, CommandHash); }
        public Byte[] ExecuteCommand(String CommandName, UInt32 CommandHash, Byte[] Parameters) { return ss.ExecuteCommand(s, CommandName, CommandHash, Parameters); }
        public event BinaryServerEventDelegate ServerEvent;
    }

    public class JsonSerializationServerAdapter : IJsonSerializationServerAdapter
    {
        private IApplicationServer s;
        private static ThreadLocal<JsonSerializationServer> sss = new ThreadLocal<JsonSerializationServer>(() => new JsonSerializationServer());
        private JsonSerializationServer ss = sss.Value;
        private JsonSerializationServerEventDispatcher ssed;

        public JsonSerializationServerAdapter(IApplicationServer ApplicationServer)
        {
            this.s = ApplicationServer;
            this.ssed = new JsonSerializationServerEventDispatcher(ApplicationServer);
            this.ssed.ServerEvent += (CommandName, CommandHash, Parameters) =>
            {
                if (ServerEvent != null)
                {
                    ServerEvent(CommandName, CommandHash, Parameters);
                }
            };
        }

        public UInt64 Hash { get { return ss.Hash; } }
        public Boolean HasCommand(String CommandName) { return ss.HasCommand(CommandName); }
        public Boolean HasCommand(String CommandName, UInt32 CommandHash) { return ss.HasCommand(CommandName, CommandHash); }
        public String ExecuteCommand(String CommandName, String Parameters) { return ss.ExecuteCommand(s, CommandName, Parameters); }
        public String ExecuteCommand(String CommandName, UInt32 CommandHash, String Parameters) { return ss.ExecuteCommand(s, CommandName, CommandHash, Parameters); }
        public event JsonServerEventDelegate ServerEvent;
    }
}
