﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BaseSystem;
using Net;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class TcpServer : IServer
    {
        private class BindingInfo
        {
            public IPEndPoint EndPoint;
            public LockedVariable<Socket> Socket;
            public AsyncConsumer<SocketAsyncEventArgs> ListenConsumer;
            public Action Start;
        }

        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);

        private List<BindingInfo> BindingInfos = new List<BindingInfo>();
        private CancellationTokenSource ListeningTaskTokenSource;
        private AsyncConsumer<Socket> AcceptConsumer;
        private AsyncConsumer<TcpSession> PurifyConsumer;

        private class IpSessionInfo
        {
            public int Count = 0;
            public HashSet<TcpSession> Authenticated = new HashSet<TcpSession>();
        }
        private class ServerSessionSets
        {
            public HashSet<TcpSession> Sessions = new HashSet<TcpSession>();
            public Dictionary<IPAddress, IpSessionInfo> IpSessions = new Dictionary<IPAddress, IpSessionInfo>();
        }
        private LockedVariable<ServerSessionSets> SessionSets = new LockedVariable<ServerSessionSets>(new ServerSessionSets());

        public IServerContext ServerContext { get; private set; }
        private Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory;
        private Action<Action> QueueUserWorkItem;
        private Action<Action> PurifierQueueUserWorkItem;

        private int MaxBadCommandsValue = 8;
        private IPEndPoint[] BindingsValue = { };
        private int? SessionIdleTimeoutValue = null;
        private int? UnauthenticatedSessionIdleTimeoutValue = null;
        private int? MaxConnectionsValue = null;
        private int? MaxConnectionsPerIPValue = null;
        private int? MaxUnauthenticatedPerIPValue = null;

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int MaxBadCommands
        {
            get
            {
                return MaxBadCommandsValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxBadCommandsValue = value;
                    }
                );
            }
        }

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public IPEndPoint[] Bindings
        {
            get
            {
                return BindingsValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        BindingsValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? SessionIdleTimeout
        {
            get
            {
                return SessionIdleTimeoutValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        SessionIdleTimeoutValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? UnauthenticatedSessionIdleTimeout
        {
            get
            {
                return UnauthenticatedSessionIdleTimeoutValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        UnauthenticatedSessionIdleTimeoutValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxConnections
        {
            get
            {
                return MaxConnectionsValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxConnectionsValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxConnectionsPerIP
        {
            get
            {
                return MaxConnectionsPerIPValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxConnectionsPerIPValue = value;
                    }
                );
            }
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxUnauthenticatedPerIP
        {
            get
            {
                return MaxUnauthenticatedPerIPValue;
            }
            set
            {
                IsRunningValue.DoAction
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }
                        MaxUnauthenticatedPerIPValue = value;
                    }
                );
            }
        }

        public LockedVariable<Dictionary<ISessionContext, TcpSession>> SessionMappings = new LockedVariable<Dictionary<ISessionContext, TcpSession>>(new Dictionary<ISessionContext, TcpSession>());

        public TcpServer(IServerContext sc, Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory, Action<Action> QueueUserWorkItem, Action<Action> PurifierQueueUserWorkItem)
        {
            ServerContext = sc;
            this.VirtualTransportServerFactory = VirtualTransportServerFactory;
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.PurifierQueueUserWorkItem = PurifierQueueUserWorkItem;
        }

        private void OnMaxConnectionsExceeded(TcpSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections, please try again later.");
            }
        }
        private void OnMaxConnectionsPerIPExceeded(TcpSession s)
        {
            if (s != null && s.IsRunning)
            {
                s.RaiseError("", "Client host rejected: too many connections from your IP(" + s.RemoteEndPoint.Address + "), please try again later.");
            }
        }

        public void Start()
        {
            var Success = false;

            try
            {
                IsRunningValue.Update
                (
                    b =>
                    {
                        if (b) { throw new InvalidOperationException(); }

                        if (BindingsValue.Length == 0)
                        {
                            throw new Exception("NoValidBinding");
                        }

                        ListeningTaskTokenSource = new CancellationTokenSource();

                        var ListeningTaskToken = ListeningTaskTokenSource.Token;

                        Action<TcpSession> Purify = StoppingSession =>
                        {
                            SessionSets.DoAction
                            (
                                ss =>
                                {
                                    if (ss.Sessions.Contains(StoppingSession))
                                    {
                                        ss.Sessions.Remove(StoppingSession);
                                        var IpAddress = StoppingSession.RemoteEndPoint.Address;
                                        var isi = ss.IpSessions[IpAddress];
                                        if (isi.Authenticated.Contains(StoppingSession))
                                        {
                                            isi.Authenticated.Remove(StoppingSession);
                                        }
                                        isi.Count -= 1;
                                        if (isi.Count == 0)
                                        {
                                            ss.IpSessions.Remove(IpAddress);
                                        }
                                    }
                                }
                            );
                            StoppingSession.Dispose();
                        };

                        Action<Socket> Accept = a =>
                        {
                            IPEndPoint ep;
                            try
                            {
                                ep = (IPEndPoint)(a.RemoteEndPoint);
                            }
                            catch
                            {
                                a.Dispose();
                                return;
                            }
                            var s = new TcpSession(this, new StreamedAsyncSocket(a, UnauthenticatedSessionIdleTimeoutValue, QueueUserWorkItem), ep, VirtualTransportServerFactory, QueueUserWorkItem);

                            if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                            {
                                PurifyConsumer.DoOne();
                            }
                            if (MaxConnectionsValue.HasValue && (SessionSets.Check(ss => ss.Sessions.Count) >= MaxConnectionsValue.Value))
                            {
                                try
                                {
                                    s.Start();
                                    OnMaxConnectionsExceeded(s);
                                }
                                finally
                                {
                                    s.Dispose();
                                }
                                return;
                            }

                            if (MaxConnectionsPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(ep.Address) ? ss.IpSessions[ep.Address].Count : 0) >= MaxConnectionsPerIPValue.Value))
                            {
                                try
                                {
                                    s.Start();
                                    OnMaxConnectionsPerIPExceeded(s);
                                }
                                finally
                                {
                                    PurifyConsumer.Push(s);
                                }
                                return;
                            }

                            if (MaxUnauthenticatedPerIPValue.HasValue && (SessionSets.Check(ss => ss.IpSessions.ContainsKey(ep.Address) ? ss.IpSessions[ep.Address].Count : 0) >= MaxUnauthenticatedPerIPValue.Value))
                            {
                                try
                                {
                                    s.Start();
                                    OnMaxConnectionsPerIPExceeded(s);
                                }
                                finally
                                {
                                    PurifyConsumer.Push(s);
                                }
                                return;
                            }

                            SessionSets.DoAction
                            (
                                ss =>
                                {
                                    ss.Sessions.Add(s);
                                    if (ss.IpSessions.ContainsKey(ep.Address))
                                    {
                                        ss.IpSessions[ep.Address].Count += 1;
                                    }
                                    else
                                    {
                                        var isi = new IpSessionInfo();
                                        isi.Count += 1;
                                        ss.IpSessions.Add(ep.Address, isi);
                                    }
                                }
                            );

                            s.Start();
                        };
                        AcceptConsumer = new AsyncConsumer<Socket>(QueueUserWorkItem, a => { Accept(a); return true; }, int.MaxValue);

                        var Exceptions = new List<Exception>();
                        foreach (var Binding in BindingsValue)
                        {
                            Func<Socket> CreateSocket = () =>
                            {
                                var s = new Socket(Binding.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                                return s;
                            };

                            var Socket = CreateSocket();

                            try
                            {
                                Socket.Bind(Binding);
                            }
                            catch (SocketException ex)
                            {
                                Exceptions.Add(ex);
                                continue;
                            }
                            Socket.Listen(MaxConnectionsValue.HasValue ? (MaxConnectionsValue.Value + 1) : 128);

                            var BindingInfo = new BindingInfo();
                            BindingInfo.EndPoint = Binding;
                            BindingInfo.Socket = new LockedVariable<Socket>(Socket);
                            Func<SocketAsyncEventArgs, Boolean> Completed = args =>
                            {
                                try
                                {
                                    if (ListeningTaskToken.IsCancellationRequested) { return false; }
                                    if (args.SocketError == SocketError.Success)
                                    {
                                        var a = args.AcceptSocket;
                                        AcceptConsumer.Push(a);
                                    }
                                    else
                                    {
                                        BindingInfo.Socket.Update
                                        (
                                            OriginalSocket =>
                                            {
                                                try
                                                {
                                                    OriginalSocket.Dispose();
                                                }
                                                catch (Exception)
                                                {
                                                }
                                                var NewSocket = CreateSocket();
                                                NewSocket.Bind(Binding);
                                                NewSocket.Listen(MaxConnectionsValue.HasValue ? (MaxConnectionsValue.Value + 1) : 128);
                                                return NewSocket;
                                            }
                                        );
                                    }
                                }
                                finally
                                {
                                    args.Dispose();
                                }
                                BindingInfo.Start();
                                return true;
                            };
                            BindingInfo.ListenConsumer = new AsyncConsumer<SocketAsyncEventArgs>(QueueUserWorkItem, Completed, 1);
                            BindingInfo.Start = () =>
                            {
                                var EventArgs = new SocketAsyncEventArgs();
                                var bs = BindingInfo.Socket.Check(s => s);
                                EventArgs.Completed += (o, args) =>
                                {
                                    if (ListeningTaskToken.IsCancellationRequested) { return; }
                                    BindingInfo.ListenConsumer.Push(args);
                                };
                                try
                                {
                                    if (!bs.AcceptAsync(EventArgs))
                                    {
                                        BindingInfo.ListenConsumer.Push(EventArgs);
                                    }
                                }
                                catch (ObjectDisposedException)
                                {
                                }
                            };

                            BindingInfos.Add(BindingInfo);
                        }
                        if (BindingInfos.Count == 0)
                        {
                            throw new AggregateException(Exceptions);
                        }

                        PurifyConsumer = new AsyncConsumer<TcpSession>(PurifierQueueUserWorkItem, s => { Purify(s); return true; }, int.MaxValue);

                        foreach (var BindingInfo in BindingInfos)
                        {
                            BindingInfo.Start();
                        }

                        Success = true;

                        return true;
                    }
                );
            }
            finally
            {
                if (!Success)
                {
                    Stop();
                }
            }
        }

        public void Stop()
        {
            IsRunningValue.Update
            (
                b =>
                {
                    if (!b) { return false; }

                    if (ListeningTaskTokenSource != null)
                    {
                        ListeningTaskTokenSource.Cancel();
                    }
                    foreach (var BindingInfo in BindingInfos)
                    {
                        BindingInfo.Socket.DoAction
                        (
                            Socket =>
                            {
                                try
                                {
                                    Socket.Dispose();
                                }
                                catch (Exception)
                                {
                                }
                            }
                        );
                    }
                    foreach (var BindingInfo in BindingInfos)
                    {
                        BindingInfo.ListenConsumer.Dispose();
                    }
                    BindingInfos.Clear();
                    if (ListeningTaskTokenSource != null)
                    {
                        ListeningTaskTokenSource = null;
                    }

                    if (AcceptConsumer != null)
                    {
                        AcceptConsumer.Dispose();
                        AcceptConsumer = null;
                    }

                    List<TcpSession> Sessions = null;
                    SessionSets.DoAction
                    (
                        ss =>
                        {
                            Sessions = ss.Sessions.ToList();
                            ss.Sessions.Clear();
                            ss.IpSessions.Clear();
                        }
                    );
                    foreach (var s in Sessions)
                    {
                        s.Dispose();
                    }

                    if (PurifyConsumer != null)
                    {
                        PurifyConsumer.Dispose();
                        PurifyConsumer = null;
                    }

                    return false;
                }
            );
        }

        public void NotifySessionQuit(TcpSession s)
        {
            PurifyConsumer.Push(s);
        }
        public void NotifySessionAuthenticated(TcpSession s)
        {
            var e = s.RemoteEndPoint;
            SessionSets.DoAction
            (
                ss =>
                {
                    if (ss.IpSessions.ContainsKey(e.Address))
                    {
                        var isi = ss.IpSessions[e.Address];
                        if (!isi.Authenticated.Contains(s))
                        {
                            isi.Authenticated.Add(s);
                        }
                    }
                }
            );
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
