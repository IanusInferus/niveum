//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天客户端
//  Version:     2012.06.05.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Communication;

namespace Client
{
    public enum ApplicationProtocolType
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
            IPEndPoint RemoteEndPoint;
            ApplicationProtocolType ProtocolType;
            if (argv.Length == 3)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(argv[1]), int.Parse(argv[2]));
                ProtocolType = (ApplicationProtocolType)Enum.Parse(typeof(ApplicationProtocolType), argv[0], true);
            }
            else if (argv.Length == 1)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
                ProtocolType = (ApplicationProtocolType)Enum.Parse(typeof(ApplicationProtocolType), argv[0], true);
            }
            else if (argv.Length == 0)
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001);
                ProtocolType = ApplicationProtocolType.Binary;
            }
            else
            {
                DisplayInfo();
                return -1;
            }

            if (UseLoadTest)
            {
                LoadTest.DoTest(RemoteEndPoint, ProtocolType);
            }
            else if (UsePerformanceTest)
            {
                PerformanceTest.DoTest(RemoteEndPoint, ProtocolType);
            }
            else if (UseStableTest)
            {
                StableTest.DoTest(RemoteEndPoint, ProtocolType);
            }
            else
            {
                Run(RemoteEndPoint, ProtocolType);
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
            Console.WriteLine(@"Client [<Protocol> [<IpAddress> <Port>]] [/load|/perf|/stable]");
            Console.WriteLine(@"Protocol 通讯协议，可为Binary或Json，默认为Binary");
            Console.WriteLine(@"IpAddress 服务器IP地址，默认为127.0.0.1");
            Console.WriteLine(@"Port 服务器端口，默认为8001");
            Console.WriteLine(@"/load 自动化负载测试");
            Console.WriteLine(@"/perf 自动化性能测试");
            Console.WriteLine(@"/stable 自动化稳定性测试");
        }

        public static void Run(IPEndPoint RemoteEndPoint, ApplicationProtocolType ProtocolType)
        {
            if (ProtocolType == ApplicationProtocolType.Binary)
            {
                using (var bc = new BinarySocketClient(RemoteEndPoint, new ClientImplementation()))
                {
                    bc.Connect();
                    Console.WriteLine("连接成功。");
                    bc.Receive(a => a(), se => Console.WriteLine((new SocketException((int)se)).Message));
                    ReadLineAndSendLoop(bc.InnerClient);
                    bc.Close();
                }
            }
            else if (ProtocolType == ApplicationProtocolType.Json)
            {
                using (var jc = new JsonSocketClient(RemoteEndPoint, new ClientImplementation()))
                {
                    jc.Connect();
                    Console.WriteLine("连接成功。");
                    jc.Receive(a => a(), se => Console.WriteLine((new SocketException((int)se)).Message));
                    ReadLineAndSendLoop(jc.InnerClient);
                    jc.Close();
                }
            }
            else
            {
                Console.WriteLine("协议不能识别：" + ProtocolType.ToString());
            }
        }

        public static void ReadLineAndSendLoop(IClient InnerClient)
        {
            while (true)
            {
                var Line = Console.ReadLine();
                if (Line == "exit") { break; }
                if (Line == "shutdown")
                {
                    InnerClient.Shutdown(new ShutdownRequest(), r =>
                    {
                        if (r.OnSuccess)
                        {
                            Console.WriteLine("服务器关闭。");
                        }
                    });
                    break;
                }
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
