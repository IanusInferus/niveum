#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "BaseSystem/ThreadLocalVariable.h"
#include "Net/TcpSessionDef.h"
#include "Net/TcpServerDef.h"
#include "Util/SessionLogEntry.h"
#include "Context/ServerContext.h"
#include "Context/SessionContext.h"
#include "Services/ServerImplementation.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <unordered_map>
#include <string>
#include <exception>
#include <stdexcept>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/format.hpp>

namespace Server
{
    class BinarySocketSession;
    class BinarySocketServer : public Communication::Net::TcpServer<BinarySocketServer, BinarySocketSession>
    {
    private:
        class WorkPart
        {
        public:
            std::shared_ptr<ServerImplementation> si;
            std::shared_ptr<Communication::Binary::BinaryServer<SessionContext>> bs;
        };
        std::shared_ptr<Communication::BaseSystem::ThreadLocalVariable<WorkPart>> WorkPartInstance;
    public:
        std::shared_ptr<Communication::Binary::BinaryServer<SessionContext>> InnerServer();
        std::shared_ptr<ServerContext> sc;

    private:
        int MaxBadCommandsValue;
        bool ClientDebugValue;
        bool EnableLogNormalInValue;
        bool EnableLogNormalOutValue;
        bool EnableLogUnknownErrorValue;
        bool EnableLogCriticalErrorValue;
        bool EnableLogPerformanceValue;
        bool EnableLogSystemValue;
        
    public:
        std::shared_ptr<BinarySocketSession> CreateSession();

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

        template <typename T>
        struct SharedPtrHash
        {
            std::size_t operator() (const std::shared_ptr<T> &p) const
            {
                return (std::size_t)(p.get());
            }
        };
        typedef std::unordered_map<std::shared_ptr<SessionContext>, std::shared_ptr<BinarySocketSession>, SharedPtrHash<SessionContext>> TSessionMapping;
        Communication::BaseSystem::LockedVariable<std::shared_ptr<TSessionMapping>> SessionMappings;

        BinarySocketServer(boost::asio::io_service &IoService);

        void RaiseError(SessionContext &c, std::wstring CommandName, std::wstring Message);

        std::function<void(std::shared_ptr<SessionLogEntry>)> SessionLog;
        void RaiseSessionLog(std::shared_ptr<SessionLogEntry> Entry);

    private:
        void OnServerEvent(SessionContext &c, std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters);

        void OnMaxConnectionsExceeded(std::shared_ptr<BinarySocketSession> s);

        void OnMaxConnectionsPerIPExceeded(std::shared_ptr<BinarySocketSession> s);
    };
}
