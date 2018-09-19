using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Blazor;

namespace Client
{
    public class JsonHttpClient
    {
        private IJsonSerializationClientAdapter jc;

        private String Prefix;
        private String ServiceVirtualPath;
        private Boolean UseJsonp;
        private Boolean UseShortConnection;
        private HttpClient HttpClient;
        private Action<String> OnError;
        private String SessionId;
        private class Command
        {
            public Object jo;
            public Action<String, UInt32, String> Callback;
        }
        private Queue<Command> CommandQueue;

        public JsonHttpClient(IJsonSerializationClientAdapter jc, String Prefix, String ServiceVirtualPath, Boolean UseJsonp, Boolean UseShortConnection, HttpClient HttpClient, Action<String> OnError)
        {
            if (!Prefix.EndsWith("/")) { throw new InvalidOperationException("PrefixNotEndWithSlash: '" + Prefix + "'"); }
            this.jc = jc;
            this.Prefix = Prefix;
            this.ServiceVirtualPath = ServiceVirtualPath;
            this.UseJsonp = UseJsonp;
            this.UseShortConnection = UseShortConnection;
            this.HttpClient = HttpClient;
            this.OnError = OnError;
            SessionId = null;
            CommandQueue = new Queue<Command>();
            jc.ClientEvent += (CommandName, CommandHash, Parameters) =>
            {
                var jo = new { commandName = CommandName, commandHash = CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), parameters = Parameters };
                SendWithHash(jo, jc.HandleResult);
            };
        }

        private static Random random = new Random();
        private String GetTimeAndRandom()
        {
            var Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            var Random = random.Next(10000).ToString("0000");
            return Time + Random;
        }
        private void SendRaw<T>(T jo, Action<String, UInt32, String> Callback)
        {
            Action<ResponsePacket> OnSuccess = r =>
            {
                var commands = r.commands;
                SessionId = r.sessionid;
                foreach (var c in commands)
                {
                    Callback(c.commandName, uint.Parse(c.commandHash, System.Globalization.NumberStyles.HexNumber), c.parameters);
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
                    url = Prefix + ServiceVirtualPath + "?sessionid=" + WebUtility.UrlEncode(SessionId) + "&callback=?";
                }
                var o = new
                {
                    data = new[] { jo }
                };
                HttpClient.SendJsonAsync<ResponsePacket>(HttpMethod.Get, url, o).ContinueWith(t =>
                {
                    try
                    {
                        OnSuccess(t.Result);
                    }
                    catch(Exception ex)
                    {
                        if (OnError != null)
                        {
                            OnError(ex.ToString());
                        }
                    }
                });
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
                    url = Prefix + ServiceVirtualPath + "?sessionid=" + WebUtility.UrlEncode(SessionId);
                }
                HttpClient.PostJsonAsync<ResponsePacket>(url, new[] { jo }).ContinueWith(t =>
                {
                    try
                    {
                        OnSuccess(t.Result);
                    }
                    catch (Exception ex)
                    {
                        if (OnError != null)
                        {
                            OnError(ex.ToString());
                        }
                    }
                });
            }
        }
        private void SendWithHash<T>(T jo, Action<String, UInt32, String> Callback)
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

        private class ResponsePacket
        {
            public CommandPacket[] commands = null;
            public String sessionid = null;
        }
        private class CommandPacket
        {
            public String commandName = null;
            public String commandHash = null;
            public String parameters = null;
        }
    }
}