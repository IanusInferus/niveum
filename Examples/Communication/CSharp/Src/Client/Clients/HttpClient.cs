using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using Newtonsoft.Json.Linq;
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
            if (!Prefix.EndsWith("/")) { throw new InvalidOperationException(String.Format("PrefixNotEndWithSlash: '{0}'", Prefix)); }
            VirtualTransportClient = new JsonHttpPacketClient();
            InnerClient.Error += e => InnerClient.DequeueCallback(e.CommandName);
            VirtualTransportClient.ClientMethod += () =>
            {
                var Bytes = System.Text.Encoding.UTF8.GetBytes((new JArray(VirtualTransportClient.TakeWriteBuffer())).ToString(Newtonsoft.Json.Formatting.None));

                Uri u;
                if (SessionId == null)
                {
                    u = new Uri(Prefix + ServiceVirtualPath);
                }
                else
                {
                    u = new Uri(Prefix + ServiceVirtualPath + "?sessionid=" + Uri.EscapeDataString(SessionId));
                }

                var req = (HttpWebRequest)(WebRequest.Create(u));
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.ContentLength = Bytes.Length;
                req.MediaType = "application/json";
                req.Headers.Add("Accept-Charset", "utf-8");

                using (var OutputStream = req.GetRequestStream())
                {
                    OutputStream.Write(Bytes, 0, Bytes.Length);
                }

                String ResultString;
                using (var resp = (HttpWebResponse)(req.GetResponse()))
                {
                    var Length = (int)(resp.ContentLength);
                    Encoding e;
                    if (resp.CharacterSet == "")
                    {
                        e = System.Text.Encoding.UTF8;
                    }
                    else
                    {
                        e = Encoding.GetEncoding(resp.CharacterSet);
                    }
                    using (var InputStream = resp.GetResponseStream())
                    {
                        var ResultBytes = Read(InputStream, Length);
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

        private static Byte[] Read(System.IO.Stream s, int Count)
        {
            var Buffer = new Byte[Count];
            var c = 0;
            while (c < Count)
            {
                var k = s.Read(Buffer, c, Count - c);
                if (k < 0) { throw new System.IO.EndOfStreamException(); }
                if (k == 0) { break; }
                c += k;
            }
            if (c != Count) { throw new System.IO.EndOfStreamException(); }
            return Buffer;
        }

        private Boolean IsDisposed = false;
        public void Dispose()
        {
            if (IsDisposed) { return; }
            IsDisposed = true;
        }
    }
}
