#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include "Utility.h"

namespace Server
{
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
