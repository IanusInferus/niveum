#include "Services/ServerImplementation.h"

#include "BaseSystem/Times.h"

#include <memory>
#include <string>

using namespace std;
using namespace Communication;
using namespace Server;

/// <summary>服务器时间</summary>
shared_ptr<ServerTimeReply> ServerImplementation::ServerTime(shared_ptr<ServerTimeRequest> r)
{
    auto s = DateTimeUtcToString(UtcNow());
    return ServerTimeReply::CreateSuccess(s);
}

/// <summary>退出</summary>
shared_ptr<QuitReply> ServerImplementation::Quit(shared_ptr<QuitRequest> r)
{
    c->RaiseQuit();
    return QuitReply::CreateSuccess();
}

/// <summary>检测类型结构版本</summary>
std::shared_ptr<CheckSchemaVersionReply> ServerImplementation::CheckSchemaVersion(std::shared_ptr<CheckSchemaVersionRequest> r)
{
    if (r->Hash == sc->HeadCommunicationSchemaHash)
    {
        return CheckSchemaVersionReply::CreateHead();
    }
    else
    {
        return CheckSchemaVersionReply::CreateNotSupported();
    }
}
