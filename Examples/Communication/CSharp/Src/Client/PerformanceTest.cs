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
    class PerformanceTest
    {
        public static void TestAdd(int NumUser, int n, ClientContext cc, IClient ic, Action Completed)
        {
            ic.TestAdd(new TestAddRequest { Left = n - 1, Right = n + 1 }, r =>
            {
                Trace.Assert(r.Result == 2 * n);
                Completed();
            });
        }

        public static void TestMultiply(int NumUser, int n, ClientContext cc, IClient ic, Action Completed)
        {
            double v = n;
            var o = v * 1000001 * 0.5;

            ic.TestMultiply(new TestMultiplyRequest { Operand = n }, r =>
            {
                Trace.Assert(Math.Abs(r.Result - o) < 0.01);
                Completed();
            });
        }

        public static void TestText(int NumUser, int n, ClientContext cc, IClient ic, Action Completed)
        {
            var ss = n.ToString();
            String s = String.Join("", Enumerable.Range(0, 10000 / ss.Length).Select(i => ss).ToArray()).Substring(0, 4096 - 256);

            ic.TestText(new TestTextRequest { Text = s }, r =>
            {
                Trace.Assert(String.Equals(r.Result, s));
                Completed();
            });
        }

        public static void TestForNumUser(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, int NumRequestPerUser, int NumUser, String Title, Action<int, int, ClientContext, IClient, Action> Test)
        {
            var tll = new Object();
            var tl = new List<Task>();
            var bcl = new List<ManagedTcpClient>();
            var ccl = new List<ClientContext>();
            var vConnected = new LockedVariable<int>(0);
            var vCompleted = new LockedVariable<int>(0);
            var Check = new AutoResetEvent(false);

            var vError = new LockedVariable<int>(0);

            for (int k = 0; k < NumUser; k += 1)
            {
                Action Completed = null;

                var n = k;
                var Lockee = new Object();
                var bc = new ManagedTcpClient(RemoteEndPoint, new ClientImplementation(), ProtocolType);
                bc.Connect();
                Action<SocketError> HandleError = se =>
                {
                    int OldValue = 0;
                    vError.Update(v =>
                    {
                        OldValue = v;
                        return v + 1;
                    });
                    if (OldValue <= 10)
                    {
                        Console.WriteLine("{0}:{1}".Formats(n, (new SocketException((int)se)).Message));
                    }
                    vCompleted.Update(i => i + 1);
                    Check.Set();
                };
                bc.Receive
                (
                    a =>
                    {
                        lock (Lockee)
                        {
                            a();
                        }
                    },
                    HandleError
                );
                lock (Lockee)
                {
                    bc.InnerClient.ServerTime(new ServerTimeRequest { }, r =>
                    {
                        vConnected.Update(i => i + 1);
                        Check.Set();
                    });
                }
                Action f = () =>
                {
                    lock (Lockee)
                    {
                        Test(NumUser, n, bc.Context, bc.InnerClient, Completed);
                    }
                };
                var t = new Task(f);
                lock (tll)
                {
                    tl.Add(t);
                }
                bcl.Add(bc);
                ccl.Add(bc.Context);

                int RequestCount = NumRequestPerUser;
                Completed = () =>
                {
                    if (RequestCount > 0)
                    {
                        RequestCount -= 1;
                        var tt = new Task(f);
                        lock (tll)
                        {
                            tl.Add(t);
                        }
                        tt.Start();
                        return;
                    }
                    vCompleted.Update(i => i + 1);
                    Check.Set();
                };
            }

            while (vConnected.Check(i => i != NumUser))
            {
                Check.WaitOne();
            }

            var Time = Environment.TickCount;

            lock (tll)
            {
                foreach (var t in tl)
                {
                    t.Start();
                }
            }

            while (vCompleted.Check(i => i != NumUser))
            {
                Check.WaitOne();
            }

            var TimeDiff = Environment.TickCount - Time;

            Task.WaitAll(tl.ToArray());
            foreach (var t in tl)
            {
                t.Dispose();
            }

            foreach (var bc in bcl)
            {
                bc.Dispose();
            }

            var NumError = vError.Check(v => v);
            if (NumError > 0)
            {
                Console.WriteLine("{0}: {1} Errors", Title, NumError);
            }
            if (Title == "") { return; }
            Console.WriteLine("{0}: {1} Users, {2} Request/User, {3} ms", Title, NumUser, NumRequestPerUser, TimeDiff);
        }

        public static int DoTest(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            TestForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestAdd", TestAdd);
            TestForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestMultiply", TestMultiply);
            TestForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestText", TestText);

            Thread.Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestAdd", TestAdd);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestMultiply", TestMultiply);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 4; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestText", TestText);
            }

            return 0;
        }
    }
}
