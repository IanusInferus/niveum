#pragma once

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/AsyncConsumer.h"
#include "BaseSystem/CancellationToken.h"
#include "BaseSystem/Optional.h"
#include "Concept.h"
#include "IContext.h"
#include "StreamedServer.h"

#include <vector>
#include <unordered_set>
#include <unordered_map>
#include <functional>
#include <utility>
#include <memory>
#include <stdexcept>
#include <asio.hpp>
#include <asio/steady_timer.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Server
{
    class TcpSession;

    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    class TcpServer : IServer
    {
    private:
        class AcceptResult
        {
        public:
            asio::error_code ec;
            std::shared_ptr<asio::ip::tcp::socket> AcceptSocket;
        };

        class BindingInfo
        {
        public:
            asio::ip::tcp::endpoint EndPoint;
            std::shared_ptr<BaseSystem::LockedVariable<std::shared_ptr<asio::ip::tcp::acceptor>>> Socket;
            std::shared_ptr<BaseSystem::AsyncConsumer<std::shared_ptr<AcceptResult>>> ListenConsumer;
            std::function<void()> Start;
        };

        BaseSystem::LockedVariable<bool> IsRunningValue;

    private:
        class IpSessionInfo
        {
        public:
            int Count;
            std::unordered_set<std::shared_ptr<TcpSession>> Authenticated;

            IpSessionInfo()
                : Count(0)
            {
            }
        };

        asio::io_service &IoService;

        std::vector<std::shared_ptr<BindingInfo>> BindingInfos;
        std::shared_ptr<BaseSystem::CancellationToken> ListeningTaskToken;
        std::shared_ptr<BaseSystem::AsyncConsumer<std::shared_ptr<asio::ip::tcp::socket>>> AcceptConsumer;
        std::shared_ptr<BaseSystem::AsyncConsumer<std::shared_ptr<TcpSession>>> PurifyConsumer;
        std::shared_ptr<asio::steady_timer> LastActiveTimeCheckTimer;

        struct IpAddressHash
        {
            template <class T>
            static inline void hash_combine(std::size_t& seed, const T& v)
            {
                std::hash<T> hasher;
                seed ^= hasher(v) + 0x9e3779b9 + (seed << 6) + (seed >> 2);
            }

            std::size_t operator() (const asio::ip::address &p) const
            {
                if (p.is_v4())
                {
                    auto Bytes = p.to_v4().to_bytes();
                    std::size_t s = 0;
                    for (auto b : Bytes)
                    {
                        hash_combine(s, b);
                    }
                    return s;
                }
                else if (p.is_v6())
                {
                    auto Bytes = p.to_v6().to_bytes();
                    std::size_t s = 0;
                    for (auto b : Bytes)
                    {
                        hash_combine(s, b);
                    }
                    return s;
                }
                else
                {
                    auto s = p.to_string();
                    return std::hash<decltype(s)>()(s);
                }
            }
        };

        class ServerSessionSets
        {
        public:
            std::unordered_set<std::shared_ptr<TcpSession>> Sessions;
            std::unordered_map<asio::ip::address, std::shared_ptr<IpSessionInfo>, IpAddressHash> IpSessions;
        };
        BaseSystem::LockedVariable<std::shared_ptr<ServerSessionSets>> SessionSets;

    public:
        std::shared_ptr<IServerContext> ServerContext() const
        {
            return ServerContextValue;
        }
    private:
        std::shared_ptr<IServerContext> ServerContextValue;
        void ServerContext(std::shared_ptr<IServerContext> Value)
        {
            ServerContextValue = Value;
        }

        std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory;
        std::function<void(std::function<void()>)> QueueUserWorkItem;
        std::function<void(std::function<void()>)> PurifierQueueUserWorkItem;

        int MaxBadCommandsValue;
        std::shared_ptr<std::vector<asio::ip::tcp::endpoint>> BindingsValue;
        Optional<int> SessionIdleTimeoutValue;
        Optional<int> UnauthenticatedSessionIdleTimeoutValue;
        Optional<int> MaxConnectionsValue;
        Optional<int> MaxConnectionsPerIPValue;
        Optional<int> MaxUnauthenticatedPerIPValue;
        int TimeoutCheckPeriodValue;

    public:
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        int MaxBadCommands() const
        {
            return MaxBadCommandsValue;
        }
        void MaxBadCommands(int value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxBadCommandsValue = value;
            });
        }

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        std::shared_ptr<std::vector<asio::ip::tcp::endpoint>> Bindings() const
        {
            return BindingsValue;
        }
        void Bindings(std::shared_ptr<std::vector<asio::ip::tcp::endpoint>> value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                BindingsValue = value;
            });
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        Optional<int> SessionIdleTimeout() const
        {
            return SessionIdleTimeoutValue;
        }
        void SessionIdleTimeout(Optional<int> value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                SessionIdleTimeoutValue = value;
            });
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        Optional<int> UnauthenticatedSessionIdleTimeout() const
        {
            return UnauthenticatedSessionIdleTimeoutValue;
        }
        void UnauthenticatedSessionIdleTimeout(Optional<int> value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                UnauthenticatedSessionIdleTimeoutValue = value;
            });
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        Optional<int> MaxConnections() const
        {
            return MaxConnectionsValue;
        }
        void MaxConnections(Optional<int> value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxConnectionsValue = value;
            });
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        Optional<int> MaxConnectionsPerIP() const
        {
            return MaxConnectionsPerIPValue;
        }
        void MaxConnectionsPerIP(Optional<int> value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxConnectionsPerIPValue = value;
            });
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        Optional<int> MaxUnauthenticatedPerIP() const
        {
            return MaxUnauthenticatedPerIPValue;
        }
        void MaxUnauthenticatedPerIP(Optional<int> value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxUnauthenticatedPerIPValue = value;
            });
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        int TimeoutCheckPeriod() const
        {
            return TimeoutCheckPeriodValue;
        }
        void TimeoutCheckPeriod(int value)
        {
            IsRunningValue.DoAction([=](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                TimeoutCheckPeriodValue = value;
            });
        }

        BaseSystem::LockedVariable<std::shared_ptr<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<TcpSession>>>> SessionMappings;

        TcpServer(asio::io_service &IoService, std::shared_ptr<IServerContext> sc, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem, std::function<void(std::function<void()>)> PurifierQueueUserWorkItem);

    private:
        void OnMaxConnectionsExceeded(std::shared_ptr<TcpSession> s);
        void OnMaxConnectionsPerIPExceeded(std::shared_ptr<TcpSession> s);
        void DoTimeoutCheck();

    public:
        void Start();
        void Stop();

        void NotifySessionQuit(std::shared_ptr<TcpSession> s);
        void NotifySessionAuthenticated(std::shared_ptr<TcpSession> s);

        ~TcpServer();
    };
}
