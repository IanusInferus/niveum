using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Communication;
using Communication.Json;

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

        private IApplicationServer s;
        private static ThreadLocal<JsonSerializationServer> sss = new ThreadLocal<JsonSerializationServer>(() => new JsonSerializationServer());
        private JsonSerializationServer ss = sss.Value;
        private JsonSerializationServerEventDispatcher ssed;
        private Context c;
        private CheckCommandAllowedDelegate CheckCommandAllowed;
        public JsonHttpPacketServer(IApplicationServer ApplicationServer, CheckCommandAllowedDelegate CheckCommandAllowed)
        {
            this.s = ApplicationServer;
            this.ssed = new JsonSerializationServerEventDispatcher(ApplicationServer);
            this.c = new Context();
            this.CheckCommandAllowed = CheckCommandAllowed;
            this.ssed.ServerEvent += (CommandName, CommandHash, Parameters) =>
            {
                var rjo = new JObject();
                rjo["commandName"] = CommandName;
                rjo["commandHash"] = CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
                rjo["parameters"] = Parameters;
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
            if (jo["commandName"] == null || jo["commandName"].Type != JTokenType.String || (jo["commandHash"] != null && jo["commandHash"].Type != JTokenType.String) || jo["parameters"] == null || jo["parameters"].Type != JTokenType.String)
            {
                return HttpVirtualTransportServerHandleResult.CreateBadCommandLine(new HttpVirtualTransportServerHandleResultBadCommandLine { CommandLine = jo.ToString(Newtonsoft.Json.Formatting.None) });
            }
            var CommandName = StringFromJson(jo["commandName"]);
            var CommandHash = Optional<UInt32>.Empty;
            if (jo["commandHash"] != null)
            {
                UInt32 ch;
                if (!UInt32.TryParse(StringFromJson(jo["commandHash"]), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ch))
                {
                    return HttpVirtualTransportServerHandleResult.CreateBadCommandLine(new HttpVirtualTransportServerHandleResultBadCommandLine { CommandLine = jo.ToString(Newtonsoft.Json.Formatting.None) });
                }
                CommandHash = ch;
            }
            var Parameters = StringFromJson(jo["parameters"]);

            HttpVirtualTransportServerHandleResult ret;
            if ((CommandHash.OnHasValue ? ss.HasCommand(CommandName, CommandHash.HasValue) : ss.HasCommand(CommandName)) && (CheckCommandAllowed != null ? CheckCommandAllowed(CommandName) : true))
            {
                if (CommandHash.OnHasValue)
                {
                    ret = HttpVirtualTransportServerHandleResult.CreateCommand(new HttpVirtualTransportServerHandleResultCommand
                    {
                        CommandName = CommandName,
                        ExecuteCommand = () =>
                        {
                            var rjo = new JObject();
                            rjo["commandName"] = CommandName;
                            rjo["commandHash"] = CommandHash.HasValue.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
                            rjo["parameters"] = ss.ExecuteCommand(s, CommandName, CommandHash.HasValue, Parameters);
                            lock (c.WriteBufferLockee)
                            {
                                c.WriteBuffer.Add(rjo);
                            }
                        }
                    });
                }
                else
                {
                    ret = HttpVirtualTransportServerHandleResult.CreateCommand(new HttpVirtualTransportServerHandleResultCommand
                    {
                        CommandName = CommandName,
                        ExecuteCommand = () =>
                        {
                            var rjo = new JObject();
                            rjo["commandName"] = CommandName;
                            rjo["parameters"] = ss.ExecuteCommand(s, CommandName, CommandHash.HasValue, Parameters);
                            lock (c.WriteBufferLockee)
                            {
                                c.WriteBuffer.Add(rjo);
                            }
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
