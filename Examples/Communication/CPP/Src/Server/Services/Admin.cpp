#include "Services/ServerImplementation.h"

#include <memory>

using namespace std;
using namespace Communication;
using namespace Server;

/// <summary>关闭服务器</summary>
shared_ptr<ShutdownReply> ServerImplementation::Shutdown(SessionContext &c, shared_ptr<ShutdownRequest> r)
{
    sc->RaiseShutdown();
    return ShutdownReply::CreateSuccess();
}
