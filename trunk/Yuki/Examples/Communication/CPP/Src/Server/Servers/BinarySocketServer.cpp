#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include "Utility.h"

namespace Server
{
    std::shared_ptr<Communication::Binary::BinaryServer<SessionContext>> BinarySocketServer::InnerServer() { return WorkPartInstance->Value().bs; }

    std::shared_ptr<Communication::Net::TcpSession> BinarySocketServer::CreateSession()
    {
        auto s = std::make_shared<BinarySocketSession>(IoService);
        s->Server = this->shared_from_this();
        return s;
    }

    int BinarySocketServer::GetMaxBadCommands() const
    {
        return MaxBadCommandsValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetMaxBadCommands(int value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        MaxBadCommandsValue = value;
    }

    bool BinarySocketServer::GetClientDebug() const
    {
        return ClientDebugValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetClientDebug(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        ClientDebugValue = value;
    }

    bool BinarySocketServer::GetEnableLogNormalIn() const
    {
        return EnableLogNormalInValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogNormalIn(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogNormalInValue = value;
    }

    bool BinarySocketServer::GetEnableLogNormalOut() const
    {
        return EnableLogNormalOutValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogNormalOut(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogNormalOutValue = value;
    }

    bool BinarySocketServer::GetEnableLogUnknownError() const
    {
        return EnableLogUnknownErrorValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogUnknownError(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogUnknownErrorValue = value;
    }

    bool BinarySocketServer::GetEnableLogCriticalError() const
    {
        return EnableLogCriticalErrorValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogCriticalError(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogCriticalErrorValue = value;
    }

    bool BinarySocketServer::GetEnableLogPerformance() const
    {
        return EnableLogPerformanceValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogPerformance(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogPerformanceValue = value;
    }

    bool BinarySocketServer::GetEnableLogSystem() const
    {
        return EnableLogSystemValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogSystem(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogSystemValue = value;
    }

    BinarySocketServer::BinarySocketServer(boost::asio::io_service &IoService)
        : Communication::Net::TcpServer(IoService),
          WorkPartInstance(nullptr),
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

        auto OnServerEventHandler = [=](SessionContext &c, std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters) { OnServerEvent(c, CommandName, CommandHash, Parameters); };

        WorkPartInstance = std::make_shared<Communication::BaseSystem::ThreadLocalVariable<WorkPart>>([=]() -> WorkPart *
        {
            auto si = std::make_shared<ServerImplementation>(sc);

            auto srv = std::make_shared<Communication::Binary::BinaryServer<SessionContext>>(si);
            srv->ServerEvent = OnServerEventHandler;

            auto i = new WorkPart();
            i->si = si;
            i->bs = srv;

            return i;
        });
        sc->SchemaHash = (boost::wformat(L"%16X") % InnerServer()->Hash()).str();

        MaxConnectionsExceeded = [=](std::shared_ptr<Communication::Net::TcpSession> s) { OnMaxConnectionsExceeded(std::static_pointer_cast<BinarySocketSession>(s)); };
        MaxConnectionsPerIPExceeded = [=](std::shared_ptr<Communication::Net::TcpSession> s) { OnMaxConnectionsExceeded(std::static_pointer_cast<BinarySocketSession>(s)); };
    }

    void BinarySocketServer::RaiseError(SessionContext &c, std::wstring CommandName, std::wstring Message)
    {
        WorkPartInstance->Value().si->RaiseError(c, CommandName, Message);
    }

    void BinarySocketServer::RaiseSessionLog(std::shared_ptr<SessionLogEntry> Entry)
    {
        if (SessionLog != nullptr)
        {
            SessionLog(Entry);
        }
    }

    void BinarySocketServer::OnServerEvent(SessionContext &c, std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
    {
        std::shared_ptr<BinarySocketSession> Session = SessionMappings.Check<std::shared_ptr<BinarySocketSession>>([&](const std::shared_ptr<TSessionMapping> &Mappings) -> std::shared_ptr<BinarySocketSession>
        {
            auto cc = c.shared_from_this();
            if (Mappings->count(cc) > 0)
            {
                return (*Mappings)[cc];
            }
            else
            {
                return nullptr;
            }
        });

        if (Session != nullptr)
        {
            Session->WriteCommand(CommandName, CommandHash, Parameters);
        }
    }

    void BinarySocketServer::OnMaxConnectionsExceeded(std::shared_ptr<BinarySocketSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(L"", L"Client host rejected: too many connections, please try again later.");
        }
    }

    void BinarySocketServer::OnMaxConnectionsPerIPExceeded(std::shared_ptr<BinarySocketSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(L"", L"Client host rejected: too many connections from your IP(" + s2w(s->RemoteEndPoint.address().to_string()) + L"), please try again later.");
        }
    }
}
