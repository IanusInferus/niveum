//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天服务器
//  Version:     2012.05.01.
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

            if (c.ProtocolType == ProtocolType.Binary)
            {
                using (var Server = new BinarySocketServer())
                {
                    using (var Logger = new ConsoleLogger())
                    {
                        if (c.EnableLogConsole)
                        {
                            Logger.Start
                            (
                                a => Server.SessionLog += a,
                                a => Server.SessionLog -= a
                            );
                        }

                        Server.Bindings = c.Bindings.Select(b => new IPEndPoint(IPAddress.Parse(b.IpAddress), b.Port)).ToArray();
                        Server.SessionIdleTimeout = c.SessionIdleTimeout;
                        Server.MaxConnections = c.MaxConnections;
                        Server.MaxConnectionsPerIP = c.MaxConnectionsPerIP;
                        Server.MaxBadCommands = c.MaxBadCommands;
                        Server.ClientDebug = c.ClientDebug;
                        Server.EnableLogNormalIn = c.EnableLogNormalIn;
                        Server.EnableLogNormalOut = c.EnableLogNormalOut;
                        Server.EnableLogUnknownError = c.EnableLogUnknownError;
                        Server.EnableLogCriticalError = c.EnableLogCriticalError;
                        Server.EnableLogPerformance = c.EnableLogPerformance;
                        Server.EnableLogSystem = c.EnableLogSystem;

                        Server.Start();

                        Console.WriteLine("服务器已启动。");
                        Console.WriteLine("协议类型：" + c.ProtocolType.ToString());
                        Console.WriteLine("服务结点: " + String.Join(", ", Server.Bindings.Select(b => b.ToString())));

                        ExitEvent.WaitOne();

                        Server.Stop();
                    }
                }
            }
            else if (c.ProtocolType == ProtocolType.Json)
            {
                using (var Server = new JsonSocketServer())
                {
                    using (var Logger = new ConsoleLogger())
                    {
                        if (c.EnableLogConsole)
                        {
                            Logger.Start
                            (
                                a => Server.SessionLog += a,
                                a => Server.SessionLog -= a
                            );
                        }

                        Server.Bindings = c.Bindings.Select(b => new IPEndPoint(IPAddress.Parse(b.IpAddress), b.Port)).ToArray();
                        Server.SessionIdleTimeout = c.SessionIdleTimeout;
                        Server.MaxConnections = c.MaxConnections;
                        Server.MaxConnectionsPerIP = c.MaxConnectionsPerIP;
                        Server.MaxBadCommands = c.MaxBadCommands;
                        Server.ClientDebug = c.ClientDebug;
                        Server.EnableLogNormalIn = c.EnableLogNormalIn;
                        Server.EnableLogNormalOut = c.EnableLogNormalOut;
                        Server.EnableLogUnknownError = c.EnableLogUnknownError;
                        Server.EnableLogCriticalError = c.EnableLogCriticalError;
                        Server.EnableLogPerformance = c.EnableLogPerformance;
                        Server.EnableLogSystem = c.EnableLogSystem;

                        Server.Start();

                        Console.WriteLine("服务器已启动。");
                        Console.WriteLine("协议类型：" + c.ProtocolType.ToString());
                        Console.WriteLine("服务结点: " + String.Join(", ", Server.Bindings.Select(b => b.ToString())));

                        ExitEvent.WaitOne();

                        Server.Stop();
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("未知协议类型：" + c.ProtocolType.ToString());
            }
        }

        public static void Stop()
        {
            ExitEvent.Set();
        }
    }
}
