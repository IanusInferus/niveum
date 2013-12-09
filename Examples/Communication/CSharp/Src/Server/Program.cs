//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天服务器
//  Version:     2013.12.09.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Net;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting.TreeFormat;
using BaseSystem;

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
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                try
                {
                    return MainInner();
                }
                catch (Exception ex)
                {
                    var Message = Times.DateTimeUtcToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
                    Console.WriteLine(Message);
                    FileLoggerSync.WriteLog("Crash.log", Message);
                    return -1;
                }
            }
        }

        private static void CurrentDomain_UnhandledException(Object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)(e.ExceptionObject);
            var Message = Times.DateTimeUtcToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex, null);
            Console.WriteLine(Message);
            FileLoggerSync.WriteLog("Crash.log", Message);
            Environment.Exit(-1);
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var ex = e.Exception;
            var Message = Times.DateTimeUtcToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
            Console.WriteLine(Message);
            FileLoggerSync.WriteLog("Crash.log", Message);
            Environment.Exit(-1);
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
            Run(c);
            return 0;
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

        public static void Run(Configuration c)
        {
            using (var ExitEvent = new AutoResetEvent(false))
            {
                var ProcessorCount = Environment.ProcessorCount;
                var MinWorkThreadCount = ProcessorCount + 1;
                var MaxWorkThreadCount = ProcessorCount * 2 + 1;
                var MinCompletionPortThreadCount = ProcessorCount + 1;
                var MaxCompletionPortThreadCount = ProcessorCount * 2 + 1;
                foreach (var s in c.Servers)
                {
                    if (s.OnTcp)
                    {
                        MinWorkThreadCount += 2 + s.Tcp.Bindings.Count();
                        MaxWorkThreadCount += 2 + s.Tcp.Bindings.Count();
                    }
                    else if (s.OnHttp)
                    {
                        MinWorkThreadCount += 3;
                        MaxWorkThreadCount += 3;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                ThreadPool.SetMinThreads(MinWorkThreadCount, MinCompletionPortThreadCount);
                ThreadPool.SetMaxThreads(MaxWorkThreadCount, MaxCompletionPortThreadCount);

                Console.WriteLine(@"逻辑处理器数量: " + ProcessorCount.ToString());
                Console.WriteLine(@"工作线程数量: [{0}-{1}]".Formats(MinWorkThreadCount, MaxWorkThreadCount));
                Console.WriteLine(@"完成端口线程数量: [{0}-{1}]".Formats(MinCompletionPortThreadCount, MaxCompletionPortThreadCount));

                ConsoleCancelEventHandler CancelKeyPress = null;
                CancelKeyPress = (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("命令行中断退出。");
                    ExitEvent.Set();
                };
                Console.CancelKeyPress += CancelKeyPress;

                using (var Logger = new ConsoleLogger())
                {
                    using (var ServerContext = new ServerContext())
                    {
                        ServerContext.EnableLogNormalIn = c.EnableLogNormalIn;
                        ServerContext.EnableLogNormalOut = c.EnableLogNormalOut;
                        ServerContext.EnableLogUnknownError = c.EnableLogUnknownError;
                        ServerContext.EnableLogCriticalError = c.EnableLogCriticalError;
                        ServerContext.EnableLogPerformance = c.EnableLogPerformance;
                        ServerContext.EnableLogSystem = c.EnableLogSystem;
                        ServerContext.ServerDebug = c.ServerDebug;
                        ServerContext.ClientDebug = c.ClientDebug;

                        ServerContext.Shutdown += () =>
                        {
                            Console.WriteLine("远程命令退出。");
                            ExitEvent.Set();
                        };
                        if (c.EnableLogConsole)
                        {
                            ServerContext.SessionLog += Logger.Push;
                        }

                        Logger.Start();

                        var ServerDict = new Dictionary<VirtualServerConfiguration, IServer>();

                        if (System.Diagnostics.Debugger.IsAttached)
                        {
                            foreach (var s in c.Servers)
                            {
                                ServerDict.Add(s, StartServer(c, s, ServerContext));
                            }

                            ExitEvent.WaitOne();
                            Console.CancelKeyPress -= CancelKeyPress;

                            foreach (var s in ServerContext.Sessions.AsParallel())
                            {
                                s.SessionLock.AcquireReaderLock(int.MaxValue);
                                try
                                {
                                    if (s.EventPump != null)
                                    {
                                        s.EventPump.ServerShutdown(new Communication.ServerShutdownEvent { });
                                    }
                                }
                                finally
                                {
                                    s.SessionLock.ReleaseLock();
                                }
                            }

                            foreach (var s in c.Servers)
                            {
                                if (ServerDict.ContainsKey(s))
                                {
                                    var Server = ServerDict[s];
                                    StopServer(c, s, Server);
                                }
                            }
                        }
                        else
                        {
                            foreach (var s in c.Servers)
                            {
                                try
                                {
                                    ServerDict.Add(s, StartServer(c, s, ServerContext));
                                }
                                catch (Exception ex)
                                {
                                    var Message = Times.DateTimeUtcToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
                                    Console.WriteLine(Message);
                                    FileLoggerSync.WriteLog("Error.log", Message);
                                }
                            }

                            ExitEvent.WaitOne();
                            Console.CancelKeyPress -= CancelKeyPress;

                            foreach (var s in ServerContext.Sessions.AsParallel())
                            {
                                s.SessionLock.AcquireReaderLock(int.MaxValue);
                                try
                                {
                                    if (s.EventPump != null)
                                    {
                                        s.EventPump.ServerShutdown(new Communication.ServerShutdownEvent { });
                                    }
                                }
                                finally
                                {
                                    s.SessionLock.ReleaseLock();
                                }
                            }

                            foreach (var s in c.Servers)
                            {
                                try
                                {
                                    if (ServerDict.ContainsKey(s))
                                    {
                                        var Server = ServerDict[s];
                                        StopServer(c, s, Server);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                                }
                            }
                        }


                        if (c.EnableLogConsole)
                        {
                            ServerContext.SessionLog -= Logger.Push;
                        }
                    }
                }
            }

            Console.WriteLine("服务器进程退出完成。");
        }

        private static IServer StartServer(Configuration c, VirtualServerConfiguration vsc, ServerContext ServerContext)
        {
            if (vsc.OnTcp)
            {
                var s = vsc.Tcp;

                if (!(s.SerializationProtocolType == SerializationProtocolType.Binary || s.SerializationProtocolType == SerializationProtocolType.Json))
                {
                    throw new InvalidOperationException("未知协议类型: " + s.SerializationProtocolType.ToString());
                }

                var Server = new Tcp<ServerContext>.TcpServer(ServerContext);
                var Success = false;

                try
                {
                    Server.SerializationProtocolType = s.SerializationProtocolType;

                    Server.CheckCommandAllowed = (sc, CommandName) =>
                    {
                        return true;
                    };

                    Server.Bindings = s.Bindings.Select(b => new IPEndPoint(IPAddress.Parse(b.IpAddress), b.Port)).ToArray();
                    Server.SessionIdleTimeout = s.SessionIdleTimeout;
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxUnauthenticatedPerIP = s.MaxUnauthenticatedPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;

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
                var s = vsc.Http;

                var Server = new Http<ServerContext>.HttpServer(ServerContext);
                var Success = false;

                try
                {
                    Server.CheckCommandAllowed = (sc, CommandName) =>
                    {
                        return true;
                    };

                    Server.Bindings = s.Bindings.Select(b => b.Prefix).ToArray();
                    Server.SessionIdleTimeout = s.SessionIdleTimeout;
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxUnauthenticatedPerIP = s.MaxUnauthenticatedPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;

                    Server.TimeoutCheckPeriod = s.TimeoutCheckPeriod;
                    Server.ServiceVirtualPath = s.ServiceVirtualPath;
                    Server.StaticContentPath = s.StaticContentPath;

                    Server.Start();

                    Console.WriteLine(@"HTTP服务器已启动。");
                    Console.WriteLine(@"序列化协议类型: Json");
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
            else
            {
                throw new InvalidOperationException("未知服务器类型: " + vsc._Tag.ToString());
            }
        }
        private static void StopServer(Configuration c, VirtualServerConfiguration vsc, IServer Server)
        {
            if (vsc.OnTcp)
            {
                var s = vsc.Tcp;

                try
                {
                    Server.Stop();

                    Console.WriteLine(@"服务器已关闭。");
                }
                finally
                {
                    Server.Dispose();
                }
            }
            else if (vsc.OnHttp)
            {
                var s = vsc.Http;

                try
                {
                    Server.Stop();

                    Console.WriteLine(@"服务器已关闭。");
                }
                finally
                {
                    Server.Dispose();
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
