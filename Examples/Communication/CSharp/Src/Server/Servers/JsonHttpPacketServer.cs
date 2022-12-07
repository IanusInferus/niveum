using System;
using System.Collections.Generic;
using System.Linq;
using Niveum.Json;

namespace Server
{
    public class JsonHttpPacketServer : IHttpVirtualTransportServer
    {
        public class Context
        {
            public Object WriteBufferLockee = new Object();
            public List<JObject> WriteBuffer = new List<JObject>();
        }

        public delegate Boolean CheckCommandAllowedDelegate(String CommandName);

        private IJsonSerializationServerAdapter ss;
        private Context c;
        private CheckCommandAllowedDelegate CheckCommandAllowed;
        public JsonHttpPacketServer(IJsonSerializationServerAdapter SerializationServerAdapter, CheckCommandAllowedDelegate CheckCommandAllowed)
        {
            this.ss = SerializationServerAdapter;
            this.c = new Context();
            this.CheckCommandAllowed = CheckCommandAllowed;
            this.ss.ServerEvent += (CommandName, CommandHash, Parameters) =>
            {
                var rjo = new JObject();
                rjo["commandName"] = new JValue(CommandName);
                rjo["commandHash"] = new JValue(CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
                rjo["parameters"] = new JValue(Parameters);
                lock (c.WriteBufferLockee)
                {
                    c.WriteBuffer.Add(rjo);
                }
                if (this.ServerEvent != null)
                {
                    this.ServerEvent();
                }
            };
        }

        public JObject[] TakeWriteBuffer()
        {
            lock (c.WriteBufferLockee)
            {
                var r = c.WriteBuffer.ToArray();
                c.WriteBuffer = new List<JObject>();
                return r;
            }
        }

        private static String StringFromJson(JToken j)
        {
            if (j.Type != JTokenType.String) { throw new InvalidOperationException(); }
            var jv = j as JValue;
            return Convert.ToString(jv.Value);
        }
        public HttpVirtualTransportServerHandleResult Handle(JObject CommandObject)
        {
            var jo = CommandObject;
            if (!jo.ContainsKey("commandName") || jo["commandName"].Type != JTokenType.String || (jo.ContainsKey("commandHash") && jo["commandHash"].Type != JTokenType.String) || !jo.ContainsKey("parameters") || jo["parameters"].Type != JTokenType.String)
            {
                return HttpVirtualTransportServerHandleResult.CreateBadCommandLine(new HttpVirtualTransportServerHandleResultBadCommandLine { CommandLine = jo.ToString(Formatting.None) });
            }
            var CommandName = StringFromJson(jo["commandName"]);
            var CommandHash = Optional<UInt32>.Empty;
            if (jo.ContainsKey("commandHash") && (jo["commandHash"] != null))
            {
                UInt32 ch;
                if (!UInt32.TryParse(StringFromJson(jo["commandHash"]), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ch))
                {
                    return HttpVirtualTransportServerHandleResult.CreateBadCommandLine(new HttpVirtualTransportServerHandleResultBadCommandLine { CommandLine = jo.ToString(Formatting.None) });
                }
                CommandHash = ch;
            }
            var Parameters = StringFromJson(jo["parameters"]);

            HttpVirtualTransportServerHandleResult ret;
            if ((CommandHash.OnSome ? ss.HasCommand(CommandName, CommandHash.Some) : ss.HasCommand(CommandName)) && (CheckCommandAllowed != null ? CheckCommandAllowed(CommandName) : true))
            {
                if (CommandHash.OnSome)
                {
                    ret = HttpVirtualTransportServerHandleResult.CreateCommand(new HttpVirtualTransportServerHandleResultCommand
                    {
                        CommandName = CommandName,
                        ExecuteCommand = (OnSuccess, OnFailure) =>
                        {
                            Action<String> OnSuccessInner = OutputParameters =>
                            {
                                var rjo = new JObject();
                                rjo["commandName"] = new JValue(CommandName);
                                rjo["commandHash"] = new JValue(CommandHash.Some.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
                                rjo["parameters"] = new JValue(OutputParameters);
                                lock (c.WriteBufferLockee)
                                {
                                    c.WriteBuffer.Add(rjo);
                                }
                                OnSuccess();
                            };
                            ss.ExecuteCommand(CommandName, CommandHash.Some, Parameters, OnSuccessInner, OnFailure);
                        }
                    });
                }
                else
                {
                    ret = HttpVirtualTransportServerHandleResult.CreateCommand(new HttpVirtualTransportServerHandleResultCommand
                    {
                        CommandName = CommandName,
                        ExecuteCommand = (OnSuccess, OnFailure) =>
                        {
                            Action<String> OnSuccessInner = OutputParameters =>
                            {
                                var rjo = new JObject();
                                rjo["commandName"] = new JValue(CommandName);
                                rjo["parameters"] = new JValue(OutputParameters);
                                lock (c.WriteBufferLockee)
                                {
                                    c.WriteBuffer.Add(rjo);
                                }
                                OnSuccess();
                            };
                            ss.ExecuteCommand(CommandName, Parameters, OnSuccessInner, OnFailure);
                        }
                    });
                }
            }
            else
            {
                ret = HttpVirtualTransportServerHandleResult.CreateBadCommand(new HttpVirtualTransportServerHandleResultBadCommand { CommandName = CommandName });
            }
            return ret;
        }

        public UInt64 Hash { get { return ss.Hash; } }
        public event Action ServerEvent;
    }
}
