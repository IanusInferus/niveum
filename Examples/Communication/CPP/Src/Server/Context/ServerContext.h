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
        ServerContext();

        std::wstring HeadCommunicationSchemaHash;

        std::function<void()> Shutdown; //跨线程事件(订阅者需要保证线程安全)
        void RaiseShutdown()
        {
            if (Shutdown != nullptr) { Shutdown(); }
        }

        std::function<std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>()> GetSessions;
        std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>> Sessions() { return GetSessions(); }
    };
}
