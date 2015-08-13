#pragma once

#include <string>
#include <chrono>

namespace Server
{
    class SessionLogEntry
    {
    public:
        std::wstring RemoteEndPoint;
        std::wstring Token;
        std::chrono::system_clock::time_point Time;
        std::wstring Type;
        std::wstring Name;
        std::wstring Message;
    };
}
