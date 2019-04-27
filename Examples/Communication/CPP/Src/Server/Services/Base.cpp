#include "Services/ServerImplementation.h"

#include "BaseSystem/Times.h"
#include "BaseSystem/StringUtilities.h"

#include <memory>
#include <string>

using namespace Communication;
using namespace Server::Services;

/// <summary>服务器时间</summary>
std::shared_ptr<ServerTimeReply> ServerImplementation::ServerTime(std::shared_ptr<ServerTimeRequest> r)
{
    auto s = wideCharToUtf16(DateTimeUtcToString(UtcNow()));
    return ServerTimeReply::CreateSuccess(s);
}

/// <summary>退出</summary>
std::shared_ptr<QuitReply> ServerImplementation::Quit(std::shared_ptr<QuitRequest> r)
{
    SessionContext->RaiseQuit();
    return QuitReply::CreateSuccess();
}

/// <summary>检测类型结构版本</summary>
std::shared_ptr<CheckSchemaVersionReply> ServerImplementation::CheckSchemaVersion(std::shared_ptr<CheckSchemaVersionRequest> r)
{
    if (r->Hash == ServerContext->HeadCommunicationSchemaHash)
    {
        auto Lock = SessionContext->WriterLock();
        SessionContext->Version = u"";
        return CheckSchemaVersionReply::CreateHead();
    }
    auto ov = ServerContext->CommunicationSchemaHashToVersion(r->Hash);
    if (ov.OnHasValue())
    {
        auto Lock = SessionContext->WriterLock();
        SessionContext->Version = ov.Value();
        return CheckSchemaVersionReply::CreateSupported();
    }
    return CheckSchemaVersionReply::CreateNotSupported();
}
