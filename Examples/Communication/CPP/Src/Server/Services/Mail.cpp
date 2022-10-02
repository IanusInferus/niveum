#include "Services/ServerImplementation.h"

#include <memory>
#include <string>

using namespace Communication;
using namespace Server::Services;

/// <summary>获取用户信息</summary>
std::shared_ptr<GetUserProfileReply> ServerImplementation::GetUserProfile(std::shared_ptr<GetUserProfileRequest> r)
{
    return GetUserProfileReply::CreateNotExist();
}
