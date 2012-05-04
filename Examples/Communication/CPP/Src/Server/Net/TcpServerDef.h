#pragma once

#include "Net/TcpSessionDef.h"

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/CancellationToken.h"
#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Optional.h"
#include "BaseSystem/AutoRelease.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <queue>
#include <unordered_set>
#include <unordered_map>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#ifdef __GNUC__
#include <boost/functional/hash.hpp>
#endif
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/thread.hpp>

namespace Communication
{
    namespace Net
    {
        template <typename TServer, typename TSession>
        class TcpServer : public std::enable_shared_from_this<TServer>
        {
        private:
            class BindingInfo;

            Communication::BaseSystem::LockedVariable<bool> IsRunningValue;

        private:
            template <typename T>
            struct SharedPtrHash
            {
                std::size_t operator() (const std::shared_ptr<T> &p) const
                {
                    return (std::size_t)(p.get());
                }
            };
            typedef std::unordered_set<std::shared_ptr<TSession>, SharedPtrHash<TSession>> TSessionSet;
            struct IpAddressHash
            {
                std::size_t operator() (const boost::asio::ip::address &p) const
                {
#ifdef __GNUC__
                    if (p.is_v4())
                    {
                        auto Bytes = p.to_v4().to_bytes();
                        auto a = (uint8_t (*)[sizeof(Bytes)])(Bytes.data());
                        return boost::hash<decltype(*a)>()(*a);
                    }
                    else if (p.is_v6())
                    {
                        auto Bytes = p.to_v6().to_bytes();
                        auto a = (uint8_t (*)[sizeof(Bytes)])(Bytes.data());
                        return boost::hash<decltype(*a)>()(*a);
                    }
                    else
                    {
                        auto s = p.to_string();
                        return boost::hash<decltype(s)>()(s);
                    }
#else
                    if (p.is_v4())
                    {
                        auto Bytes = p.to_v4().to_bytes();
                        auto a = (uint8_t (*)[sizeof(Bytes)])(Bytes.data());
                        return std::hash<decltype(*a)>()(*a);
                    }
                    else if (p.is_v6())
                    {
                        auto Bytes = p.to_v6().to_bytes();
                        auto a = (uint8_t (*)[sizeof(Bytes)])(Bytes.data());
                        return std::hash<decltype(*a)>()(*a);
                    }
                    else
                    {
                        auto s = p.to_string();
                        return std::hash<decltype(s)>()(s);
                    }
#endif
                }
            };
            typedef std::unordered_map<boost::asio::ip::address, int, IpAddressHash> TIpAddressMap;

        protected:
            boost::asio::io_service &IoService;

        private:
            std::vector<std::shared_ptr<BindingInfo>> BindingInfos;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>>> AcceptedSockets;
            std::shared_ptr<boost::thread> AcceptingTask;
            Communication::BaseSystem::CancellationToken AcceptingTaskToken;
            Communication::BaseSystem::AutoResetEvent AcceptingTaskNotifier;
            std::shared_ptr<boost::thread> PurifieringTask;
            Communication::BaseSystem::CancellationToken PurifieringTaskToken;
            Communication::BaseSystem::AutoResetEvent PurifieringTaskNotifier;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<TSessionSet>> Sessions;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<TIpAddressMap>> IpSessions;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<TSessionSet>> StoppingSessions;

            std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> BindingsValue;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> SessionIdleTimeoutValue;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> MaxConnectionsValue;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> MaxConnectionsPerIPValue;

        public:
            TcpServer(boost::asio::io_service &IoService);

            virtual ~TcpServer();

            bool IsRunning();

            std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> GetBindings() const;
            void SetBindings(std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> Bindings);

            std::shared_ptr<Communication::BaseSystem::Optional<int>> GetSessionIdleTimeout() const;
            void SetSessionIdleTimeout(std::shared_ptr<Communication::BaseSystem::Optional<int>> ms);

            std::shared_ptr<Communication::BaseSystem::Optional<int>> GetMaxConnections() const;
            void SetMaxConnections(std::shared_ptr<Communication::BaseSystem::Optional<int>> v);

            std::shared_ptr<Communication::BaseSystem::Optional<int>> GetMaxConnectionsPerIP() const;
            void SetMaxConnectionsPerIP(std::shared_ptr<Communication::BaseSystem::Optional<int>> v);

            void Start();

            virtual std::shared_ptr<TSession> CreateSession() = 0;

        private:
            void DoAccepting();

            void DoPurifiering();

            bool DoStopping(bool b);

        public:
            void Stop();

            void NotifySessionQuit(std::shared_ptr<TSession> s)
            {
                StoppingSessions.DoAction([&](std::shared_ptr<TSessionSet> &Sessions)
                {
                    Sessions->insert(s);
                });
                PurifieringTaskNotifier.Set();
            }

            std::function<void(std::shared_ptr<TSession>)> MaxConnectionsExceeded;
            std::function<void(std::shared_ptr<TSession>)> MaxConnectionsPerIPExceeded;
        };
    }
}
