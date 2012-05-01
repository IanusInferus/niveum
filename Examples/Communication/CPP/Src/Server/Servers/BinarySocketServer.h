#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "Net/TcpServer.h"
#include "Net/TcpSession.h"
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
#include <boost/format.hpp>

namespace Server
{
    class BinarySocketSession;
    class BinarySocketServer : public Communication::Net::TcpServer<BinarySocketServer, BinarySocketSession>
    {
    private:
        std::shared_ptr<ServerImplementation> si;
    public:
        std::shared_ptr<Communication::Binary::BinaryServer<SessionContext>> InnerServer;
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
        int GetMaxBadCommands() const
        {
            return MaxBadCommandsValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetMaxBadCommands(int value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            MaxBadCommandsValue = value;
        }

        bool GetClientDebug() const
        {
            return ClientDebugValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetClientDebug(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            ClientDebugValue = value;
        }

        bool GetEnableLogNormalIn() const
        {
            return EnableLogNormalInValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogNormalIn(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            EnableLogNormalInValue = value;
        }

        bool GetEnableLogNormalOut() const
        {
            return EnableLogNormalOutValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogNormalOut(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            EnableLogNormalOutValue = value;
        }

        bool GetEnableLogUnknownError() const
        {
            return EnableLogUnknownErrorValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogUnknownError(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            EnableLogUnknownErrorValue = value;
        }

        bool GetEnableLogCriticalError() const
        {
            return EnableLogCriticalErrorValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogCriticalError(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            EnableLogCriticalErrorValue = value;
        }

        bool GetEnableLogPerformance() const
        {
            return EnableLogPerformanceValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogPerformance(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            EnableLogPerformanceValue = value;
        }

        bool GetEnableLogSystem() const
        {
            return EnableLogSystemValue;
        }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        void SetEnableLogSystem(bool value)
        {
            if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
            EnableLogSystemValue = value;
        }

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

        BinarySocketServer(boost::asio::io_service &IoService)
            : Communication::Net::TcpServer<BinarySocketServer, BinarySocketSession>(IoService),
              MaxBadCommandsValue(8),
              ClientDebugValue(false),
              EnableLogNormalInValue(true),
              EnableLogNormalOutValue(true),
              EnableLogUnknownErrorValue(true),
              EnableLogCriticalErrorValue(true),
              EnableLogPerformanceValue(true),
              EnableLogSystemValue(true),
              SessionMappings(std::make_shared<TSessionMapping>())
        {
            sc = std::make_shared<ServerContext>();
            sc->GetSessions = [&]() -> std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>
            {
                return SessionMappings.Check<std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>>([&](const std::shared_ptr<TSessionMapping> &Mappings) -> std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>
                {
                    auto l = std::make_shared<std::vector<std::shared_ptr<SessionContext>>>();
                    for (auto i = Mappings->begin(); i != Mappings->end(); i.operator ++())
                    {
                        l->push_back(std::get<0>(*i));
                    }
                    return l;
                });
            };
            si = std::make_shared<ServerImplementation>(sc);

            InnerServer = std::make_shared<Communication::Binary::BinaryServer<SessionContext>>(si);
            InnerServer->ServerEvent = [=](SessionContext &c, std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters) { OnServerEvent(c, CommandName, CommandHash, Parameters); };
            sc->SchemaHash = (boost::wformat(L"%16X") % InnerServer->Hash()).str();

            MaxConnectionsExceeded = [=](std::shared_ptr<BinarySocketSession> s) { OnMaxConnectionsExceeded(s); };
            MaxConnectionsPerIPExceeded = [=](std::shared_ptr<BinarySocketSession> s) { OnMaxConnectionsExceeded(s); };
        }

        void RaiseError(SessionContext &c, std::wstring CommandName, std::wstring Message)
        {
            si->RaiseError(c, CommandName, Message);
        }

        std::function<void(std::shared_ptr<SessionLogEntry>)> SessionLog;
        void RaiseSessionLog(std::shared_ptr<SessionLogEntry> Entry)
        {
            if (SessionLog != nullptr)
            {
                SessionLog(Entry);
            }
        }

    private:
        void OnServerEvent(SessionContext &c, std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters);

        void OnMaxConnectionsExceeded(std::shared_ptr<BinarySocketSession> s);

        void OnMaxConnectionsPerIPExceeded(std::shared_ptr<BinarySocketSession> s);
    };
}
