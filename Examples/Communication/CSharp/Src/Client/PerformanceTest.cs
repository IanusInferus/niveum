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
    class PerformanceTest
    {
        public class ClientContext
        {
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

        public static void TestTcpForNumUser(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, int NumRequestPerUser, int NumUser, String Title, Action<int, int, ClientContext, IApplicationClient, Action> Test)
        {
            Console.Write("{0}: ", Title);
            Console.Out.Flush();

            var tll = new Object();
            var tl = new List<Task>();
            var bcl = new List<Streamed.TcpClient>();
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
                var bc = new Streamed.TcpClient(RemoteEndPoint, vtc);
                var cc = new ClientContext();
                ac.Error += e =>
                {
                    var m = e.Message;
                    Console.WriteLine(m);
                };
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
                    vCompleted.Update(i => i + 1);
                    Check.Set();
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
                Action f = () =>
                {
                    Test(NumUser, n, cc, ac, Completed);
                };
                var t = new Task(f);
                lock (tll)
                {
                    tl.Add(t);
                }
                bcl.Add(bc);
                ccl.Add(cc);

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
                Console.WriteLine("{0} Errors", NumError);
            }
            Console.WriteLine("{0} Users, {1} Request/User, {2} ms", NumUser, NumRequestPerUser, TimeDiff);
        }

        public static void TestUdpForNumUser(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, int NumRequestPerUser, int NumUser, String Title, Action<int, int, ClientContext, IApplicationClient, Action> Test)
        {
            Console.Write("{0}: ", Title);
            Console.Out.Flush();

            var tll = new Object();
            var tl = new List<Task>();
            var bcl = new List<Streamed.UdpClient>();
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
                var bc = new Streamed.UdpClient(RemoteEndPoint, vtc);
                var cc = new ClientContext();
                ac.Error += e =>
                {
                    var m = e.Message;
                    Console.WriteLine(m);
                };
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
                    vCompleted.Update(i => i + 1);
                    Check.Set();
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
                Action f = () =>
                {
                    Test(NumUser, n, cc, ac, Completed);
                };
                var t = new Task(f);
                lock (tll)
                {
                    tl.Add(t);
                }
                bcl.Add(bc);
                ccl.Add(cc);

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
                    ac.Quit(new QuitRequest { }, r =>
                    {
                        vCompleted.Update(i => i + 1);
                        Check.Set();
                    });
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
                Console.WriteLine("{0} Errors", NumError);
            }
            Console.WriteLine("{0} Users, {1} Request/User, {2} ms", NumUser, NumRequestPerUser, TimeDiff);
        }

        public static void TestHttpForNumUser(String UrlPrefix, String ServiceVirtualPath, int NumRequestPerUser, int NumUser, String Title, Action<int, int, ClientContext, IApplicationClient, Action> Test)
        {
            Console.Write("{0}: ", Title);
            Console.Out.Flush();

            var tll = new Object();
            var tl = new List<Task>();
            var bcl = new List<Http.HttpClient>();
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
                Action<Exception> HandleError = ex =>
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
                    vCompleted.Update(i => i + 1);
                    Check.Set();
                };
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
                    HandleError(ex);
                }
                Action f = () =>
                {
                    try
                    {
                        Test(NumUser, n, cc, ac, Completed);
                    }
                    catch (Exception ex)
                    {
                        HandleError(ex);
                    }
                };
                var t = new Task(f);
                lock (tll)
                {
                    tl.Add(t);
                }
                bcl.Add(bc);
                ccl.Add(cc);

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
                    ac.Quit(new QuitRequest { }, r =>
                    {
                        vCompleted.Update(i => i + 1);
                        Check.Set();
                    });
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
                Console.WriteLine("{0} Errors", NumError);
            }
            Console.WriteLine("{0} Users, {1} Request/User, {2} ms", NumUser, NumRequestPerUser, TimeDiff);
        }

        public static int DoTestTcp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestAdd", TestAdd);
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestMultiply", TestMultiply);
            TestTcpForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestText", TestText);
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestAdd", TestAdd);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestMultiply", TestMultiply);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestTcpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestText", TestText);
            }

            return 0;
        }

        public static int DoTestUdp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType)
        {
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestAdd", TestAdd);
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestMultiply", TestMultiply);
            TestUdpForNumUser(RemoteEndPoint, ProtocolType, 8, 1, "TestText", TestText);
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestAdd", TestAdd);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestMultiply", TestMultiply);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestUdpForNumUser(RemoteEndPoint, ProtocolType, 1 << (2 * k), 4096, "TestText", TestText);
            }

            return 0;
        }

        public static int DoTestHttp(String UrlPrefix, String ServiceVirtualPath)
        {
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 8, 1, "TestAdd", TestAdd);
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 8, 1, "TestMultiply", TestMultiply);
            TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 8, 1, "TestText", TestText);
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), 4096, "TestAdd", TestAdd);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), 4096, "TestMultiply", TestMultiply);
            }
            Thread.Sleep(5000);

            for (int k = 0; k < 4; k += 1)
            {
                TestHttpForNumUser(UrlPrefix, ServiceVirtualPath, 1 << (2 * k), 4096, "TestText", TestText);
            }

            return 0;
        }
    }
}
