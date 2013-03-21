#pragma once

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/AutoResetEvent.h"
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

namespace Net
{
    class StreamedAsyncSocket
    {
    private:
        class SendAsyncParameters
        {
        public:
            std::shared_ptr<std::vector<uint8_t>> Bytes;
            int Offset;
            int Count;
            std::function<void()> Completed;
            std::function<void(const boost::system::error_code &se)> Faulted;
        };

        BaseSystem::AutoResetEvent NumAsyncOperationUpdated;
        BaseSystem::LockedVariable<int> NumAsyncOperation;
        std::shared_ptr<boost::asio::ip::tcp::socket> InnerSocket;
        BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<SendAsyncParameters>>>> SendQueue;

    public:
        StreamedAsyncSocket(std::shared_ptr<boost::asio::ip::tcp::socket> Socket)
            :
            NumAsyncOperation(0),
            InnerSocket(Socket),
            SendQueue(std::make_shared<std::queue<std::shared_ptr<SendAsyncParameters>>>())
        {
        }
        ~StreamedAsyncSocket()
        {
            InnerSocket->close();
            while (NumAsyncOperation.Check<bool>([](const int &n) { return n != 0; }))
            {
                NumAsyncOperationUpdated.WaitOne();
            }
        }

    private:
        void LockAsyncOperation()
        {
            NumAsyncOperation.Update([](int n) { return n + 1; });
            NumAsyncOperationUpdated.Set();
        }

        void ReleaseAsyncOperation()
        {
            NumAsyncOperation.Update([&](const int &n) -> int
            {
                NumAsyncOperationUpdated.Set();
                return n - 1;
            });
        }

        void DoSendAsync(std::shared_ptr<SendAsyncParameters> p)
        {
            auto Release = [=]()
            {
                BaseSystem::AutoRelease Final([=]() { this->ReleaseAsyncOperation(); });
                SendQueue.DoAction([=](std::shared_ptr<std::queue<std::shared_ptr<SendAsyncParameters>>> &q)
                {
                    if (q->front() != p) { throw std::logic_error("InvalidOperationException"); }
                    q->pop();
                    if (q->size() > 0)
                    {
                        DoSendAsync(q->front());
                    }
                });
            };
            auto WriteHandler = [=](const boost::system::error_code &se, size_t Count)
            {
                BaseSystem::AutoRelease ar(Release);
                if (se == boost::system::errc::success)
                {
                    p->Completed();
                }
                else
                {
                    p->Faulted(se);
                }
            };
            boost::asio::async_write(*InnerSocket, boost::asio::buffer(p->Bytes->data() + p->Offset, p->Count), WriteHandler);
        }

    public:
        void SendAsync(std::shared_ptr<std::vector<std::uint8_t>> Bytes, int Offset, int Count, std::function<void()> Completed, std::function<void(const boost::system::error_code &se)> Faulted)
        {
            if ((Offset < 0) || (Count < 0) || (Offset + Count > (int)(Bytes->size()))) { throw std::out_of_range(""); }
            SendQueue.DoAction([=](std::shared_ptr<std::queue<std::shared_ptr<SendAsyncParameters>>> &q)
            {
                auto p = std::make_shared<SendAsyncParameters>();
                p->Bytes = Bytes;
                p->Offset = Offset;
                p->Count = Count;
                p->Completed = Completed;
                p->Faulted = Faulted;
                LockAsyncOperation();
                q->push(p);
                if (q->size() == 1)
                {
                    DoSendAsync(q->front());
                }
            });
        }

        void ReceiveAsync(std::shared_ptr<std::vector<std::uint8_t>> Bytes, int Offset, int Count, std::function<void(int)> Completed, std::function<void(const boost::system::error_code &se)> Faulted)
        {
            if ((Offset < 0) || (Count < 0) || (Offset + Count > (int)(Bytes->size()))) { throw std::out_of_range(""); }
            LockAsyncOperation();
            auto ReadHandler = [=](const boost::system::error_code &se, size_t Count)
            {
                BaseSystem::AutoRelease Final([=]() { this->ReleaseAsyncOperation(); });
                auto ba = Bytes;
                ba = nullptr;
                if (se == boost::system::errc::success)
                {
                    Completed(Count);
                }
                else
                {
                    Faulted(se);
                }
            };
            InnerSocket->async_read_some(boost::asio::buffer(Bytes->data() + Offset, Count), ReadHandler);
        }

        void ShutdownReceive()
        {
            InnerSocket->shutdown(boost::asio::ip::tcp::socket::shutdown_receive);
        }

        void ShutdownBoth()
        {
            InnerSocket->shutdown(boost::asio::ip::tcp::socket::shutdown_both);
        }
    };
}
