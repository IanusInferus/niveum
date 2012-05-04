#pragma once

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/Optional.h"
#include "BaseSystem/AutoRelease.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <queue>
#include <string>
#include <exception>
#include <stdexcept>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/date_time/posix_time/posix_time.hpp>

namespace Communication
{
    namespace Net
    {
        template <typename TSession>
        class TcpSession : public std::enable_shared_from_this<TSession>
        {
        protected:
            boost::asio::io_service &IoService;
        private:
            Communication::BaseSystem::LockedVariable<std::shared_ptr<boost::asio::ip::tcp::socket>> Socket;
            class SendAsyncParameters;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<SendAsyncParameters>>>> SendQueue;
        public:
            std::function<void(std::shared_ptr<TSession>)> NotifySessionQuit;
            boost::asio::ip::tcp::endpoint RemoteEndPoint;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> IdleTimeout;

            TcpSession(boost::asio::io_service &IoService);

            virtual ~TcpSession();

            void Start();

            void Stop();

            void SetSocket(std::shared_ptr<boost::asio::ip::tcp::socket> s);

            std::shared_ptr<boost::asio::ip::tcp::socket> GetSocket();

        private:
            void DoSendAsync(std::shared_ptr<SendAsyncParameters> p);
        protected:
            virtual void StartInner();

            virtual void StopInner();

            void SendAsync(std::shared_ptr<std::vector<uint8_t>> Bytes, int Offset, int Count, std::function<void()> Completed, std::function<void(const boost::system::error_code &se)> Faulted);

            void ReceiveAsync(std::shared_ptr<std::vector<uint8_t>> Bytes, int Offset, int Count, std::function<void(int)> Completed, std::function<void(const boost::system::error_code &se)> Faulted);
        };
    }
}
