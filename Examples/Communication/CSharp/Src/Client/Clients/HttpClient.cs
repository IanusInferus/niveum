using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using Newtonsoft.Json.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Binary;

namespace Client
{
    public class HttpVirtualTransportClientHandleResultCommand
    {
        public String CommandName;
        public Action HandleResult;
    }

    public interface IHttpVirtualTransportClient
    {
        IApplicationClient ApplicationClient { get; }

        JObject[] TakeWriteBuffer();
        HttpVirtualTransportClientHandleResultCommand Handle(JObject CommandObject);
        UInt64 Hash { get; }
        event Action ClientMethod;
    }

    public sealed class HttpClient : IDisposable
    {
        public IApplicationClient InnerClient { get { return VirtualTransportClient.ApplicationClient; } }
        public IHttpVirtualTransportClient VirtualTransportClient { get; private set; }

        private String SessionId;

        public HttpClient(String Prefix, String ServiceVirtualPath)
        {
            if (!Prefix.EndsWith("/")) { throw new InvalidOperationException("PrefixNotEndWithSlash: '{0}'".Formats(Prefix)); }
            VirtualTransportClient = new JsonHttpPacketClient();
            InnerClient.Error += e => InnerClient.DequeueCallback(e.CommandName);
            VirtualTransportClient.ClientMethod += () =>
            {
                var Bytes = TextEncoding.UTF8.GetBytes((new JArray(VirtualTransportClient.TakeWriteBuffer())).ToString(Newtonsoft.Json.Formatting.None));

                Uri Uri;
                if (SessionId == null)
                {
                    Uri = new Uri(Prefix + ServiceVirtualPath);
                }
                else
                {
                    Uri = new Uri(Prefix + ServiceVirtualPath + "?sessionid=" + Uri.EscapeDataString(SessionId));
                }

                var req = (HttpWebRequest)(WebRequest.Create(Uri));
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.ContentLength = Bytes.Length;
                req.MediaType = "application/json";

                using (var OutputStream = req.GetRequestStream().AsWritable())
                {
                    OutputStream.Write(Bytes);
                }

                String ResultString;
                using (var resp = (HttpWebResponse)(req.GetResponse()))
                {
                    var Length = (int)(resp.ContentLength);
                    Encoding e;
                    if (resp.CharacterSet == "")
                    {
                        e = TextEncoding.UTF8;
                    }
                    else
                    {
                        e = Encoding.GetEncoding(resp.CharacterSet);
                    }
                    using (var InputStream = resp.GetResponseStream().AsReadable())
                    {
                        var ResultBytes = InputStream.Read(Length);
                        ResultString = e.GetString(ResultBytes);
                    }

                    resp.Close();
                }

                var Result = (JObject)(JToken.Parse(ResultString));
                var Commands = (JArray)(Result["commands"]);
                SessionId = (String)(Result["sessionid"]);

                foreach (var co in Commands)
                {
                    var r = VirtualTransportClient.Handle((JObject)(co));
                    r.HandleResult();
                }
            };
        }

        private Boolean IsDisposed = false;
        public void Dispose()
        {
            if (IsDisposed) { return; }
            IsDisposed = true;
        }
    }
}
