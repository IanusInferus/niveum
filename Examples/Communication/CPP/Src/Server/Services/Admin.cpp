#include "Services/ServerImplementation.h"

#include <memory>

using namespace Communication;
using namespace Server;

/// <summary>关闭服务器</summary>
std::shared_ptr<ShutdownReply> ServerImplementation::Shutdown(std::shared_ptr<ShutdownRequest> r)
{
    ServerContext->RaiseShutdown();
    return ShutdownReply::CreateSuccess();
}
