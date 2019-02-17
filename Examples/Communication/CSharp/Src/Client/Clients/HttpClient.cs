using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Niveum.Json;

namespace Client
{
    public partial class Http
    {
        public class HttpVirtualTransportClientHandleResultCommand
        {
            public String CommandName;
            public Action HandleResult;
        }

        public interface IHttpVirtualTransportClient
        {
            JObject[] TakeWriteBuffer();
            HttpVirtualTransportClientHandleResultCommand Handle(JObject CommandObject);
            UInt64 Hash { get; }
            event Action<Action<Exception>> ClientMethod;
        }

        public sealed class HttpClient : IDisposable
        {
            public IHttpVirtualTransportClient VirtualTransportClient { get; private set; }

            private String SessionId;

            public HttpClient(String Prefix, String ServiceVirtualPath, IHttpVirtualTransportClient VirtualTransportClient)
            {
                if (!Prefix.EndsWith("/")) { throw new InvalidOperationException(String.Format("PrefixNotEndWithSlash: '{0}'", Prefix)); }
                this.VirtualTransportClient = VirtualTransportClient;
                VirtualTransportClient.ClientMethod += OnError =>
                {
                    Func<Task> t = async () =>
                    {
                        var Bytes = System.Text.Encoding.UTF8.GetBytes((new JArray(VirtualTransportClient.TakeWriteBuffer())).ToString(Formatting.None));

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

                        using (var OutputStream = await req.GetRequestStreamAsync())
                        {
                            await OutputStream.WriteAsync(Bytes, 0, Bytes.Length);
                        }

                        String ResultString;
                        using (var resp = (HttpWebResponse)(await req.GetResponseAsync()))
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
                        }

                        var Result = (JObject)(JToken.Parse(ResultString));
                        var Commands = (JArray)(Result["commands"]);
                        SessionId = (String)((Result["sessionid"] as JValue).Value);

                        foreach (var co in Commands)
                        {
                            var r = VirtualTransportClient.Handle((JObject)(co));
                            r.HandleResult();
                        }
                    };
                    t().ContinueWith(tt => OnError(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
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
}
