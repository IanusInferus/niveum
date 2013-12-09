//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天客户端
//  Version:     2013.12.09.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Communication;

namespace Client
{
    public enum ServerProtocolType
    {
        Binary,
        Json,
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
            ServerProtocolType ProtocolType = ServerProtocolType.Binary;
            SerializationProtocolType SerializationProtocolType = SerializationProtocolType.Binary;
            int DefaultPort = 8001;
            if (argv.Length > 0)
            {
                ProtocolType = (ServerProtocolType)Enum.Parse(typeof(ServerProtocolType), argv[0], true);
                if (ProtocolType == ServerProtocolType.Binary)
                {
                    SerializationProtocolType = SerializationProtocolType.Binary;
                    DefaultPort = 8001;
                }
                else if (ProtocolType == ServerProtocolType.Json)
                {
                    SerializationProtocolType = SerializationProtocolType.Json;
                    DefaultPort = 8002;
                }
            }

            if (ProtocolType != ServerProtocolType.Http)
            {
                IPEndPoint RemoteEndPoint;
                if (argv.Length == 3)
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(argv[1]), int.Parse(argv[2]));
                }
                else if (argv.Length == 1)
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
            else
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
            Console.WriteLine(@"Client [<Protocol=Binary> [<IpAddress=127.0.0.1> <Port=8001>]] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Client <Protocol=Json> [<IpAddress=127.0.0.1> <Port=8002>] [/old|/load|/perf|/stable]");
            Console.WriteLine(@"Client <Protocol=Http> [<UrlPrefix=http://localhost:8003/> <ServiceVirtualPath=cmd>] [/old|/load|/perf|/stable]");
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
            if (ProtocolType == SerializationProtocolType.Binary)
            {
                var a = new BinarySerializationClientAdapter();
                ac = a.GetApplicationClient();
                vtc = new Tcp.BinaryCountPacketClient(a);
            }
            else if (ProtocolType == SerializationProtocolType.Json)
            {
                var a = new JsonSerializationClientAdapter();
                ac = a.GetApplicationClient();
                vtc = new Tcp.JsonLinePacketClient(a);
            }
            else
            {
                throw new InvalidOperationException();
            }
            using (var bc = new Tcp.TcpClient(RemoteEndPoint, vtc))
            {
                bc.Connect();
                Console.WriteLine("连接成功。");

                var Lockee = new Object();
                Action<Action> DoHandle = a =>
                {
                    lock (Lockee)
                    {
                        a();
                    }
                };
                bc.ReceiveAsync(DoHandle, ex => Console.WriteLine(ex.Message));
                ReadLineAndSendLoop(ac, UseOld, Lockee);
            }
        }

        public static void RunHttp(String UrlPrefix, String ServiceVirtualPath, Boolean UseOld)
        {
            using (var bc = new HttpClient(UrlPrefix, ServiceVirtualPath))
            {
                var Lockee = new Object();
                ReadLineAndSendLoop(bc.InnerClient, UseOld, Lockee);
            }
        }

        public static void ReadLineAndSendLoop(IApplicationClient InnerClient, Boolean UseOld, Object Lockee)
        {
            var Shutdown = false;
            InnerClient.ServerShutdown += e =>
            {
                Console.WriteLine("服务器已关闭。");
                Shutdown = true;
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
                var Line = Console.ReadLine();
                if (Shutdown) { break; }
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
