#pragma once

#include "Communication.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/thread.hpp>
#include <boost/format.hpp>

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

        boost::asio::ip::tcp::endpoint RemoteEndPoint;

        std::shared_ptr<std::vector<std::uint8_t>> SessionToken;
        std::wstring GetSessionTokenString() const
        {
            std::wstring s;
            for (int k = 0; k < (int)(SessionToken->size()); k += 1)
            {
                auto b = (*SessionToken)[k];
                s += (boost::wformat(L"%2X") % b).str();
            }
            return s;
        }

        //跨线程共享读写访问变量锁
        //#include <boost/thread/locks.hpp>
        //写时先定义boost::unique_lock<boost::shared_mutex> WriterLock(SessionLock);
        //读时先定义boost::shared_lock<boost::shared_mutex> ReaderLock(SessionLock);
        boost::shared_mutex SessionLock;


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
