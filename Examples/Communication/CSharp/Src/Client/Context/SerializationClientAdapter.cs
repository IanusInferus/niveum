using System;
using Communication;
using Communication.Binary;
using Communication.Json;

namespace Client
{
    public class BinarySerializationClientAdapter : IBinarySerializationClientAdapter, IBinarySender
    {
        private BinarySerializationClient bc;
        public BinarySerializationClientAdapter()
        {
            this.bc = new BinarySerializationClient(this);
            var ac = bc.GetApplicationClient();
            ac.ErrorCommand += e => ac.DequeueCallback(e.CommandName);
        }

        public IApplicationClient GetApplicationClient()
        {
            return bc.GetApplicationClient();
        }

        public UInt64 Hash { get { return bc.GetApplicationClient().Hash; } }
        public void DequeueCallback(String CommandName) { bc.GetApplicationClient().DequeueCallback(CommandName); }
        public void HandleResult(String CommandName, UInt32 CommandHash, Byte[] Parameters) { bc.HandleResult(CommandName, CommandHash, Parameters); }
        public event BinaryClientEventDelegate ClientEvent;

        void IBinarySender.Send(String CommandName, UInt32 CommandHash, Byte[] Parameters)
        {
            if (ClientEvent != null)
            {
                ClientEvent(CommandName, CommandHash, Parameters);
            }
        }
    }

    public class JsonSerializationClientAdapter : IJsonSerializationClientAdapter, IJsonSender
    {
        private JsonSerializationClient jc;
        public JsonSerializationClientAdapter()
        {
            this.jc = new JsonSerializationClient(this);
            var ac = jc.GetApplicationClient();
            ac.ErrorCommand += e => ac.DequeueCallback(e.CommandName);
        }

        public IApplicationClient GetApplicationClient()
        {
            return jc.GetApplicationClient();
        }

        public UInt64 Hash { get { return jc.GetApplicationClient().Hash; } }
        public void DequeueCallback(String CommandName) { jc.GetApplicationClient().DequeueCallback(CommandName); }
        public void HandleResult(String CommandName, UInt32 CommandHash, String Parameters) { jc.HandleResult(CommandName, CommandHash, Parameters); }
        public event JsonClientEventDelegate ClientEvent;

        void IJsonSender.Send(String CommandName, UInt32 CommandHash, String Parameters)
        {
            if (ClientEvent != null)
            {
                ClientEvent(CommandName, CommandHash, Parameters);
            }
        }
    }
}
