using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Communication;
using Communication.BaseSystem;
using Communication.Binary;
using Communication.Json;

namespace Client
{
    class StableTest
    {
        public static void TestForNumUser(IPEndPoint RemoteEndPoint, int NumUser, String Title)
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

        public static int DoTest(IPEndPoint RemoteEndPoint, ApplicationProtocolType ProtocolType)
        {
            TestForNumUser(RemoteEndPoint, 4096, "TestHalfConnection");

            return 0;
        }
    }
}
