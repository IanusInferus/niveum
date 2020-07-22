#pragma once

#include "Generated/Communication.h"
#include "Servers/IContext.h"
#include "BaseSystem/StringUtilities.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <mutex>
#include <shared_mutex>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <format.h>

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
            Version(u"")
        {
            std::wstring s;
            for (std::size_t k = 0; k < SessionTokenValue.size(); k += 1)
            {
                auto b = SessionTokenValue[k];
                s += fmt::format(L"{:02X}", b);
            }
            SessionTokenStringValue = wideCharToUtf16(s);
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

        std::u16string RemoteEndPoint()
        {
            return RemoteEndPointValue;
        }
        void RemoteEndPoint(std::u16string value)
        {
            RemoteEndPointValue = value;
        }

    private:
        std::u16string RemoteEndPointValue;

        std::vector<std::uint8_t> SessionTokenValue;
        std::u16string SessionTokenStringValue;

    public:
        /// <summary>长度为4</summary>
        std::vector<std::uint8_t> SessionToken()
        {
            return SessionTokenValue;
        }
        std::u16string SessionTokenString()
        {
            return SessionTokenStringValue;
        }


        //读时先定义auto Lock = ReaderLock();
        //写时先定义auto Lock = WriterLock();
        std::shared_mutex SessionLock;
        std::shared_lock<std::shared_mutex> ReaderLock() { return std::shared_lock<std::shared_mutex>(SessionLock); }
        std::unique_lock<std::shared_mutex> WriterLock() { return std::unique_lock<std::shared_mutex>(SessionLock); }


        //跨线程共享读写访问，读写必须通过SessionLock

        int ReceivedMessageCount; //跨线程变量

        std::u16string Version;
        std::shared_ptr<Communication::IEventPump> EventPump;

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
