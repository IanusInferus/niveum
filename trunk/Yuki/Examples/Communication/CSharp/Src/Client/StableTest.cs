using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Firefly.Streaming;
using Communication;
using Communication.BaseSystem;
using Communication.Binary;
using Communication.Json;

namespace Client
{
    class StableTest
    {
        public static void TestTcpForNumUser(IPEndPoint RemoteEndPoint, int NumUser, String Title)
        {
            var Bytes = new Byte[] { 0x00, 0x01, 0x02, 0x03 };

            for (int k = 0; k < NumUser; k += 1)
            {
                var s = new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                s.Connect(RemoteEndPoint);
                s.Send(Bytes);
                s.Dispose();
            }

            if (Title == "") { return; }
            Console.WriteLine("{0}: {1} Connections", Title, NumUser);
        }

        public static void TestHttpForNumUser(String UrlPrefix, String ServiceVirtualPath, int NumUser, String Title)
        {
            var Bytes = new Byte[] { 0x00, 0x01, 0x02, 0x03 };

            for (int k = 0; k < NumUser; k += 1)
            {
                var req = (HttpWebRequest)(WebRequest.Create(UrlPrefix + ServiceVirtualPath));
                try
                {
                    req.Method = "POST";
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = 4;
                    req.MediaType = "application/json";
                    using (var OutputStream = req.GetRequestStream().AsWritable())
                    {
                        OutputStream.Write(Bytes);
                    }
                }
                catch
                {
                }
                req.Abort();
            }

            if (Title == "") { return; }
            Console.WriteLine("{0}: {1} Connections", Title, NumUser);
        }

        public static int DoTestTcp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            TestTcpForNumUser(RemoteEndPoint, 4096, "TestHalfConnection");

            return 0;
        }

        public static int DoTestHttp(String UrlPrefix, String ServiceVirtualPath)
        {
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 4096, "TestHalfConnection");

            return 0;
        }
    }
}
