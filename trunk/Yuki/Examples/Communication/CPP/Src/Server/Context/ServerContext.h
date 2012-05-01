#pragma once

#include "SessionContext.h"

#include <vector>
#include <string>
#include <functional>
#include <memory>

namespace Server
{
    class ServerContext
    {
    public:
        std::function<std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>()> GetSessions;
        std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>> Sessions() { return GetSessions(); }
        std::wstring SchemaHash;
    };
}
