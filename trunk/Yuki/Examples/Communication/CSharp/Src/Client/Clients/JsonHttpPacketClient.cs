﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Client
{
    public partial class Http
    {
        public class JsonHttpPacketClient : IHttpVirtualTransportClient
        {
            private class Context
            {
                public List<JObject> WriteBuffer = new List<JObject>();
            }

            private Context c;
            private IJsonSerializationClientAdapter jc;
            public JsonHttpPacketClient(IJsonSerializationClientAdapter jc)
            {
                this.c = new Context();
                this.jc = jc;
                jc.ClientEvent += (String CommandName, UInt32 CommandHash, String Parameters) =>
                {
                    var jo = new JObject();
                    jo["commandName"] = CommandName;
                    jo["commandHash"] = CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
                    jo["parameters"] = Parameters;
                    c.WriteBuffer.Add(jo);
                    if (this.ClientMethod != null) { ClientMethod(); }
                };
            }

            public JObject[] TakeWriteBuffer()
            {
                var r = c.WriteBuffer.ToArray();
                c.WriteBuffer = new List<JObject>();
                return r;
            }

            private static String StringFromJson(JToken j)
            {
                if (j.Type != JTokenType.String) { throw new InvalidOperationException(); }
                var jv = j as JValue;
                return Convert.ToString(jv.Value);
            }
            public HttpVirtualTransportClientHandleResultCommand Handle(JObject CommandObject)
            {
                var jo = CommandObject;
                var CommandName = StringFromJson(jo["commandName"]);
                var CommandHash = UInt32.Parse(StringFromJson(jo["commandHash"]), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                var Parameters = StringFromJson(jo["parameters"]);
                var ret = new HttpVirtualTransportClientHandleResultCommand
                {
                    CommandName = CommandName,
                    HandleResult = () => jc.HandleResult(CommandName, CommandHash, Parameters)
                };

                return ret;
            }

            public UInt64 Hash { get { return jc.Hash; } }
            public event Action ClientMethod;
        }
    }
}