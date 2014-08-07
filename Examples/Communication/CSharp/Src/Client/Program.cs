//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天客户端
//  Version:     2014.08.07.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Communication;

namespace Client
{
    public enum TransportProtocolType
    {
        Tcp,
        Udp,
        Http
    }
    public enum SerializationProtocolType
    {
        Binary,
        Json
    }

    public static class Program
    {
        public static int Main(String[] args)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return MainInner();
            }
            else
            {
                try
                {
                    return MainInner();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                    return -1;
                }
            }
        }

        public static int MainInner()
        {
            DisplayTitle();

            var CmdLine = CommandLine.GetCmdLine();

            var UseOld = false;
            var UseLoadTest = false;
            var UsePerformanceTest = false;
            var UseStableTest = false;
            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (opt.Name.ToLower() == "old")
                {
                    UseOld = true;
                }
                else if (opt.Name.ToLower() == "load")
                {
                    UseLoadTest = true;
                }
                else if (opt.Name.ToLower() == "perf")
                {
                    UsePerformanceTest = true;
                }
                else if (opt.Name.ToLower() == "stable")
                {
                    UseStableTest = true;
                }
            }

            var argv = CmdLine.Arguments;
            TransportProtocolType TransportProtocolType = TransportProtocolType.Tcp;
            SerializationProtocolType SerializationProtocolType = SerializationProtocolType.Binary;
            int DefaultPort = 8001;
            if (argv.Length >= 1)
            {
                TransportProtocolType = (TransportProtocolType)Enum.Parse(typeof(TransportProtocolType), argv[0], true);
            }
            if (TransportProtocolType == TransportProtocolType.Tcp)
            {
                if (argv.Length >= 2)
                {
                    SerializationProtocolType = (SerializationProtocolType)Enum.Parse(typeof(SerializationProtocolType), argv[1], true);
                }
                if (SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    DefaultPort = 8001;
                }
                else if (SerializationProtocolType == SerializationProtocolType.Json)
                {
                    DefaultPort = 8002;
                }
            }
            else if (TransportProtocolType == TransportProtocolType.Udp)
            {
                if (argv.Length >= 2)
                {
                    SerializationProtocolType = (SerializationProtocolType)Enum.Parse(typeof(SerializationProtocolType), argv[1], true);
                }
                if (SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    DefaultPort = 8001;
                }
                else if (SerializationProtocolType == SerializationProtocolType.Json)
                {
                    DefaultPort = 8002;
                }
            }

            if (TransportProtocolType == TransportProtocolType.Tcp)
            {
                IPEndPoint RemoteEndPoint;
                if (argv.Length == 4)
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(argv[2]), int.Parse(argv[3]));
                }
                else if (argv.Length == 2)
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), DefaultPort);
                }
                else if (argv.Length == 0)
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), DefaultPort);
                }
                else
                {
                    DisplayInfo();
                    return -1;
                }

                if (UseLoadTest)
                {
                    LoadTest.DoTestTcp(RemoteEndPoint, SerializationProtocolType);
                }
                else if (UsePerformanceTest)
                {
                    PerformanceTest.DoTestTcp(RemoteEndPoint, SerializationProtocolType);
                }
                else if (UseStableTest)
                {
                    StableTest.DoTestTcp(RemoteEndPoint, SerializationProtocolType);
                }
                else
                {
                    RunTcp(RemoteEndPoint, SerializationProtocolType, UseOld);
                }
            }
            else if (TransportProtocolType == TransportProtocolType.Udp)
            {
                IPEndPoint RemoteEndPoint;
                if (argv.Length == 4)
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(argv[2]), int.Parse(argv[3]));
                }
                else if (argv.Length == 2)
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), DefaultPort);
                }
                else
                {
                    DisplayInfo();
                    return -1;
                }

                if (UseLoadTest)
                {
                    LoadTest.DoTestUdp(RemoteEndPoint, SerializationProtocolType);
                }
                else if (UsePerformanceTest)
                {
                    PerformanceTest.DoTestUdp(RemoteEndPoint, SerializationProtocolType);
                }
                else if (UseStableTest)
                {
                    StableTest.DoTestTcp(RemoteEndPoint, SerializationProtocolType);
                }
                else
                {
                    RunUdp(RemoteEndPoint, SerializationProtocolType, UseOld);
                }
            }
            else if (TransportProtocolType == TransportProtocolType.Http)
            {
                String UrlPrefix = "http://localhost:8003/";
                String ServiceVirtualPath = "cmd";
                if (argv.Length == 3)
                {
                    UrlPrefix = argv[1];
                    ServiceVirtualPath = argv[2];
                }
                else if (argv.Length == 1)
                {
                }
                else
                {
                    DisplayInfo();
                    return -1;
                }

                if (UseLoadTest)
                {
                    LoadTest.DoTestHttp(UrlPrefix, ServiceVirtualPath);
                }
                else if (UsePerformanceTest)
                {
                    PerformanceTest.DoTestHttp(UrlPrefix, ServiceVirtualPath);
                }
                else if (UseStableTest)
                {
                    StableTest.DoTestHttp(UrlPrefix, ServiceVirtualPath);
                }
                else
                {
                    RunHttp(UrlPrefix, ServiceVirtualPath, UseOld);
                }
            }
            else
            {
                DisplayInfo();
                return -1;
            }

            return 0;
        }

        public static void DisplayTitle()
        {
            Console.WriteLine(@"聊天客户端");
            Console.WriteLine(@"Author:      F.R.C.");
            Console.WriteLine(@"Copyright(C) Public Domain");
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"Client [<TransportProtocol=Tcp> <SerializationProtocol=Binary>] [<IpAddress=127.0.0.1> <Port=8001>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Client <TransportProtocol=Tcp> <SerializationProtocol=Json> [<IpAddress=127.0.0.1> <Port=8002>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Client <TransportProtocol=Udp> <SerializationProtocol=Binary> [<IpAddress=127.0.0.1> <Port=8001>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Client <TransportProtocol=Udp> <SerializationProtocol=Json> [<IpAddress=127.0.0.1> <Port=8002>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Client <TransportProtocol=Http> [<UrlPrefix=http://localhost:8003/> <ServiceVirtualPath=cmd>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Protocol 通讯协议，可为Binary|Json|Http，默认为Binary");
            Console.WriteLine(@"IpAddress 服务器IP地址");
            Console.WriteLine(@"Port 服务器端口");
            Console.WriteLine(@"/old 使用老协议");
            Console.WriteLine(@"/load 自动化负载测试");
            Console.WriteLine(@"/perf 自动化性能测试");
            Console.WriteLine(@"/stable 自动化稳定性测试");
        }

        public static void RunTcp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, Boolean UseOld)
        {
            if (!(ProtocolType == SerializationProtocolType.Binary || ProtocolType == SerializationProtocolType.Json))
            {
                Console.WriteLine("协议不能识别：" + ProtocolType.ToString());
            }
            IApplicationClient ac;
            Tcp.ITcpVirtualTransportClient vtc;
            var bt = new Rc4PacketClientTransformer();
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                var a = new BinarySerializationClientAdapter();
                ac = a.GetApplicationClient();
                vtc = new Tcp.BinaryCountPacketClient(a, bt);
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                var a = new JsonSerializationClientAdapter();
                ac = a.GetApplicationClient();
                vtc = new Tcp.JsonLinePacketClient(a, bt);
            }
            else
            {
                throw new InvalidOperationException();
            }
            using (var bc = new Tcp.TcpClient(RemoteEndPoint, vtc))
            {
                bc.Connect();
                Console.WriteLine("连接成功。输入login登录，输入secure启用安全连接。");

                var Lockee = new Object();
                Action<Action> DoHandle = a =>
                {
                    lock (Lockee)
                    {
                        a();
                    }
                };
                bc.ReceiveAsync(DoHandle, ex => Console.WriteLine(ex.Message));
                Action<SecureContext> SetSecureContext = c =>
                {
                    bt.SetSecureContext(c);
                };
                ReadLineAndSendLoop(ac, SetSecureContext, UseOld, Lockee);
            }
        }

        public static void RunUdp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, Boolean UseOld)
        {
            if (!(ProtocolType == SerializationProtocolType.Binary || ProtocolType == SerializationProtocolType.Json))
            {
                Console.WriteLine("协议不能识别：" + ProtocolType.ToString());
            }
            IApplicationClient ac;
            Tcp.ITcpVirtualTransportClient vtc;
            var bt = new Rc4PacketClientTransformer();
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                var a = new BinarySerializationClientAdapter();
                ac = a.GetApplicationClient();
                vtc = new Tcp.BinaryCountPacketClient(a, bt);
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                var a = new JsonSerializationClientAdapter();
                ac = a.GetApplicationClient();
                vtc = new Tcp.JsonLinePacketClient(a, bt);
            }
            else
            {
                throw new InvalidOperationException();
            }
            using (var bc = new Tcp.UdpClient(RemoteEndPoint, vtc))
            {
                bc.Connect();
                Console.WriteLine("输入login登录，输入secure启用安全连接。");

                var Lockee = new Object();
                Action<Action> DoHandle = a =>
                {
                    lock (Lockee)
                    {
                        a();
                    }
                };
                bc.ReceiveAsync(DoHandle, ex => Console.WriteLine(ex.Message));
                Action<SecureContext> SetSecureContext = c =>
                {
                    bt.SetSecureContext(c);
                    bc.SecureContext = c;
                };
                ReadLineAndSendLoop(ac, SetSecureContext, UseOld, Lockee);
            }
        }

        public static void RunHttp(String UrlPrefix, String ServiceVirtualPath, Boolean UseOld)
        {
            IApplicationClient ac;
            Http.IHttpVirtualTransportClient vtc;
            var a = new JsonSerializationClientAdapter();
            ac = a.GetApplicationClient();
            vtc = new Http.JsonHttpPacketClient(a);
            using (var bc = new Http.HttpClient(UrlPrefix, ServiceVirtualPath, vtc))
            {
                Console.WriteLine("输入login登录。");

                var Lockee = new Object();
                Action<SecureContext> SetSecureContext = c =>
                {
                    throw new InvalidOperationException();
                };
                ReadLineAndSendLoop(ac, SetSecureContext, UseOld, Lockee);
            }
        }

        public static void ReadLineAndSendLoop(IApplicationClient InnerClient, Action<SecureContext> SetSecureContext, Boolean UseOld, Object Lockee)
        {
            var NeedToExit = false;
            AutoResetEvent NeedToCheck = new AutoResetEvent(false);
            InnerClient.ServerShutdown += e =>
            {
                Console.WriteLine("服务器已关闭。");
                lock (Lockee)
                {
                    NeedToExit = true;
                }
                NeedToCheck.Set();
            };
            InnerClient.Error += e =>
            {
                var m = e.Message;
                Console.WriteLine(m);
            };
            InnerClient.MessageReceived += e => Console.WriteLine(e.Content);
            InnerClient.MessageReceivedAt1 += e =>
            {
                if (e.Title != "")
                {
                    Console.WriteLine(e.Title);
                }
                foreach (var Line in e.Lines)
                {
                    Console.WriteLine(Line);
                }
            };
            Action<CheckSchemaVersionReply> CheckSchemaVersionHandler = r =>
            {
                if (r.OnHead)
                {
                }
                else if (r.OnSupported)
                {
                    Console.WriteLine("客户端不是最新版本，但服务器可以支持。");
                }
                else if (r.OnNotSupported)
                {
                    Console.WriteLine("客户端版本不受支持。");
                    lock (Lockee)
                    {
                        NeedToExit = true;
                    }
                    NeedToCheck.Set();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            };
            if (UseOld)
            {
                InnerClient.CheckSchemaVersion(new CheckSchemaVersionRequest { Hash = "98301A7C877EDA6E" }, CheckSchemaVersionHandler);
            }
            else
            {
                InnerClient.CheckSchemaVersion(new CheckSchemaVersionRequest { Hash = InnerClient.Hash.ToString("X16") }, CheckSchemaVersionHandler);
            }
            while (true)
            {
                String Line = null;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var l = Console.ReadLine();
                    lock (Lockee)
                    {
                        Line = l;
                    }
                    NeedToCheck.Set();
                });
                while (true)
                {
                    if (NeedToExit) { return; }
                    String l;
                    lock (Lockee)
                    {
                        l = Line;
                    }
                    if (l != null) { break; }
                    NeedToCheck.WaitOne();
                }
                lock (Lockee)
                {
                    if (Line == "exit")
                    {
                        InnerClient.Quit(new QuitRequest(), r => { });
                        break;
                    }
                    if (Line == "shutdown")
                    {
                        InnerClient.Shutdown(new ShutdownRequest(), r =>
                        {
                            if (r.OnSuccess)
                            {
                                Console.WriteLine("服务器正在关闭。");
                            }
                        });
                        break;
                    }
                    if (Line == "secure")
                    {
                        InnerClient.SendMessage(new SendMessageRequest { Content = Line }, r =>
                        {
                            //生成测试用确定Key
                            var ServerToken = Enumerable.Range(0, 41).Select(i => (Byte)(i)).ToArray();
                            var ClientToken = Enumerable.Range(0, 41).Select(i => (Byte)(40 - i)).ToArray();
                            SetSecureContext(new SecureContext { ServerToken = ServerToken, ClientToken = ClientToken });
                        });
                        continue;
                    }
                    if (UseOld)
                    {
                        InnerClient.SendMessageAt1(new SendMessageAt1Request { Title = "", Lines = new List<String> { Line } }, r =>
                        {
                            if (r.OnTitleTooLong || r.OnLinesTooLong || r.OnLineTooLong)
                            {
                                Console.WriteLine("消息过长。");
                            }
                        });
                    }
                    else
                    {
                        InnerClient.SendMessage(new SendMessageRequest { Content = Line }, r =>
                        {
                            if (r.OnTooLong)
                            {
                                Console.WriteLine("消息过长。");
                            }
                        });
                    }
                }
            }
        }
    }
}
