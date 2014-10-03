using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Communication;
using BaseSystem;
using Communication.Binary;
using Communication.Json;

namespace Client
{
    class LoadTest
    {
        private static Action<Action> QueueUserWorkItem = a => ThreadPool.QueueUserWorkItem(o => a());

        public class ClientContext
        {
            public Object Lockee = new Object();

            public int NumOnline;
            public int Num;

            public Int64 Sum = 0;
        }

        public static void TestQuit(int NumUser, int n, ClientContext cc, IApplicationClient ic, Action Completed)
        {
            ic.Quit(new QuitRequest { }, r =>
            {
                Completed();
            });
        }

        public static void TestAdd(int NumUser, int n, ClientContext cc, IApplicationClient ic, Action Completed)
        {
            ic.TestAdd(new TestAddRequest { Left = n - 1, Right = n + 1 }, r =>
            {
                Trace.Assert(r.Result == 2 * n);
                Completed();
            });
        }

        public static void TestMultiply(int NumUser, int n, ClientContext cc, IApplicationClient ic, Action Completed)
        {
            double v = n;
            var o = v * 1000001 * 0.5;

            ic.TestMultiply(new TestMultiplyRequest { Operand = n }, r =>
            {
                Trace.Assert(Math.Abs(r.Result - o) < 0.01);
                Completed();
            });
        }

        public static void TestText(int NumUser, int n, ClientContext cc, IApplicationClient ic, Action Completed)
        {
            var ss = n.ToString();
            String s = String.Join("", Enumerable.Range(0, 10000 / ss.Length).Select(i => ss).ToArray()).Substring(0, 4096 - 256);

            ic.TestText(new TestTextRequest { Text = s }, r =>
            {
                Trace.Assert(String.Equals(r.Result, s));
                Completed();
            });
        }

        public static void TestMessageInitializeClientContext(int NumUser, int n, ClientContext cc, IApplicationClient ic, Action Completed)
        {
            ic.TestMessageReceived += e =>
            {
                var Done = false;
                lock (cc.Lockee)
                {
                    cc.Sum += Int32.Parse(e.Message);
                    cc.Num -= 1;
                    Done = cc.Num == 0;
                }
                if (Done)
                {
                    Completed();
                }
            };
            cc.NumOnline = NumUser;
            cc.Num = NumUser;
        }
        public static void TestMessage(int NumUser, int n, ClientContext cc, IApplicationClient ic, Action Completed)
        {
            var s = n.ToString();

            ic.TestMessage(new TestMessageRequest { Message = s }, r =>
            {
                //Trace.Assert(r.Success == cc.NumOnline);
                cc.Num -= 1;
                if (cc.Num == 0)
                {
                    Completed();
                }
            });
        }
        public static void TestMessageFinalCheck(ClientContext[] ccl)
        {
            if (ccl.All(cc => cc.Num == 0))
            {
                var NumUser = ccl.Length;
                var PredicatedSum = (Int64)NumUser * (Int64)(NumUser - 1) * (Int64)(NumUser - 1) / (Int64)2;
                var Sum = ccl.Select(cc => cc.Sum).Sum();
                Trace.Assert(Sum == PredicatedSum);
            }
        }

        public static void TestTcpForNumUser(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, int NumUser, String Title, Action<int, int, ClientContext, IApplicationClient, Action> Test, Action<int, int, ClientContext, IApplicationClient, Action> InitializeClientContext = null, Action<ClientContext[]> FinalCheck = null)
        {
            Console.Write("{0}: ", Title);
            Console.Out.Flush();

            var tl = new List<Task>();
            var bcl = new List<Streamed.TcpClient>();
            var ccl = new List<ClientContext>();
            var tmrl = new List<Timer>();
            var vConnected = new LockedVariable<int>(0);
            var vCompleted = new LockedVariable<int>(0);
            var Check = new AutoResetEvent(false);

            var bAbondon = new LockedVariable<Boolean>(false);

            var vError = new LockedVariable<int>(0);

            for (int k = 0; k < NumUser; k += 1)
            {
                var n = k;
                var Lockee = new Object();
                IApplicationClient ac;
                Streamed.IStreamedVirtualTransportClient vtc;
                if (ProtocolType == SerializationProtocolType.Binary)
                {
                    var a = new BinarySerializationClientAdapter();
                    ac = a.GetApplicationClient();
                    vtc = new Streamed.BinaryCountPacketClient(a);
                }
                else if (ProtocolType == SerializationProtocolType.Json)
                {
                    var a = new JsonSerializationClientAdapter();
                    ac = a.GetApplicationClient();
                    vtc = new Streamed.JsonLinePacketClient(a);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var bc = new Streamed.TcpClient(RemoteEndPoint, vtc, QueueUserWorkItem);
                var cc = new ClientContext();
                var bCompleted = new LockedVariable<Boolean>(false);
                Action Completed = () =>
                {
                    bCompleted.Update(b => true);
                    vCompleted.Update(i => i + 1);
                    Check.Set();
                };

                ac.Error += e =>
                {
                    var m = e.Message;
                    Console.WriteLine(m);
                };
                if (InitializeClientContext != null) { InitializeClientContext(NumUser, n, cc, ac, Completed); }
                bc.Connect();
                Action<Exception> UnknownFaulted = ex =>
                {
                    int OldValue = 0;
                    vError.Update(v =>
                    {
                        OldValue = v;
                        return v + 1;
                    });
                    if (OldValue <= 10)
                    {
                        Console.WriteLine(String.Format("{0}:{1}", n, ex.Message));
                    }
                    Completed();
                };
                bc.ReceiveAsync
                (
                    a =>
                    {
                        a();
                    },
                    UnknownFaulted
                );
                ac.ServerTime(new ServerTimeRequest { }, r =>
                {
                    vConnected.Update(i => i + 1);
                    Check.Set();
                });
                var t = new Task
                (
                    () =>
                    {
                        Test(NumUser, n, cc, ac, Completed);
                    }
                );
                var tmr = new Timer
                (
                    o =>
                    {
                        if (!bAbondon.Check(b => b)) { return; }
                        if (bCompleted.Check(b => b)) { return; }
                        ac.ServerTime(new ServerTimeRequest { }, r => { });
                    },
                    null,
                    10000,
                    10000
                );
                tl.Add(t);
                bcl.Add(bc);
                ccl.Add(cc);
                tmrl.Add(tmr);
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
                if (!Check.WaitOne(10000))
                {
                    if (vCompleted.Check(i => i > 0))
                    {
                        bAbondon.Update(b => true);
                        break;
                    }
                }
            }

            var NumMutualWaiting = NumUser - vCompleted.Check(i => i);

            while (vCompleted.Check(i => i != NumUser))
            {
                Check.WaitOne();
            }
            foreach (var tmr in tmrl)
            {
                tmr.Dispose();
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

            if (FinalCheck != null)
            {
                FinalCheck(ccl.ToArray());
            }

            var NumError = vError.Check(v => v);
            if (NumError > 0)
            {
                Console.WriteLine("{0} Errors", NumError);
            }
            if (NumMutualWaiting > 0)
            {
                Console.WriteLine("{0} Users, {1} ms, {2} MutualWaiting", NumUser, TimeDiff, NumMutualWaiting);
            }
            else
            {
                Console.WriteLine("{0} Users, {1} ms", NumUser, TimeDiff);
            }
        }

        public static void TestUdpForNumUser(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, int NumUser, String Title, Action<int, int, ClientContext, IApplicationClient, Action> Test, Action<int, int, ClientContext, IApplicationClient, Action> InitializeClientContext = null, Action<ClientContext[]> FinalCheck = null)
        {
            Console.Write("{0}: ", Title);
            Console.Out.Flush();

            var tl = new List<Task>();
            var bcl = new List<Streamed.UdpClient>();
            var ccl = new List<ClientContext>();
            var tmrl = new List<Timer>();
            var vConnected = new LockedVariable<int>(0);
            var vCompleted = new LockedVariable<int>(0);
            var Check = new AutoResetEvent(false);

            var bAbondon = new LockedVariable<Boolean>(false);

            var vError = new LockedVariable<int>(0);

            for (int k = 0; k < NumUser; k += 1)
            {
                var n = k;
                var Lockee = new Object();
                IApplicationClient ac;
                Streamed.IStreamedVirtualTransportClient vtc;
                if (ProtocolType == SerializationProtocolType.Binary)
                {
                    var a = new BinarySerializationClientAdapter();
                    ac = a.GetApplicationClient();
                    vtc = new Streamed.BinaryCountPacketClient(a);
                }
                else if (ProtocolType == SerializationProtocolType.Json)
                {
                    var a = new JsonSerializationClientAdapter();
                    ac = a.GetApplicationClient();
                    vtc = new Streamed.JsonLinePacketClient(a);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var bc = new Streamed.UdpClient(RemoteEndPoint, vtc, QueueUserWorkItem);
                var cc = new ClientContext();
                var bCompleted = new LockedVariable<Boolean>(false);
                Action Completed;
                Action FaultedCompleted;
                if (Test == TestQuit)
                {
                    Completed = () =>
                    {
                        bCompleted.Update(b => true);
                        vCompleted.Update(i => i + 1);
                        Check.Set();
                    };
                    FaultedCompleted = Completed;
                }
                else
                {
                    Completed = () =>
                    {
                        ac.Quit(new QuitRequest { }, r =>
                        {
                            bCompleted.Update(b => true);
                            vCompleted.Update(i => i + 1);
                            Check.Set();
                        });
                    };
                    FaultedCompleted = () =>
                    {
                        bCompleted.Update(b => true);
                        vCompleted.Update(i => i + 1);
                        Check.Set();
                    };
                }

                ac.Error += e =>
                {
                    var m = e.Message;
                    Console.WriteLine(m);
                };
                if (InitializeClientContext != null) { InitializeClientContext(NumUser, n, cc, ac, Completed); }
                bc.Connect();
                Action<Exception> UnknownFaulted = ex =>
                {
                    int OldValue = 0;
                    vError.Update(v =>
                    {
                        OldValue = v;
                        return v + 1;
                    });
                    if (OldValue <= 10)
                    {
                        Console.WriteLine(String.Format("{0}:{1}", n, ex.Message));
                    }
                    FaultedCompleted();
                };
                bc.ReceiveAsync
                (
                    a =>
                    {
                        a();
                    },
                    UnknownFaulted
                );
                ac.ServerTime(new ServerTimeRequest { }, r =>
                {
                    vConnected.Update(i => i + 1);
                    Check.Set();
                });
                var t = new Task
                (
                    () =>
                    {
                        Test(NumUser, n, cc, ac, Completed);
                    }
                );
                var tmr = new Timer
                (
                    o =>
                    {
                        if (!bAbondon.Check(b => b)) { return; }
                        if (bCompleted.Check(b => b)) { return; }
                        int OldValue = 0;
                        vError.Update(v =>
                        {
                            OldValue = v;
                            return v + 1;
                        });
                        if (OldValue <= 10)
                        {
                            Console.WriteLine(String.Format("{0}:{1}", n, "Timedout"));
                        }
                        FaultedCompleted();
                    },
                    null,
                    10000,
                    10000
                );
                tl.Add(t);
                bcl.Add(bc);
                ccl.Add(cc);
                tmrl.Add(tmr);
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
                if (!Check.WaitOne(10000))
                {
                    if (vCompleted.Check(i => i > 0))
                    {
                        bAbondon.Update(b => true);
                        break;
                    }
                }
            }

            var NumMutualWaiting = NumUser - vCompleted.Check(i => i);

            while (vCompleted.Check(i => i != NumUser))
            {
                Check.WaitOne();
            }
            foreach (var tmr in tmrl)
            {
                tmr.Dispose();
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

            if (FinalCheck != null)
            {
                FinalCheck(ccl.ToArray());
            }

            var NumError = vError.Check(v => v);
            if (NumError > 0)
            {
                Console.WriteLine("{0} Errors", NumError);
            }
            if (NumMutualWaiting > 0)
            {
                Console.WriteLine("{0} Users, {1} ms, {2} MutualWaiting", NumUser, TimeDiff, NumMutualWaiting);
            }
            else
            {
                Console.WriteLine("{0} Users, {1} ms", NumUser, TimeDiff);
            }
        }

        public static void TestHttpForNumUser(String UrlPrefix, String ServiceVirtualPath, int NumUser, String Title, Action<int, int, ClientContext, IApplicationClient, Action> Test, Action<int, int, ClientContext, IApplicationClient, Action> InitializeClientContext = null, Action<ClientContext[]> FinalCheck = null)
        {
            Console.Write("{0}: ", Title);
            Console.Out.Flush();

            var tl = new List<Task>();
            var bcl = new List<Http.HttpClient>();
            var ccl = new List<ClientContext>();
            var vConnected = new LockedVariable<int>(0);
            var vCompleted = new LockedVariable<int>(0);
            var Check = new AutoResetEvent(false);

            var vError = new LockedVariable<int>(0);

            for (int k = 0; k < NumUser; k += 1)
            {
                var n = k;
                var Lockee = new Object();
                var a = new JsonSerializationClientAdapter();
                var ac = a.GetApplicationClient();
                var vtc = new Http.JsonHttpPacketClient(a);
                var bc = new Http.HttpClient(UrlPrefix, ServiceVirtualPath, vtc);
                var cc = new ClientContext();
                ac.Error += e =>
                {
                    var m = e.Message;
                    Console.WriteLine(m);
                };
                Action Completed;
                if (Test == TestQuit)
                {
                    Completed = () =>
                    {
                        vCompleted.Update(i => i + 1);
                        Check.Set();
                    };
                }
                else
                {
                    Completed = () =>
                    {
                        ac.Quit(new QuitRequest { }, r =>
                        {
                            vCompleted.Update(i => i + 1);
                            Check.Set();
                        });
                    };
                }
                if (InitializeClientContext != null) { InitializeClientContext(NumUser, n, cc, ac, Completed); }
                try
                {
                    ac.ServerTime(new ServerTimeRequest { }, r =>
                    {
                        vConnected.Update(i => i + 1);
                        Check.Set();
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(String.Format("{0}:{1}", n, ex.Message));
                    Completed();
                }
                var t = new Task
                (
                    () =>
                    {
                        try
                        {
                            Test(NumUser, n, cc, ac, Completed);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(String.Format("{0}:{1}", n, ex.Message));
                            Completed();
                        }
                    }
                );
                tl.Add(t);
                bcl.Add(bc);
                ccl.Add(cc);
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
                bc.Dispose();
            }

            if (FinalCheck != null)
            {
                FinalCheck(ccl.ToArray());
            }

            var NumError = vError.Check(v => v);
            if (NumError > 0)
            {
                Console.WriteLine("{0} Errors", NumError);
            }
            Console.WriteLine("{0} Users, {1} ms", NumUser, TimeDiff);
        }

        public static int DoTestTcp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            //如果在Windows测试提示“由于系统缓冲区空间不足或队列已满，不能执行套接字上的操作。”（WSAENOBUFS 10055），表示系统的TCP端口数不足
            //可以使用下述命令查看动态端口范围
            //netsh int ipv4 show dynamicport tcp
            //可以在管理员权限下使用下述命令设置
            //netsh int ipv4 set dynamicport tcp start=16384 num=49152
            //参见：http://support.microsoft.com/kb/929851/en-us

            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestQuit", TestQuit);
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestAdd", TestAdd);
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestMultiply", TestMultiply);
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestText", TestText);
            Thread.Sleep(1000);
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestMessage", TestMessage, TestMessageInitializeClientContext, TestMessageFinalCheck);
            Thread.Sleep(5000);

            for (int k = 0; k < 8; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestQuit", TestQuit);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 8; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestAdd", TestAdd);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 7; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestMultiply", TestMultiply);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 7; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestText", TestText);
            }
            Thread.Sleep(10000);

            for (int k = 0; k < 6; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestMessage", TestMessage, TestMessageInitializeClientContext, TestMessageFinalCheck);
                Thread.Sleep(1000);
            }

            return 0;
        }

        public static int DoTestUdp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            //如果在Windows测试提示“由于系统缓冲区空间不足或队列已满，不能执行套接字上的操作。”（WSAENOBUFS 10055），表示系统的UDP端口数不足
            //可以使用下述命令查看动态端口范围
            //netsh int ipv4 show dynamicport udp
            //可以在管理员权限下使用下述命令设置
            //netsh int ipv4 set dynamicport udp start=16384 num=49152
            //参见：http://support.microsoft.com/kb/929851/en-us

            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestQuit", TestQuit);
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestAdd", TestAdd);
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestMultiply", TestMultiply);
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestText", TestText);
            Thread.Sleep(1000);
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 64, "TestMessage", TestMessage, TestMessageInitializeClientContext, TestMessageFinalCheck);
            Thread.Sleep(5000);

            for (int k = 0; k < 8; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestQuit", TestQuit);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 8; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestAdd", TestAdd);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 7; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestMultiply", TestMultiply);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 7; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestText", TestText);
            }
            Thread.Sleep(10000);

            for (int k = 0; k < 6; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), "TestMessage", TestMessage, TestMessageInitializeClientContext, TestMessageFinalCheck);
                Thread.Sleep(1000);
            }

            return 0;
        }

        public static int DoTestHttp(String UrlPrefix, String ServiceVirtualPath)
        {
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 64, "TestQuit", TestQuit);
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 64, "TestAdd", TestAdd);
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 64, "TestMultiply", TestMultiply);
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 64, "TestText", TestText);
            Thread.Sleep(5000);

            for (int k = 0; k < 8; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), "TestQuit", TestQuit);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 8; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), "TestAdd", TestAdd);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 7; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), "TestMultiply", TestMultiply);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 7; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), "TestText", TestText);
            }

            return 0;
        }
    }
}
