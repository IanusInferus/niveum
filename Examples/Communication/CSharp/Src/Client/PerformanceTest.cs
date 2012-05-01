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
        public static void TestAdd(int NumUser, int n, ClientContext cc, IClient<ClientContext> ic, Action Completed)
        {
            ic.TestAdd(new TestAddRequest { Left = n - 1, Right = n + 1 }, (c, r) =>
            {
                Debug.Assert(r.Result == 2 * n);
                Completed();
            });
        }

        public static void TestMultiply(int NumUser, int n, ClientContext cc, IClient<ClientContext> ic, Action Completed)
        {
            double v = n;
            var o = v * 1000001 * 0.5;

            ic.TestMultiply(new TestMultiplyRequest { Operand = n }, (c, r) =>
            {
                Debug.Assert(Math.Abs(r.Result - o) < 0.01);
                Completed();
            });
        }

        public static void TestText(int NumUser, int n, ClientContext cc, IClient<ClientContext> ic, Action Completed)
        {
            var ss = n.ToString();
            String s = String.Join("", Enumerable.Range(0, 10000 / ss.Length).Select(i => ss).ToArray()).Substring(0, 4096 - 256);

            ic.TestText(new TestTextRequest { Text = s }, (c, r) =>
            {
                Debug.Assert(String.Equals(r.Result, s));
                Completed();
            });
        }

        public static void TestMessage(int NumUser, int n, ClientContext cc, IClient<ClientContext> ic, Action Completed)
        {
            var s = n.ToString();
            cc.NumOnline = NumUser;
            cc.Num = NumUser;
            cc.Completed = Completed;

            ic.TestMessage(new TestMessageRequest { Message = s }, (c, r) =>
            {
                Debug.Assert(r.Success == c.NumOnline);
                c.Num -= 1;
                if (c.Num == 0)
                {
                    Completed();
                }
            });
        }
        public static void TestMessageFinalCheck(ClientContext[] ccl)
        {
            var NumUser = ccl.Length;
            var PredicatedSum = (Int64)NumUser * (Int64)(NumUser - 1) * (Int64)(NumUser - 1) / (Int64)2;
            var Sum = ccl.Select(cc => cc.Sum).Sum();
            Debug.Assert(Sum == PredicatedSum);
        }

        public static void TestForNumUser(IPEndPoint RemoteEndPoint, ApplicationProtocolType ProtocolType, int NumUser, String Title, Action<int, int, ClientContext, IClient<ClientContext>, Action> Test, Action<ClientContext[]> FinalCheck = null)
        {
            var tl = new List<Task>();
            var bcl = new List<BinarySocketClient>();
            var jcl = new List<JsonSocketClient>();
            var ccl = new List<ClientContext>();
            var vConnected = new LockedVariable<int>(0);
            var vCompleted = new LockedVariable<int>(0);
            var Check = new AutoResetEvent(false);
            if (ProtocolType == ApplicationProtocolType.Binary)
            {
                for (int k = 0; k < NumUser; k += 1)
                {
                    var n = k;
                    var bc = new BinarySocketClient(RemoteEndPoint, new ClientImplementation());
                    bc.Connect();
                    bc.Receive(se => Console.WriteLine((new SocketException((int)se)).Message));
                    bc.InnerClient.ServerTime(new ServerTimeRequest { }, (c, r) =>
                    {
                        vConnected.Update(i => i + 1);
                        Check.Set();
                    });
                    var t = new Task
                    (
                        () =>
                        {
                            Test(NumUser, n, bc.Context, bc.InnerClient, () =>
                            {
                                vCompleted.Update(i => i + 1);
                                Check.Set();
                            });
                        }
                    );
                    tl.Add(t);
                    bcl.Add(bc);
                    ccl.Add(bc.Context);
                }
            }
            else if (ProtocolType == ApplicationProtocolType.Json)
            {
                for (int k = 0; k < NumUser; k += 1)
                {
                    var n = k;
                    var jc = new JsonSocketClient(RemoteEndPoint, new ClientImplementation());
                    jc.Connect();
                    jc.Receive(se => Console.WriteLine((new SocketException((int)se)).Message));
                    jc.InnerClient.ServerTime(new ServerTimeRequest { }, (c, r) =>
                    {
                        vConnected.Update(i => i + 1);
                        Check.Set();
                    });
                    var t = new Task
                    (
                        () =>
                        {
                            Test(NumUser, n, jc.Context, jc.InnerClient, () =>
                            {
                                vCompleted.Update(i => i + 1);
                                Check.Set();
                            });
                        }
                    );
                    tl.Add(t);
                    jcl.Add(jc);
                    ccl.Add(jc.Context);
                }
            }
            else
            {
                throw new InvalidOperationException();
            }

            while (vConnected.Check(i => i != NumUser))
            {
                Check.WaitOne();
            }

            var Time = Environment.TickCount;

            foreach (var t in tl)
            {
                t.Start();
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
                bc.Close();
                bc.Dispose();
            }
            foreach (var jc in jcl)
            {
                jc.Close();
                jc.Dispose();
            }

            if (FinalCheck != null)
            {
                FinalCheck(ccl.ToArray());
            }

            if (Title == "") { return; }
            Console.WriteLine("{0}: {1} Users, {2} ms", Title, NumUser, TimeDiff);
        }

        public static int DoTest(IPEndPoint RemoteEndPoint, ApplicationProtocolType ProtocolType)
        {
            //TestForNumUser(RemoteEndPoint, ProtocolType, 64, "", TestAdd);
            //TestForNumUser(RemoteEndPoint, ProtocolType, 64, "", TestMultiply);
            //TestForNumUser(RemoteEndPoint, ProtocolType, 64, "", TestText);
            TestForNumUser(RemoteEndPoint, ProtocolType, 64, "", TestMessage, TestMessageFinalCheck);

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestAdd", TestAdd);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestMultiply", TestMultiply);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 8; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestText", TestText);
            }

            Thread.Sleep(5000);
            for (int k = 0; k < 6; k += 1)
            {
                TestForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestMessage", TestMessage, TestMessageFinalCheck);
            }

            return 0;
        }
    }
}
