#pragma once

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/CancellationToken.h"
#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Optional.h"
#include "BaseSystem/AutoRelease.h"
#include "Communication.h"
#include "UtfEncoding.h"
#include "CommunicationBinary.h"
#include "Util/SessionLogEntry.h"
#include "Context/ServerContext.h"
#include "Context/SessionContext.h"

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
#include <thread>
#include <boost/functional/hash.hpp>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/format.hpp>

namespace Server
{
    class BinarySocketSession;
    class BinarySocketServer : public std::enable_shared_from_this<BinarySocketServer>
    {
    private:
        class BindingInfo;

        BaseSystem::LockedVariable<bool> IsRunningValue;

    private:
        template <typename T>
        struct SharedPtrHash
        {
            std::size_t operator() (const std::shared_ptr<T> &p) const
            {
                return (std::size_t)(p.get());
            }
        };
        typedef std::unordered_set<std::shared_ptr<BinarySocketSession>, SharedPtrHash<BinarySocketSession>> TSessionSet;
        struct IpAddressHash
        {
            std::size_t operator() (const asio::ip::address &p) const
            {
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
            }
        };
        typedef std::unordered_map<asio::ip::address, int, IpAddressHash> TIpAddressMap;

    protected:
        asio::io_service &IoService;

    private:
        std::vector<std::shared_ptr<BindingInfo>> BindingInfos;
        BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<asio::ip::tcp::socket>>>> AcceptedSockets;
        std::shared_ptr<std::thread> AcceptingTask;
        BaseSystem::CancellationToken AcceptingTaskToken;
        BaseSystem::AutoResetEvent AcceptingTaskNotifier;
        std::shared_ptr<std::thread> PurifieringTask;
        BaseSystem::CancellationToken PurifieringTaskToken;
        BaseSystem::AutoResetEvent PurifieringTaskNotifier;
        BaseSystem::LockedVariable<std::shared_ptr<TSessionSet>> Sessions;
        BaseSystem::LockedVariable<std::shared_ptr<TIpAddressMap>> IpSessions;
        BaseSystem::LockedVariable<std::shared_ptr<TSessionSet>> StoppingSessions;

        std::shared_ptr<std::vector<asio::ip::tcp::endpoint>> BindingsValue;
        Optional<int> SessionIdleTimeoutValue;
        Optional<int> MaxConnectionsValue;
        Optional<int> MaxConnectionsPerIPValue;

    public:
        BinarySocketServer(asio::io_service &IoService);

        virtual ~BinarySocketServer();

        bool IsRunning();

        std::shared_ptr<std::vector<asio::ip::tcp::endpoint>> GetBindings() const;
        void SetBindings(std::shared_ptr<std::vector<asio::ip::tcp::endpoint>> Bindings);

        Optional<int> GetSessionIdleTimeout() const;
        void SetSessionIdleTimeout(Optional<int> ms);

        Optional<int> GetMaxConnections() const;
        void SetMaxConnections(Optional<int> v);

        Optional<int> GetMaxConnectionsPerIP() const;
        void SetMaxConnectionsPerIP(Optional<int> v);

        void Start();

    private:
        void DoAccepting();

        bool Purify(std::shared_ptr<BinarySocketSession> StoppingSession);
        bool PurifyOneInSession();
        void DoPurifiering();

        bool DoStopping(bool b);

    public:
        void Stop();

        void NotifySessionQuit(std::shared_ptr<BinarySocketSession> s)
        {
            StoppingSessions.DoAction([&](std::shared_ptr<TSessionSet> &Sessions)
            {
                Sessions->insert(s);
            });
            PurifieringTaskNotifier.Set();
        }

    public:
        std::shared_ptr<ServerContext> sc;

    private:
        std::function<bool(std::shared_ptr<SessionContext>, std::wstring)> CheckCommandAllowedValue;
        std::function<void()> ShutdownValue;
        int MaxBadCommandsValue;
        bool ClientDebugValue;
        bool EnableLogNormalInValue;
        bool EnableLogNormalOutValue;
        bool EnableLogUnknownErrorValue;
        bool EnableLogCriticalErrorValue;
        bool EnableLogPerformanceValue;
        bool EnableLogSystemValue;
        
    public:
        std::function<bool(std::shared_ptr<SessionContext>, std::wstring)> GetCheckCommandAllowed() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetCheckCommandAllowed(std::function<bool(std::shared_ptr<SessionContext>, std::wstring)> value);

        std::function<void()> GetShutdown() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetShutdown(std::function<void()> value);

        int GetMaxBadCommands() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetMaxBadCommands(int value);

        bool GetClientDebug() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetClientDebug(bool value);

        bool GetEnableLogNormalIn() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogNormalIn(bool value);

        bool GetEnableLogNormalOut() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogNormalOut(bool value);

        bool GetEnableLogUnknownError() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogUnknownError(bool value);

        bool GetEnableLogCriticalError() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogCriticalError(bool value);

        bool GetEnableLogPerformance() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogPerformance(bool value);

        bool GetEnableLogSystem() const;
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogSystem(bool value);

        typedef std::unordered_map<std::shared_ptr<SessionContext>, std::shared_ptr<BinarySocketSession>, SharedPtrHash<SessionContext>> TSessionMapping;
        BaseSystem::LockedVariable<std::shared_ptr<TSessionMapping>> SessionMappings;

        std::function<void(std::shared_ptr<SessionLogEntry>)> SessionLog;
        void RaiseSessionLog(std::shared_ptr<SessionLogEntry> Entry);

    private:
        void OnMaxConnectionsExceeded(std::shared_ptr<BinarySocketSession> s);

        void OnMaxConnectionsPerIPExceeded(std::shared_ptr<BinarySocketSession> s);
    };
}
