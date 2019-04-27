#include "ServerContext.h"

#include "BaseSystem/StringUtilities.h"
#include "CommunicationBinary.h"
#include "Context/SerializationServerAdapter.h"
#include "Services/ServerImplementation.h"

namespace Server
{
    ServerContext::ServerContext()
        :
        SessionSet(std::unordered_set<std::shared_ptr<SessionContext>>()),
        EnableLogNormalInValue(false),
        EnableLogNormalOutValue(false),
        EnableLogUnknownErrorValue(false),
        EnableLogCriticalErrorValue(false),
        EnableLogPerformanceValue(false),
        EnableLogSystemValue(false),
        ServerDebugValue(false),
        ClientDebugValue(false)
    {
        Communication::Binary::BinarySerializationServer bss;
        HeadCommunicationSchemaHash = ToHexU16String(bss.Hash());
    }

    std::shared_ptr<IServerImplementation> ServerContext::CreateServerImplementation(std::shared_ptr<SessionContext> SessionContext)
    {
        auto si = std::make_shared<Server::Services::ServerImplementation>(this->shared_from_this(), SessionContext);
        return si;
    }
    std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IBinarySerializationServerAdapter>> ServerContext::CreateServerImplementationWithBinaryAdapter(std::shared_ptr<ISessionContext> SessionContext)
    {
        auto sc = std::dynamic_pointer_cast<class SessionContext>(SessionContext);
        if (sc == nullptr) { throw std::logic_error("InvalidOperationException"); }
        auto si = std::dynamic_pointer_cast<Server::Services::ServerImplementation>(CreateServerImplementation(sc));
        if (si == nullptr) { throw std::logic_error("InvalidOperationException"); }
        auto a = std::make_shared<BinarySerializationServerAdapter>(si);
        return std::make_pair(si, a);
    }
}
