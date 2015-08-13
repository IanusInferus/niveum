#pragma once

#include "Communication.h"
#include "Servers/IContext.h"

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
    class SessionContext : public ISessionContext, public std::enable_shared_from_this<SessionContext>
    {
    public:
        SessionContext(const std::vector<std::uint8_t> &SessionTokenValue)
            :
            IsSecureConnection(false),
            SessionTokenValue(SessionTokenValue),
            ReceivedMessageCount(0),
            SendMessageCount(0),
            Version(L"")
        {
            std::wstring s;
            for (std::size_t k = 0; k < SessionTokenValue.size(); k += 1)
            {
                auto b = SessionTokenValue[k];
                s += fmt::format(L"{:02X}", b);
            }
            SessionTokenStringValue = s;
        }

        //跨线程共享只读访问

        void RaiseQuit()
        {
            if (Quit != nullptr) { Quit(); }
        }

        void RaiseAuthenticated()
        {
            if (Authenticated != nullptr) { Authenticated(); }
        }

        void RaiseSecureConnectionRequired(std::shared_ptr<SecureContext> c)
        {
            if (SecureConnectionRequired != nullptr) { SecureConnectionRequired(c); }
            IsSecureConnection = true;
        }
        bool IsSecureConnection;

        std::wstring RemoteEndPoint()
        {
            return RemoteEndPointValue;
        }
        void RemoteEndPoint(std::wstring value)
        {
            RemoteEndPointValue = value;
        }

    private:
        std::wstring RemoteEndPointValue;

        std::vector<std::uint8_t> SessionTokenValue;
        std::wstring SessionTokenStringValue;

    public:
        /// <summary>长度为4</summary>
        std::vector<std::uint8_t> SessionToken()
        {
            return SessionTokenValue;
        }
        std::wstring SessionTokenString()
        {
            return SessionTokenStringValue;
        }


        //读时先定义auto Lock = ReaderLock();
        //写时先定义auto Lock = WriterLock();
        _shared_mutex SessionLock;
        _shared_lock ReaderLock() { return _shared_lock(SessionLock); }
        _unique_lock WriterLock() { return _unique_lock(SessionLock); }


        //跨线程共享读写访问，读写必须通过SessionLock

        int ReceivedMessageCount; //跨线程变量

        std::wstring Version;

        std::function<void(std::shared_ptr<Communication::MessageReceivedEvent>)> MessageReceived;

        std::function<void(std::shared_ptr<Communication::TestMessageReceivedEvent>)> TestMessageReceived;

        //单线程访问

        int SendMessageCount;

        std::chrono::system_clock::time_point RequestTime()
        {
            return RequestTimeValue;
        }
        void RequestTime(std::chrono::system_clock::time_point value)
        {
            RequestTimeValue = value;
        }

    private:
        std::chrono::system_clock::time_point RequestTimeValue;
    };
}
