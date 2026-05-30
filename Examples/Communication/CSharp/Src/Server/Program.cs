//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.Examples <Visual C#>
//  Description: 聊天服务器
//  Version:     2019.09.10.
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
            var x = TreeFile.ReadFile("Configuration.tree");
            var c = (new XmlSerializer()).Read<Configuration>(x);
            return c;
        }

        public static void Run(Configuration c)
        {
            Console.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + @"  服务器进程启动。");

            var ProcessorCount = Environment.ProcessorCount;
            var WorkThreadCount = c.NumThread.OnSome ? Math.Max(1, c.NumThread.Value) : ProcessorCount;
            Console.WriteLine(@"逻辑处理器数量: " + ProcessorCount.ToString());
            Console.WriteLine(@"工作线程数量: {0}".Formats(WorkThreadCount));

            using (var tp = new CountedThreadPool("Worker", WorkThreadCount))
            using (var tpPurifier = new CountedThreadPool("Purifier", 2))
            using (var tpLog = new CountedThreadPool("Log", 1))
            using (var ExitEvent = new AutoResetEvent(false))
            using (var Logger = new ConsoleLogger(tpLog.QueueUserWorkItem))
            {
                Logger.Start();

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

                var ChatContexts = new List<ServerContext>();

                var ServerCloses = new List<Action>();

                try
                {
                    foreach (var s in c.Servers)
                    {
                        if (s.OnChat)
                        {
                            var ss = s.Chat;
                            var ServerContext = new ServerContext();
                            ChatContexts.Add(ServerContext);

                            ServerContext.EnableLogNormalIn = c.EnableLogNormalIn;
                            ServerContext.EnableLogNormalOut = c.EnableLogNormalOut;
                            ServerContext.EnableLogUnknownError = c.EnableLogUnknownError;
                            ServerContext.EnableLogCriticalError = c.EnableLogCriticalError;
                            ServerContext.EnableLogPerformance = c.EnableLogPerformance;
                            ServerContext.EnableLogSystem = c.EnableLogSystem;
                            ServerContext.EnableLogTransport = c.EnableLogTransport;
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

                            var Protocols = new List<IServer>();
                            var Factory = new TaskFactory(tp);
                            var PurifierFactory = new TaskFactory(tp);
                            foreach (var p in ss.Protocols)
                            {
                                if (System.Diagnostics.Debugger.IsAttached)
                                {
                                    Protocols.Add(StartProtocol(c, p, ServerContext, Factory, PurifierFactory));
                                }
                                else
                                {
                                    try
                                    {
                                        Protocols.Add(StartProtocol(c, p, ServerContext, Factory, PurifierFactory));
                                    }
                                    catch (Exception ex)
                                    {
                                        var Message = Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
                                        Console.WriteLine(Message);
                                        FileLoggerSync.WriteLog("Error.log", Message);
                                    }
                                }
                            }

                            ServerCloses.Add(() =>
                            {
                                foreach (var Session in ServerContext.Sessions.AsParallel())
                                {
                                    Session.SessionLock.EnterReadLock(); ;
                                    try
                                    {
                                        if (Session.EventPump != null)
                                        {
                                            Session.EventPump.ServerShutdown(new Communication.ServerShutdownEvent { });
                                        }
                                    }
                                    finally
                                    {
                                        Session.SessionLock.ExitReadLock();
                                    }
                                }

                                foreach (var p in Protocols)
                                {
                                    if (System.Diagnostics.Debugger.IsAttached)
                                    {
                                        StopProtocol(p);
                                    }
                                    else
                                    {
                                        try
                                        {
                                            StopProtocol(p);
                                        }
                                        catch (Exception ex)
                                        {
                                            var Message = Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + "\r\n" + ExceptionInfo.GetExceptionInfo(ex);
                                            Console.WriteLine(Message);
                                            FileLoggerSync.WriteLog("Error.log", Message);
                                        }
                                    }
                                }

                                if (c.EnableLogConsole)
                                {
                                    ServerContext.SessionLog -= Logger.Push;
                                }

                                Console.WriteLine(@"ChatServerContext.RequestCount = {0}".Formats(ServerContext.RequestCount));
                                Console.WriteLine(@"ChatServerContext.ReplyCount = {0}".Formats(ServerContext.ReplyCount));
                                Console.WriteLine(@"ChatServerContext.EventCount = {0}".Formats(ServerContext.EventCount));
                            });
                        }
                        else
                        {
                            throw new InvalidOperationException("未知服务器类型: " + s._Tag.ToString());
                        }
                    }

                    ExitEvent.WaitOne();
                    Console.CancelKeyPress -= CancelKeyPress;
                }
                finally
                {
                    foreach (var a in ServerCloses)
                    {
                        a();
                    }
                }
            }

            Console.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + @"  服务器进程退出完成。");
        }

        private static IServer StartProtocol(Configuration c, ChatProtocolConfiguration pc, ServerContext ServerContext, TaskFactory Factory, TaskFactory PurifierFactory)
        {
            if (pc.OnTcp)
            {
                var s = pc.Tcp;

                if (!(s.SerializationProtocolType == SerializationProtocolType.Binary || s.SerializationProtocolType == SerializationProtocolType.Json))
                {
                    throw new InvalidOperationException("未知协议类型: " + s.SerializationProtocolType.ToString());
                }

                Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory;
                if (s.SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithBinaryAdapter(Factory, Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new BinaryCountPacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else if (s.SerializationProtocolType == SerializationProtocolType.Json)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithJsonAdapter(Factory, Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new JsonLinePacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else
                {
                    throw new InvalidOperationException();
                }

                var Server = new TcpServer(ServerContext, VirtualTransportServerFactory, a => Factory.StartNew(a), a => PurifierFactory.StartNew(a));
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
            else if (pc.OnUdp)
            {
                var s = pc.Udp;

                if (!(s.SerializationProtocolType == SerializationProtocolType.Binary || s.SerializationProtocolType == SerializationProtocolType.Json))
                {
                    throw new InvalidOperationException("未知协议类型: " + s.SerializationProtocolType.ToString());
                }

                Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory;
                if (s.SerializationProtocolType == SerializationProtocolType.Binary)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithBinaryAdapter(Factory, Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new BinaryCountPacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else if (s.SerializationProtocolType == SerializationProtocolType.Json)
                {
                    VirtualTransportServerFactory = (Context, t) =>
                    {
                        var p = ServerContext.CreateServerImplementationWithJsonAdapter(Factory, Context);
                        var si = p.Key;
                        var a = p.Value;
                        var bcps = new JsonLinePacketServer(a, CommandName => true, t);
                        return new KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>(si, bcps);
                    };
                }
                else
                {
                    throw new InvalidOperationException();
                }

                var Server = new UdpServer(ServerContext, VirtualTransportServerFactory, a => Factory.StartNew(a), a => PurifierFactory.StartNew(a));
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
            else if (pc.OnHttp)
            {
                var s = pc.Http;

                Func<ISessionContext, KeyValuePair<IServerImplementation, IHttpVirtualTransportServer>> VirtualTransportServerFactory = Context =>
                {
                    var p = ServerContext.CreateServerImplementationWithJsonAdapter(Factory, Context);
                    var si = p.Key;
                    var a = p.Value;
                    var jhps = new JsonHttpPacketServer(a, CommandName => true);
                    return new KeyValuePair<IServerImplementation, IHttpVirtualTransportServer>(si, jhps);
                };

                var Server = new HttpServer(ServerContext, VirtualTransportServerFactory, a => Factory.StartNew(a), a => PurifierFactory.StartNew(a));
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
            else if (pc.OnHttpStatic)
            {
                var s = pc.HttpStatic;

                var Server = new StaticHttpServer(ServerContext, a => Factory.StartNew(a), 8 * 1024);
                var Success = false;

                try
                {
                    Server.Bindings = s.Bindings.Select(b => b.Prefix).ToArray();
                    Server.SessionIdleTimeout = Math.Min(s.SessionIdleTimeout, s.UnauthenticatedSessionIdleTimeout);
                    Server.MaxConnections = s.MaxConnections;
                    Server.MaxConnectionsPerIP = s.MaxConnectionsPerIP;
                    Server.MaxBadCommands = s.MaxBadCommands;
                    Server.MaxUnauthenticatedPerIP = s.MaxUnauthenticatedPerIP;

                    Server.ServiceVirtualPath = s.ServiceVirtualPath;
                    Server.PhysicalPath = s.PhysicalPath;
                    Server.Indices = s.Indices.Split(',').Select(Index => Index.Trim(' ')).Where(Index => Index != "").ToArray();

                    Server.Start();

                    Console.WriteLine(@"HTTP静态服务器已启动。结点: {0}".Formats(String.Join(", ", Server.Bindings.Select(b => b.ToString()))));

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
                throw new InvalidOperationException("未知服务器类型: " + pc._Tag.ToString());
            }
        }
        private static void StopProtocol(IServer Server)
        {
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
    }
}
