//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天服务器
//  Version:     2012.12.14.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Net;
using System.Threading.Tasks;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting.TreeFormat;

namespace Server
{
    public sealed class Program
    {
        public static int Main()
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

            var c = LoadConfiguration();

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Run(c);
                return 0;
            }
            else
            {
                while (true)
                {
                    try
                    {
                        Run(c);
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        var TimeNow = DateTime.UtcNow;
                        var LocalTime = TimeNow.ToLocalTime();
                        var TimeOffset = LocalTime - TimeNow;
                        var Time = LocalTime.ToString("yyyy-MM-dd HH:mm:ss.fff" + String.Format(" (UTC+{0})", TimeOffset.TotalHours));
                        var Message = Time + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
                        Console.WriteLine(Message);
                    }
                    Thread.Sleep(10000);
                }
            }
        }

        public static void DisplayTitle()
        {
            Console.WriteLine(@"聊天服务器");
            Console.WriteLine(@"Author:      F.R.C.");
            Console.WriteLine(@"Copyright(C) Public Domain");
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"直接运行程序");
            Console.WriteLine(@"Server");
        }

        public static Configuration LoadConfiguration()
        {
            var ConfigurationFilePath = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Assembly.GetEntryAssembly().Location), "Configuration.tree");
            var x = TreeFile.ReadFile(ConfigurationFilePath);
            var c = (new XmlSerializer()).Read<Configuration>(x);
            return c;
        }

        private static AutoResetEvent ExitEvent = new AutoResetEvent(false);
        public static void Run(Configuration c)
        {
            var ProcessorCount = Environment.ProcessorCount;
            ThreadPool.SetMinThreads(ProcessorCount + 1, ProcessorCount + 1);
            ThreadPool.SetMaxThreads(ProcessorCount * 2 + 1, ProcessorCount * 2 + 1);

            Console.WriteLine(@"逻辑处理器数量: " + ProcessorCount.ToString());

            using (var Logger = new ConsoleLogger())
            {
                var ServerContext = new ServerContext();
                ServerContext.Shutdown += () =>
                {
                    ExitEvent.Set();
                };

                Logger.Start();

                var ServerDict = new Dictionary<VirtualServerConfiguration, IServer>();

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    foreach (var s in c.Servers)
                    {
                        ServerDict.Add(s, StartServer(s, ServerContext, Logger));
                    }

                    ExitEvent.WaitOne();

                    foreach (var s in c.Servers)
                    {
                        if (ServerDict.ContainsKey(s))
                        {
                            var Server = ServerDict[s];
                            StopServer(s, Server, Logger);
                        }
                    }
                }
                else
                {
                    foreach (var s in c.Servers)
                    {
                        try
                        {
                            ServerDict.Add(s, StartServer(s, ServerContext, Logger));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                        }
                    }

                    ExitEvent.WaitOne();

                    foreach (var s in c.Servers)
                    {
                        try
                        {
                            if (ServerDict.ContainsKey(s))
                            {
                                var Server = ServerDict[s];
                                StopServer(s, Server, Logger);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                        }
                    }
                }

                Logger.Stop();
            }
        }

        private static IServer StartServer(VirtualServerConfiguration vsc, ServerContext ServerContext, ConsoleLogger Logger)
        {
            if (vsc.OnTcp)
            {
                var s = vsc.Tcp;

                if (!(s.SerializationProtocolType == SerializationProtocolType.Binary || s.SerializationProtocolType == SerializationProtocolType.Json))
                {
                    throw new InvalidOperationException("未知协议类型: " + s.SerializationProtocolType.ToString());
                }

                var Server = new TcpServer(ServerContext);
                var Success = false;

                try
                {
                    if (s.EnableLogConsole)
                    {
                        Server.SessionLog += Logger.Push;
                    }

                    Server.SerializationProtocolType = s.SerializationProtocolType;

                    Server.CheckCommandAllowed = (sc, CommandName) =>
                    {
                        return true;
                    };

                    Server.Bindings = s.Bindings.Select(b => new IPEndPoint(IPAddress.Parse(b.IpAddress), b.Port)).ToArray();
                    Server.SessionIdleTimeout = s.SessionIdleTimeout;
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;
                    Server.ClientDebug = s.ClientDebug;
                    Server.EnableLogNormalIn = s.EnableLogNormalIn;
                    Server.EnableLogNormalOut = s.EnableLogNormalOut;
                    Server.EnableLogUnknownError = s.EnableLogUnknownError;
                    Server.EnableLogCriticalError = s.EnableLogCriticalError;
                    Server.EnableLogPerformance = s.EnableLogPerformance;
                    Server.EnableLogSystem = s.EnableLogSystem;

                    Server.Start();

                    Console.WriteLine(@"TCP服务器已启动。");
                    Console.WriteLine(@"序列化协议类型: " + s.SerializationProtocolType.ToString());
                    Console.WriteLine(@"服务结点: " + String.Join(", ", Server.Bindings.Select(b => b.ToString())));

                    Success = true;
                }
                finally
                {
                    if (!Success)
                    {
                        Server.Dispose();
                    }
                }

                return Server;
            }
            else if (vsc.OnHttp)
            {
                throw new NotImplementedException("Http服务器尚未实现。");
            }
            else
            {
                throw new InvalidOperationException("未知服务器类型: " + vsc._Tag.ToString());
            }
        }
        private static void StopServer(VirtualServerConfiguration vsc, IServer Server, ConsoleLogger Logger)
        {
            if (vsc.OnTcp)
            {
                var s = vsc.Tcp;

                try
                {
                    Server.Stop();

                    Console.WriteLine(@"服务器已关闭。");

                    if (s.EnableLogConsole)
                    {
                        Server.SessionLog -= Logger.Push;
                    }
                }
                finally
                {
                    Server.Dispose();
                }
            }
            else if (vsc.OnHttp)
            {
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public static void Stop()
        {
            ExitEvent.Set();
        }
    }
}
