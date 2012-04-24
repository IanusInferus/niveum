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

            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
            }

            var argv = CmdLine.Arguments;
            if (argv.Length == 3)
            {
                Run(new IPEndPoint(IPAddress.Parse(argv[1]), int.Parse(argv[2])), (ApplicationProtocolType)Enum.Parse(typeof(ApplicationProtocolType), argv[0], true));
            }
            else if (argv.Length == 1)
            {
                Run(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001), (ApplicationProtocolType)Enum.Parse(typeof(ApplicationProtocolType), argv[0], true));
            }
            else if (argv.Length == 0)
            {
                Run(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001), ApplicationProtocolType.Binary);
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
            Console.WriteLine(@"Client [<Protocol> [<IpAddress> <Port>]]");
            Console.WriteLine(@"Protocol 通讯协议，可为Binary或Json，默认为Binary");
            Console.WriteLine(@"IpAddress 服务器IP地址，默认为127.0.0.1");
            Console.WriteLine(@"Port 服务器端口，默认为8001");
        }

        public static void Run(IPEndPoint RemoteEndPoint, ApplicationProtocolType ProtocolType)
        {
            if (ProtocolType == ApplicationProtocolType.Binary)
            {
                using (var bc = new BinaryClient(RemoteEndPoint, new ClientImplementation()))
                {
                    bc.Connect();
                    Console.WriteLine("连接成功。");
                    bc.Receive(se => Console.WriteLine((new SocketException((int)se)).Message));
                    while (true)
                    {
                        var Line = Console.ReadLine();
                        if (Line == "exit") { break; }
                        bc.InnerClient.SendMessage(new SendMessageRequest { Content = Line }, (c, r) =>
                        {
                            if (r.OnTooLong)
                            {
                                Console.WriteLine("消息过长。");
                            }
                        });
                    }
                    bc.Close();
                }
            }
            else if (ProtocolType == ApplicationProtocolType.Json)
            {
                using (var jc = new JsonClient(RemoteEndPoint, new ClientImplementation()))
                {
                    jc.Connect();
                    Console.WriteLine("连接成功。");
                    jc.Receive(se => Console.WriteLine((new SocketException((int)se)).Message));
                    while (true)
                    {
                        var Line = Console.ReadLine();
                        if (Line == "exit") { break; }
                        jc.InnerClient.SendMessage(new SendMessageRequest { Content = Line }, (c, r) =>
                        {
                            if (r.OnTooLong)
                            {
                                Console.WriteLine("消息过长。");
                            }
                        });
                    }
                    jc.Close();
                }
            }
            else
            {
                Console.WriteLine("协议不能识别：" + ProtocolType.ToString());
            }
        }
    }
}
