//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 聊天服务器
//  Version:     2014.10.31.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
                    var Message = Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
                    Console.WriteLine(Message);
                    FileLoggerSync.WriteLog("Crash.log", Message);
                    return -1;
                }
            }
        }

        private static void CurrentDomain_UnhandledException(Object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)(e.ExceptionObject);
            var Message = Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex, null);
            Console.WriteLine(Message);
            FileLoggerSync.WriteLog("Crash.log", Message);
            Environment.Exit(-1);
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var ex = e.Exception;
            var Message = Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
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
            Console.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + @"  服务器进程启动。");

            var ProcessorCount = Environment.ProcessorCount;
            var WorkThreadCount = ProcessorCount;
            //增加后台执行功能使用的线程
            WorkThreadCount = Math.Max(1, Math.Min(WorkThreadCount, c.NumMaxThread));
            Console.WriteLine(@"逻辑处理器数量: " + ProcessorCount.ToString());
            Console.WriteLine(@"工作线程数量: {0}".Formats(WorkThreadCount));

            using (var tp = new CountedThreadPool("Worker", WorkThreadCount))
            using (var tpPurifier = new CountedThreadPool("Purifier", 2))
            using (var tpLog = new CountedThreadPool("Log", 1))
            using (var ExitEvent = new AutoResetEvent(false))
            {
                LockedVariable<ConsoleCancelEventHandler> CancelKeyPressInner = null;
                CancelKeyPressInner = new LockedVariable<ConsoleCancelEventHandler>((sender, e) =>
                {
                    CancelKeyPressInner.Update(v => { return null; });
                    e.Cancel = true;
                    Console.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + @"  命令行中断退出。");
                    ExitEvent.Set();
                });
                ConsoleCancelEventHandler CancelKeyPress = (sender, e) =>
                {
                    var f = CancelKeyPressInner.Check(v => v);
                    if (f == null) { return; }
                    f(sender, e);
                };
                Console.CancelKeyPress += CancelKeyPress;

                using (var Logger = new ConsoleLogger(tpLog.QueueUserWorkItem))
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
                            Console.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + @"  远程命令退出。");
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
                                ServerDict.Add(s, StartServer(c, s, ServerContext, tp.QueueUserWorkItem, tpPurifier.QueueUserWorkItem));
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
                                    ServerDict.Add(s, StartServer(c, s, ServerContext, tp.QueueUserWorkItem, tpPurifier.QueueUserWorkItem));
                                }
                                catch (Exception ex)
                                {
                                    var Message = Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
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

            Console.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + @"  服务器进程退出完成。");
        }

        private static IServer StartServer(Configuration c, VirtualServerConfiguration vsc, ServerContext ServerContext, Action<Action> QueueUserWorkItem, Action<Action> PurifierQueueUserWorkItem)
        {
            if (vsc.OnTcp)
            {
                var s = vsc.Tcp;

                if (!(s.SerializationProtocolType == SerializationProtocolType.Binary || s.SerializationProtocolType == SerializationProtocolType.Json))
                {
                    throw new InvalidOperationException("未知协议类型: " + s.SerializationProtocolType.ToString());
                }

                Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, Streamed<ServerContext>.IStreamedVirtualTransportServer>> VirtualTransportServerFactory;
                if (s.SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithBinaryAdapter(Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new Streamed<ServerContext>.BinaryCountPacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, Streamed<ServerContext>.IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else if (s.SerializationProtocolType == SerializationProtocolType.Json)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithJsonAdapter(Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new Streamed<ServerContext>.JsonLinePacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, Streamed<ServerContext>.IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else
                {
                    throw new InvalidOperationException();
                }

                var Server = new Streamed<ServerContext>.TcpServer(ServerContext, VirtualTransportServerFactory, QueueUserWorkItem, PurifierQueueUserWorkItem);
                var Success = false;

                try
                {
                    Server.Bindings = s.Bindings.Select(b => new IPEndPoint(IPAddress.Parse(b.IpAddress), b.Port)).ToArray();
                    Server.SessionIdleTimeout = s.SessionIdleTimeout;
                    Server.UnauthenticatedSessionIdleTimeout = s.UnauthenticatedSessionIdleTimeout;
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxUnauthenticatedPerIP = s.MaxUnauthenticatedPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;

                    Server.Start();

                    Console.WriteLine(@"TCP/{0}服务器已启动。结点: {1}".Formats(s.SerializationProtocolType.ToString(), String.Join(", ", Server.Bindings.Select(b => b.ToString() + "(TCP)"))));

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
            else if (vsc.OnUdp)
            {
                var s = vsc.Udp;

                if (!(s.SerializationProtocolType == SerializationProtocolType.Binary || s.SerializationProtocolType == SerializationProtocolType.Json))
                {
                    throw new InvalidOperationException("未知协议类型: " + s.SerializationProtocolType.ToString());
                }

                Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, Streamed<ServerContext>.IStreamedVirtualTransportServer>> VirtualTransportServerFactory;
                if (s.SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithBinaryAdapter(Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new Streamed<ServerContext>.BinaryCountPacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, Streamed<ServerContext>.IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else if (s.SerializationProtocolType == SerializationProtocolType.Json)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithJsonAdapter(Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new Streamed<ServerContext>.JsonLinePacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, Streamed<ServerContext>.IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else
                {
                    throw new InvalidOperationException();
                }

                var Server = new Streamed<ServerContext>.UdpServer(ServerContext, VirtualTransportServerFactory, QueueUserWorkItem, PurifierQueueUserWorkItem);
                var Success = false;

                try
                {
                    Server.Bindings = s.Bindings.Select(b => new IPEndPoint(IPAddress.Parse(b.IpAddress), b.Port)).ToArray();
                    Server.SessionIdleTimeout = s.SessionIdleTimeout;
                    Server.UnauthenticatedSessionIdleTimeout = s.UnauthenticatedSessionIdleTimeout;
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxUnauthenticatedPerIP = s.MaxUnauthenticatedPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;

                    Server.TimeoutCheckPeriod = s.TimeoutCheckPeriod;

                    Server.Start();

                    Console.WriteLine(@"UDP/{0}服务器已启动。结点: {1}".Formats(s.SerializationProtocolType.ToString(), String.Join(", ", Server.Bindings.Select(b => b.ToString() + "(UDP)"))));

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

                Func<ISessionContext, KeyValuePair<IServerImplementation, Http<ServerContext>.IHttpVirtualTransportServer>> VirtualTransportServerFactory = Context =>
                {
                    var p = ServerContext.CreateServerImplementationWithJsonAdapter(Context);
                    var si = p.Key;
                    var a = p.Value;
                    var jhps = new Http<ServerContext>.JsonHttpPacketServer(a, CommandName => true);
                    return new KeyValuePair<IServerImplementation, Http<ServerContext>.IHttpVirtualTransportServer>(si, jhps);
                };

                var Server = new Http<ServerContext>.HttpServer(ServerContext, VirtualTransportServerFactory, QueueUserWorkItem, PurifierQueueUserWorkItem);
                var Success = false;

                try
                {
                    Server.Bindings = s.Bindings.Select(b => b.Prefix).ToArray();
                    Server.SessionIdleTimeout = s.SessionIdleTimeout;
                    Server.UnauthenticatedSessionIdleTimeout = s.UnauthenticatedSessionIdleTimeout;
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxUnauthenticatedPerIP = s.MaxUnauthenticatedPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;

                    Server.TimeoutCheckPeriod = s.TimeoutCheckPeriod;
                    Server.ServiceVirtualPath = s.ServiceVirtualPath;

                    Server.Start();

                    Console.WriteLine(@"HTTP/{0}服务器已启动。结点: {1}".Formats("Json", String.Join(", ", Server.Bindings.Select(b => b.ToString()))));

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

                    Console.WriteLine(@"TCP/{0}服务器已关闭。".Formats(s.SerializationProtocolType.ToString()));
                }
                finally
                {
                    Server.Dispose();
                }
            }
            else if (vsc.OnUdp)
            {
                var s = vsc.Udp;

                try
                {
                    Server.Stop();

                    Console.WriteLine(@"UDP/{0}服务器已关闭。".Formats(s.SerializationProtocolType.ToString()));
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

                    Console.WriteLine(@"HTTP/{0}服务器已关闭。".Formats("Json"));
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
