﻿#include "Services/ServerImplementation.h"

#include "BaseSystem/Times.h"

#include <memory>
#include <string>

using namespace Communication;
using namespace Server::Services;

/// <summary>服务器时间</summary>
std::shared_ptr<CommunicationDuplication::ServerTimeReply> ServerImplementation::CommunicationDuplicationDotServerTime(std::shared_ptr<CommunicationDuplication::ServerTimeRequest> r)
{
    auto s = DateTimeUtcToString(UtcNow());
    return CommunicationDuplication::ServerTimeReply::CreateSuccess(s);
}
