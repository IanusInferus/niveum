using System;
using System.Collections.Generic;
using Bridge.Html5;
using Bridge.jQuery2;

namespace Client
{
    class JsonHttpClient
    {
        private IJsonSerializationClientAdapter jc;

        private String Prefix;
        private String ServiceVirtualPath;
        private Boolean UseJsonp;
        private Boolean UseShortConnection;
        private String SessionId;
        private class Command
        {
            public Object jo;
            public Action<String, UInt32, String> Callback;
        }
        private Queue<Command> CommandQueue;

        public JsonHttpClient(IJsonSerializationClientAdapter jc, String Prefix, String ServiceVirtualPath, Boolean UseJsonp, Boolean UseShortConnection)
        {
            if (!Prefix.EndsWith("/")) { throw new InvalidOperationException("PrefixNotEndWithSlash: '" + Prefix + "'"); }
            this.jc = jc;
            this.Prefix = Prefix;
            this.ServiceVirtualPath = ServiceVirtualPath;
            this.UseJsonp = UseJsonp;
            this.UseShortConnection = UseShortConnection;
            SessionId = null;
            CommandQueue = new Queue<Command>();
            jc.ClientEvent += (CommandName, CommandHash, Parameters) =>
            {
                var jo = new { commandName = CommandName, commandHash = CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), parameters = Parameters };
                SendWithHash(jo, jc.HandleResult);
            };
        }

        private String GetTimeAndRandom()
        {
            var Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            var Random = (Math.Random() * 10000).ToString("0000");
            return Time + Random;
        }
        private void SendRaw(dynamic jo, Action<String, UInt32, String> Callback)
        {
            Action<dynamic> OnSuccess = r =>
            {
                dynamic[] commands = r.commands;
                SessionId = r.sessionid;
                foreach (var c in commands)
                {
                    Callback(c.commandName, Global.ParseInt(c.commandHash, 16), c.parameters);
                }
                CommandQueue.Dequeue();
                if (CommandQueue.Count > 0)
                {
                    var Pair = CommandQueue.Peek();
                    SendRaw(Pair.jo, Pair.Callback);
                }
            };
            if (UseJsonp)
            {
                String url;
                if ((SessionId == null) || UseShortConnection)
                {
                    url = Prefix + ServiceVirtualPath + "?avoidCache=" + GetTimeAndRandom() + "&callback=?";
                }
                else
                {
                    url = Prefix + ServiceVirtualPath + "?sessionid=" + Global.EncodeURI(SessionId) + "&callback=?";
                }
                jQuery.GetJSON(url, new { data = JSON.Stringify(new[] { jo }) }, OnSuccess);
            }
            else
            {
                String url;
                if ((SessionId == null) || UseShortConnection)
                {
                    url = Prefix + ServiceVirtualPath + "?avoidCache=" + GetTimeAndRandom();
                }
                else
                {
                    url = Prefix + ServiceVirtualPath + "?sessionid=" + Global.EncodeURI(SessionId);
                }
                jQuery.Post(url, JSON.Stringify(new[] { jo }), OnSuccess);
            }
        }
        private void SendWithHash(dynamic jo, Action<String, UInt32, String> Callback)
        {
            if (CommandQueue.Count > 0)
            {
                CommandQueue.Enqueue(new Command { jo = jo, Callback = Callback });
            }
            else
            {
                CommandQueue.Enqueue(new Command { jo = jo, Callback = Callback });
                SendRaw(jo, Callback);
            }
        }

        public void Send(String CommandName, String Parameters, Action<String, String> Callback)
        {
            Action<String, UInt32, String> c = (CommandNameInner, CommandHashInner, ParametersInner) =>
            {
                Callback(CommandNameInner, ParametersInner);
            };
            SendWithHash(new { commandName = CommandName, parameters = Parameters }, c);
        }
    }
}