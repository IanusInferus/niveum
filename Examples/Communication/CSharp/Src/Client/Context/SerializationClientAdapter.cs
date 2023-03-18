using System;
using System.Threading;
using BaseSystem;
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
            ac.ErrorCommand += e => ac.NotifyErrorCommand(e.CommandName, e.Message);
        }

        public IApplicationClient GetApplicationClient()
        {
            return bc.GetApplicationClient();
        }

        public UInt64 Hash { get { return bc.GetApplicationClient().Hash; } }

        private LockedVariable<(String CommandName, UInt32 CommandHash, Timer Timer)?> ResponseTimer = new LockedVariable<(String CommandName, UInt32 CommandHash, Timer Timer)?>(null);
        private LockedVariable<int?> ResponseTimeoutSecondsValue = new LockedVariable<int?>(null);
        public int? ResponseTimeoutSeconds
        {
            get
            {
                return ResponseTimeoutSecondsValue.Check(v => v);
            }
            set
            {
                ResponseTimeoutSecondsValue.Update(v => value);
            }
        }

        public void HandleResult(String CommandName, UInt32 CommandHash, Byte[] Parameters)
        {
            Timer tm = null;
            ResponseTimer.Update(OldTriple =>
            {
                if (OldTriple != null)
                {
                    var t = OldTriple.Value;
                    if ((CommandName == t.CommandName) && (CommandHash == t.CommandHash))
                    {
                        tm = t.Timer;
                        return null;
                    }
                }
                return OldTriple;
            });
            if (tm != null)
            {
                tm.Dispose();
            }
            bc.HandleResult(CommandName, CommandHash, Parameters);
        }
        public event BinaryClientEventDelegate ClientEvent;

        void IBinarySender.Send(String CommandName, UInt32 CommandHash, Byte[] Parameters, Action<Exception> OnError)
        {
            if (ClientEvent != null)
            {
                var TimeoutSeconds = this.ResponseTimeoutSeconds;
                if (TimeoutSeconds != null)
                {
                    var tm = new Timer(o =>
                    {
                        OnError(new TimeoutException());
                    }, null, ResponseTimeoutSeconds.Value * 1000, Timeout.Infinite);
                    ResponseTimer.Update(OldTriple =>
                    {
                        if (OldTriple != null)
                        {
                            OldTriple.Value.Timer.Dispose();
                        }
                        return (CommandName, CommandHash, tm);
                    });
                }
                ClientEvent(CommandName, CommandHash, Parameters, OnError);
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
            ac.ErrorCommand += e => ac.NotifyErrorCommand(e.CommandName, e.Message);
        }

        public IApplicationClient GetApplicationClient()
        {
            return jc.GetApplicationClient();
        }

        public UInt64 Hash { get { return jc.GetApplicationClient().Hash; } }

        private LockedVariable<(String CommandName, UInt32 CommandHash, Timer Timer)?> ResponseTimer = new LockedVariable<(String CommandName, UInt32 CommandHash, Timer Timer)?>(null);
        private LockedVariable<int?> ResponseTimeoutSecondsValue = new LockedVariable<int?>(null);
        public int? ResponseTimeoutSeconds
        {
            get
            {
                return ResponseTimeoutSecondsValue.Check(v => v);
            }
            set
            {
                ResponseTimeoutSecondsValue.Update(v => value);
            }
        }

        public void HandleResult(String CommandName, UInt32 CommandHash, String Parameters)
        {
            Timer tm = null;
            ResponseTimer.Update(OldTriple =>
            {
                if (OldTriple != null)
                {
                    var t = OldTriple.Value;
                    if ((CommandName == t.CommandName) && (CommandHash == t.CommandHash))
                    {
                        tm = t.Timer;
                        return null;
                    }
                }
                return OldTriple;
            });
            if (tm != null)
            {
                tm.Dispose();
            }
            jc.HandleResult(CommandName, CommandHash, Parameters);
        }
        public event JsonClientEventDelegate ClientEvent;

        void IJsonSender.Send(String CommandName, UInt32 CommandHash, String Parameters, Action<Exception> OnError)
        {
            if (ClientEvent != null)
            {
                var TimeoutSeconds = this.ResponseTimeoutSeconds;
                if (TimeoutSeconds != null)
                {
                    var tm = new Timer(o =>
                    {
                        OnError(new TimeoutException());
                    }, null, ResponseTimeoutSeconds.Value * 1000, Timeout.Infinite);
                    ResponseTimer.Update(OldTriple =>
                    {
                        if (OldTriple != null)
                        {
                            OldTriple.Value.Timer.Dispose();
                        }
                        return (CommandName, CommandHash, tm);
                    });
                }
                ClientEvent(CommandName, CommandHash, Parameters, OnError);
            }
        }
    }
}
