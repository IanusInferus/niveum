#nullable disable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Client
{
    public class JsonHttpClient
    {
        private IJsonSerializationClientAdapter jc;

        private String Prefix;
        private String ServiceVirtualPath;
        private Boolean UseShortConnection;
        private HttpClient HttpClient;
        private String SessionId;
        private class Command
        {
            public Object jo;
            public Action<String, UInt32, String> Callback;
            public Action<Exception> OnError;
        }
        private Queue<Command> CommandQueue;

        public JsonHttpClient(IJsonSerializationClientAdapter jc, String Prefix, String ServiceVirtualPath, Boolean UseShortConnection, HttpClient HttpClient)
        {
            if (!Prefix.EndsWith("/")) { throw new InvalidOperationException("PrefixNotEndWithSlash: '" + Prefix + "'"); }
            this.jc = jc;
            this.Prefix = Prefix;
            this.ServiceVirtualPath = ServiceVirtualPath;
            this.UseShortConnection = UseShortConnection;
            this.HttpClient = HttpClient;
            SessionId = null;
            CommandQueue = new Queue<Command>();
            jc.ClientEvent += (CommandName, CommandHash, Parameters, OnError) =>
            {
                var jo = new { commandName = CommandName, commandHash = CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), parameters = Parameters };
                SendWithHash(jo, jc.HandleResult, OnError);
            };
        }

        private static Random random = new Random();
        private String GetTimeAndRandom()
        {
            var Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            var Random = random.Next(10000).ToString("0000");
            return Time + Random;
        }
        private void SendRaw<T>(T jo, Action<String, UInt32, String> Callback, Action<Exception> OnError)
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
                    var Command = CommandQueue.Peek();
                    SendRaw(Command.jo, Command.Callback, Command.OnError);
                }
            };
            String url;
            if ((SessionId == null) || UseShortConnection)
            {
                url = Prefix + ServiceVirtualPath + "?avoidCache=" + GetTimeAndRandom();
            }
            else
            {
                url = Prefix + ServiceVirtualPath + "?sessionid=" + WebUtility.UrlEncode(SessionId);
            }
            var Request = new HttpRequestMessage(HttpMethod.Post, url);
            var Text = JsonSerializer.Serialize(new[] { jo });
            Request.Content = new StringContent(Text);
            HttpClient.SendAsync(Request).ContinueWith(async t =>
            {
                try
                {
                    var Result = await t.Result.Content.ReadAsStringAsync();
                    Console.WriteLine("Result: " + Result);
                    var r = JsonSerializer.Deserialize<ResponsePacket>(Result);
                    Console.WriteLine("r: " + JsonSerializer.Serialize(r));
                    OnSuccess(JsonSerializer.Deserialize<ResponsePacket>(Result));
                }
                catch (Exception ex)
                {
                    if (OnError != null)
                    {
                        OnError(ex);
                    }
                }
            });
        }
        private void SendWithHash<T>(T jo, Action<String, UInt32, String> Callback, Action<Exception> OnError)
        {
            if (CommandQueue.Count > 0)
            {
                CommandQueue.Enqueue(new Command { jo = jo, Callback = Callback, OnError = OnError });
            }
            else
            {
                CommandQueue.Enqueue(new Command { jo = jo, Callback = Callback, OnError = OnError });
                SendRaw(jo, Callback, OnError);
            }
        }

        public void Send(String CommandName, String Parameters, Action<String, String> Callback, Action<Exception> OnError)
        {
            Action<String, UInt32, String> c = (CommandNameInner, CommandHashInner, ParametersInner) =>
            {
                Callback(CommandNameInner, ParametersInner);
            };
            SendWithHash(new { commandName = CommandName, parameters = Parameters }, c, OnError);
        }

        private class ResponsePacket
        {
            public CommandPacket[] commands { get; set; }
            public String sessionid { get; set; }
        }
        private class CommandPacket
        {
            public String commandName { get; set; }
            public String commandHash { get; set; }
            public String parameters { get; set; }
        }
    }
}