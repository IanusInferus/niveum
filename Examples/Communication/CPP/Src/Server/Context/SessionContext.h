#pragma once

#include "Communication.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <mutex>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <format.h>

//在C++11中实现SessionLock
using _shared_mutex = std::mutex;
using _shared_lock = std::unique_lock<std::mutex>;
using _unique_lock = std::unique_lock<std::mutex>;

//在C++17中实现SessionLock
//#include <shared_mutex>
//using _shared_mutex = std::shared_mutex;
//using _shared_lock = std::shared_lock<std::shared_mutex>;
//using _unique_lock = std::unique_lock<std::shared_mutex>;

//在boost中实现SessionLock
//#include <boost/thread/shared_mutex.hpp>
//#include <boost/thread/locks.hpp>
//using _shared_mutex = boost::shared_mutex;
//using _shared_lock = boost::shared_lock<boost::shared_mutex>;
//using _unique_lock = boost::unique_lock<boost::shared_mutex>;

namespace Server
{
    class SessionContext : public std::enable_shared_from_this<SessionContext>
    {
    public:
        //跨线程共享只读访问

        std::function<void()> Quit; //跨线程事件(订阅者需要保证线程安全)
        void RaiseQuit()
        {
            if (Quit != nullptr) { Quit(); }
        }

        asio::ip::tcp::endpoint RemoteEndPoint;

        std::shared_ptr<std::vector<std::uint8_t>> SessionToken;
        std::wstring GetSessionTokenString() const
        {
            std::wstring s;
            for (int k = 0; k < (int)(SessionToken->size()); k += 1)
            {
                auto b = (*SessionToken)[k];
                s += fmt::format(L"{:02X}", b);
            }
            return s;
        }


        //读时先定义auto Lock = ReaderLock();
        //写时先定义auto Lock = WriterLock();
        _shared_mutex SessionLock;
        _shared_lock ReaderLock() { return _shared_lock(SessionLock); }
        _unique_lock WriterLock() { return _unique_lock(SessionLock); }


        //跨线程共享读写访问，读写必须通过SessionLock

        int ReceivedMessageCount; //跨线程变量

        std::function<void(std::shared_ptr<Communication::MessageReceivedEvent>)> MessageReceived;

        std::function<void(std::shared_ptr<Communication::TestMessageReceivedEvent>)> TestMessageReceived;

        //单线程访问

        int SendMessageCount;


        SessionContext()
            : SessionToken(std::make_shared<std::vector<std::uint8_t>>()),
              ReceivedMessageCount(0),
              SendMessageCount(0)
        {
        }
    };
}
