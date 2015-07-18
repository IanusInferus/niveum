#pragma once

#include <string>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <chrono>

namespace Server
{
    class SessionLogEntry
    {
    public:
        asio::ip::tcp::endpoint RemoteEndPoint;
        std::wstring Token;
        std::chrono::system_clock::time_point Time;
        std::wstring Type;
        std::wstring Name;
        std::wstring Message;
    };
}
