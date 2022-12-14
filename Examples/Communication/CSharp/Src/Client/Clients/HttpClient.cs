using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Niveum.Json;
using System.Net.Http;

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
            private System.Net.Http.HttpClient c;
            public IHttpVirtualTransportClient VirtualTransportClient { get; private set; }

            private String SessionId;

            public HttpClient(String Prefix, String ServiceVirtualPath, IHttpVirtualTransportClient VirtualTransportClient)
            {
                if (!Prefix.EndsWith("/")) { throw new InvalidOperationException(String.Format("PrefixNotEndWithSlash: '{0}'", Prefix)); }
                this.c = new System.Net.Http.HttpClient();
                this.VirtualTransportClient = VirtualTransportClient;
                VirtualTransportClient.ClientMethod += OnError =>
                {
                    Func<Task> t = async () =>
                    {
                        var s = (new JArray(VirtualTransportClient.TakeWriteBuffer())).ToString(Formatting.None);

                        Uri u;
                        if (SessionId == null)
                        {
                            u = new Uri(Prefix + ServiceVirtualPath);
                        }
                        else
                        {
                            u = new Uri(Prefix + ServiceVirtualPath + "?sessionid=" + Uri.EscapeDataString(SessionId));
                        }

                        String ResultString;
                        using (var req = new HttpRequestMessage(HttpMethod.Post, u))
                        {
                            req.Content = new StringContent(s, System.Text.Encoding.UTF8, "application/json");
                            req.Headers.Add("Accept-Charset", "utf-8");

                            using (var resp = await c.SendAsync(req))
                            {
                                ResultString = await resp.Content.ReadAsStringAsync();
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
                c.Dispose();
            }
        }
    }
}
