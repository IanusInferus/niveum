#pragma once

#include <string>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/date_time/posix_time/posix_time.hpp>

namespace Server
{
    class SessionLogEntry
    {
    public:
        boost::asio::ip::tcp::endpoint RemoteEndPoint;
        std::wstring Token;
        boost::posix_time::ptime Time;
        std::wstring Type;
        std::wstring Message;
    };
}
