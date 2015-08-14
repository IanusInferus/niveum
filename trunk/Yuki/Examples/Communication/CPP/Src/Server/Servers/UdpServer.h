#pragma once

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/AsyncConsumer.h"
#include "BaseSystem/CancellationToken.h"
#include "BaseSystem/Optional.h"
#include "Concept.h"
#include "IContext.h"
#include "StreamedServer.h"

#include <cstdint>
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
    class UdpSession;

    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// UDP数据包
    /// Packet ::= SessionId:Int32 Flag:UInt16 Index:UInt16 Verification:Int32 Inner:Byte*
    /// 所有数据均为little-endian
    /// SessionId，当INI存在时，为初始包，服务器收到包后分配SessionId，建议初始包的SessionId和Index均为0
    /// Flag，标记，1 ACK，表示Inner中包含确认收到的包索引，2 ENC，表示数据已加密，4 INI，表示初始化，8 AUX，表示从客户端发到服务器的包为没有数据的辅助确认包
    /// Index，序列号，当AUX存在时，必须为LowerIndex
    /// Verification，当ENC存在时，为Inner的MAC验证码，否则为CRC32验证码，其中HMAC的验证码的计算方式为
    ///     Key = SessionKey XOR SHA1(Flag :: Index)
    ///     MAC = HMAC(Key, SessionId :: Flag :: Index :: 0 :: Inner).Take(4)
    ///     HMAC = H((K XOR opad) :: H((K XOR ipad) :: Inner))
    ///     H = SHA1
    ///     opad = 0x5C
    ///     ipad = 0x36
    /// Inner ::= NumIndex:UInt16 LowerIndex:UInt16 Index:UInt16{NumIndex - 1} Payload:Byte*，当ACK存在时
    ///         |= Payload:Byte*
    /// </summary>
    class UdpServer : public IServer
    {
    private:
        class AcceptResult
        {
        public:
            asio::error_code ec;
            std::size_t BytesTransferred;
            asio::ip::udp::endpoint RemoteEndPoint;
        };

        class BindingInfo
        {
        public:
            asio::ip::udp::endpoint EndPoint;
            std::shared_ptr<BaseSystem::LockedVariable<std::shared_ptr<asio::ip::udp::socket>>> Socket;
            std::shared_ptr<std::vector<std::uint8_t>> ReadBuffer;
            std::shared_ptr<BaseSystem::AsyncConsumer<std::shared_ptr<AcceptResult>>> ListenConsumer;
            std::function<void()> Start;
        };

        BaseSystem::LockedVariable<bool> IsRunningValue;

    private:
        asio::io_service &IoService;

        std::vector<std::shared_ptr<BindingInfo>> BindingInfos;
        std::shared_ptr<BaseSystem::CancellationToken> ListeningTaskToken;
        class AcceptingInfo
        {
        public:
            std::shared_ptr<asio::ip::udp::socket> Socket;
            std::shared_ptr<std::vector<std::uint8_t>> ReadBuffer;
            asio::ip::udp::endpoint RemoteEndPoint;
        };
        std::shared_ptr<BaseSystem::AsyncConsumer<std::shared_ptr<AcceptingInfo>>> AcceptConsumer;
        std::shared_ptr<BaseSystem::AsyncConsumer<std::shared_ptr<UdpSession>>> PurifyConsumer;
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

        class IpSessionInfo
        {
        public:
            int Count;
            std::unordered_set<std::shared_ptr<UdpSession>> Authenticated;

            IpSessionInfo()
                : Count(0)
            {
            }
        };
        class ServerSessionSets
        {
        public:
            std::unordered_set<std::shared_ptr<UdpSession>> Sessions;
            std::unordered_map<asio::ip::address, std::shared_ptr<IpSessionInfo>, IpAddressHash> IpSessions;
            std::unordered_map<int, std::shared_ptr<UdpSession>> SessionIdToSession;
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
        std::shared_ptr<std::vector<asio::ip::udp::endpoint>> BindingsValue;
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
        std::shared_ptr<std::vector<asio::ip::udp::endpoint>> Bindings() const
        {
            return BindingsValue;
        }
        void Bindings(std::shared_ptr<std::vector<asio::ip::udp::endpoint>> value)
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

        BaseSystem::LockedVariable<std::shared_ptr<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<UdpSession>>>> SessionMappings;

        UdpServer(asio::io_service &IoService, std::shared_ptr<IServerContext> sc, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem, std::function<void(std::function<void()>)> PurifierQueueUserWorkItem);

    private:
        void OnMaxConnectionsExceeded(std::shared_ptr<UdpSession> s);
        void OnMaxConnectionsPerIPExceeded(std::shared_ptr<UdpSession> s);
        void DoTimeoutCheck();

    public:
        void Start();
        void Stop();

        void NotifySessionQuit(std::shared_ptr<UdpSession> s);
        void NotifySessionAuthenticated(std::shared_ptr<UdpSession> s);

        ~UdpServer();
    };
}
