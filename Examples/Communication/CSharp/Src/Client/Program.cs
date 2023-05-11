//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.Examples <Visual C#>
//  Description: 聊天客户端
//  Version:     2023.05.11.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Firefly;
using Communication;
using BaseSystem;

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
                String UrlPrefix = "http://localhost:8003/api/";
                String ServiceVirtualPath = "q";
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
            Console.WriteLine(@"Client <TransportProtocol=Http> [<UrlPrefix=http://localhost:8003/api/> <ServiceVirtualPath=q>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Protocol 通讯协议，可为Binary|Json|Http，默认为Binary");
            Console.WriteLine(@"IpAddress 服务器IP地址");
            Console.WriteLine(@"Port 服务器端口");
            Console.WriteLine(@"/old 使用老协议");
            Console.WriteLine(@"/load 自动化负载测试");
            Console.WriteLine(@"/perf 自动化性能测试");
            Console.WriteLine(@"/stable 自动化稳定性测试");
        }

        private const int ResponseTimeoutSeconds = 30;
        public static void RunTcp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, Boolean UseOld)
        {
            if (!(ProtocolType == SerializationProtocolType.Binary || ProtocolType == SerializationProtocolType.Json))
            {
                Console.WriteLine("协议不能识别：" + ProtocolType.ToString());
            }
            IApplicationClient ac;
            IStreamedVirtualTransportClient vtc;
            var bt = new Rc4PacketClientTransformer();
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                var a = new BinarySerializationClientAdapter();
                a.ResponseTimeoutSeconds = ResponseTimeoutSeconds;
                ac = a.GetApplicationClient();
                vtc = new BinaryCountPacketClient(a, bt);
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                var a = new JsonSerializationClientAdapter();
                a.ResponseTimeoutSeconds = ResponseTimeoutSeconds;
                ac = a.GetApplicationClient();
                vtc = new JsonLinePacketClient(a, bt);
            }
            else
            {
                throw new InvalidOperationException();
            }
            using (var bc = new TcpClient(RemoteEndPoint, vtc, new TaskFactory()))
            {
                bc.Connect();
                Console.WriteLine("连接成功。输入login登录，输入secure启用安全连接。");

                bc.ReceiveAsync(ex =>
                {
                    Console.WriteLine(ex.Message);
                    bc.Dispose();
                });
                Action<SecureContext> SetSecureContext = c =>
                {
                    bt.SetSecureContext(c);
                };
                ReadLineAndSendLoop(ac, SetSecureContext, UseOld).Wait();
            }
        }

        public static void RunUdp(IPEndPoint RemoteEndPoint, SerializationProtocolType ProtocolType, Boolean UseOld)
        {
            if (!(ProtocolType == SerializationProtocolType.Binary || ProtocolType == SerializationProtocolType.Json))
            {
                Console.WriteLine("协议不能识别：" + ProtocolType.ToString());
            }
            IApplicationClient ac;
            IStreamedVirtualTransportClient vtc;
            var bt = new Rc4PacketClientTransformer();
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                var a = new BinarySerializationClientAdapter();
                a.ResponseTimeoutSeconds = ResponseTimeoutSeconds;
                ac = a.GetApplicationClient();
                vtc = new BinaryCountPacketClient(a, bt);
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                var a = new JsonSerializationClientAdapter();
                a.ResponseTimeoutSeconds = ResponseTimeoutSeconds;
                ac = a.GetApplicationClient();
                vtc = new JsonLinePacketClient(a, bt);
            }
            else
            {
                throw new InvalidOperationException();
            }
            using (var bc = new UdpClient(RemoteEndPoint, vtc, new TaskFactory()))
            {
                bc.Connect();
                Console.WriteLine("输入login登录，输入secure启用安全连接。");

                bc.ReceiveAsync(ex =>
                {
                    Console.WriteLine(ex.Message);
                    bc.Dispose();
                });
                Action<SecureContext> SetSecureContext = c =>
                {
                    bt.SetSecureContext(c);
                    bc.SecureContext = c;
                };
                ReadLineAndSendLoop(ac, SetSecureContext, UseOld).Wait();
            }
        }

        public static void RunHttp(String UrlPrefix, String ServiceVirtualPath, Boolean UseOld)
        {
            IApplicationClient ac;
            Http.IHttpVirtualTransportClient vtc;
            var a = new JsonSerializationClientAdapter();
            a.ResponseTimeoutSeconds = ResponseTimeoutSeconds;
            ac = a.GetApplicationClient();
            vtc = new Http.JsonHttpPacketClient(a);
            using (var bc = new Http.HttpClient(UrlPrefix, ServiceVirtualPath, vtc))
            {
                Console.WriteLine("输入login登录。");

                Action<SecureContext> SetSecureContext = c =>
                {
                    throw new InvalidOperationException();
                };
                ReadLineAndSendLoop(ac, SetSecureContext, UseOld).Wait();
            }
        }

        public static async Task ReadLineAndSendLoop(IApplicationClient InnerClient, Action<SecureContext> SetSecureContext, Boolean UseOld)
        {
            var NeedToExit = new CancellationTokenSource();
            var NeedToExitToken = NeedToExit.Token;
            var NeedToCheck = new AutoResetEvent(false);
            InnerClient.ServerShutdown += e =>
            {
                Console.WriteLine("服务器已关闭。");
                NeedToExit.Cancel();
                NeedToCheck.Set();
            };
            InnerClient.Error += e =>
            {
                var m = e.Message;
                Console.WriteLine(m);
            };
            InnerClient.MessageReceived += e => Console.WriteLine(e.Content);
            InnerClient.MessageReceivedAt2 += e =>
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
            var r = await InnerClient.CheckSchemaVersion(new CheckSchemaVersionRequest { Hash = UseOld ? "D7FFBD0D2E5D7274" : InnerClient.Hash.ToString("X16") });
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
                return;
            }
            else
            {
                throw new InvalidOperationException();
            }
            var Factory = new TaskFactory();
            try
            {
                while (true)
                {
                    var LockedLine = new LockedVariable<String>(null);
                    _ = Factory.StartNew(() =>
                    {
                        var Line = Console.ReadLine();
                        LockedLine.Update(OldLine => Line);
                        NeedToCheck.Set();
                    });

                    String CurrentLine = null;
                    while (true)
                    {
                        if (NeedToExitToken.IsCancellationRequested) { return; }
                        CurrentLine = LockedLine.Check(l => l);
                        if (CurrentLine != null) { break; }
                        NeedToCheck.WaitOne();
                    }

                    if (!await HandleLine(InnerClient, SetSecureContext, UseOld, CurrentLine))
                    {
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static async Task<bool> HandleLine(IApplicationClient InnerClient, Action<SecureContext> SetSecureContext, bool UseOld, string Line)
        {
            if (Line == "exit")
            {
                await InnerClient.Quit(new QuitRequest());
                return false;
            }
            if (Line == "shutdown")
            {
                var r = await InnerClient.Shutdown(new ShutdownRequest());
                if (r.OnSuccess)
                {
                    Console.WriteLine("服务器正在关闭。");
                }
                return false;
            }
            if (Line == "secure")
            {
                await InnerClient.SendMessage(new SendMessageRequest { Content = Line });
                //生成测试用确定Key
                var ServerToken = Enumerable.Range(0, 41).Select(i => (Byte)(i)).ToArray();
                var ClientToken = Enumerable.Range(0, 41).Select(i => (Byte)(40 - i)).ToArray();
                SetSecureContext(new SecureContext { ServerToken = ServerToken, ClientToken = ClientToken });
            }
            else if (UseOld)
            {
                var r = await InnerClient.SendMessageAt1(new SendMessageAt1Request { Id = 1, Message = new MessageAt2 { Title = "", Lines = new List<String> { Line } } });
                if (r.OnTitleTooLong || r.OnLinesTooLong || r.OnLineTooLong)
                {
                    Console.WriteLine("消息过长。");
                }
            }
            else
            {
                var r = await InnerClient.SendMessage(new SendMessageRequest { Content = Line });
                if (r.OnTooLong)
                {
                    Console.WriteLine("消息过长。");
                }
            }
            return true;
        }
    }
}
