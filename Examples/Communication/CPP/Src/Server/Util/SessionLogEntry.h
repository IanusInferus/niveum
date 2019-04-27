#pragma once

#include <string>
#include <chrono>

namespace Server
{
    class SessionLogEntry
    {
    public:
        std::u16string RemoteEndPoint;
        std::u16string Token;
        std::chrono::system_clock::time_point Time;
        std::u16string Type;
        std::u16string Name;
        std::u16string Message;
    };
}
