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
        class TcpSession<TSession>::SendAsyncParameters
        {
        public:
            std::shared_ptr<std::vector<uint8_t>> Bytes;
            int Offset;
            int Count;
            std::function<void()> Completed;
            std::function<void(const boost::system::error_code &se)> Faulted;
        };

        template <typename TSession>
        TcpSession<TSession>::TcpSession(boost::asio::io_service &IoService)
            : IoService(IoService),
            Socket(nullptr),
            SendQueue(std::make_shared<std::queue<std::shared_ptr<SendAsyncParameters>>>()),
            IdleTimeout(Communication::BaseSystem::Optional<int>::CreateNotHasValue())
        {
        }

        template <typename TSession>
        TcpSession<TSession>::~TcpSession()
        {
            Stop();
        }

        template <typename TSession>
        void TcpSession<TSession>::Start()
        {
            StartInner();
        }

        template <typename TSession>
        void TcpSession<TSession>::Stop()
        {
            StopInner();

            std::shared_ptr<boost::asio::ip::tcp::socket> s = nullptr;
            Socket.Update([&](std::shared_ptr<boost::asio::ip::tcp::socket> ss) -> std::shared_ptr<boost::asio::ip::tcp::socket>
            {
                s = ss;
                return nullptr;
            });

            if (s != nullptr)
            {
                try
                {
                    s->shutdown(boost::asio::ip::tcp::socket::shutdown_both);
                }
                catch (std::exception &)
                {
                }
                try
                {
                    s->close();
                }
                catch (std::exception &)
                {
                }
                try
                {
                    s = nullptr;
                }
                catch (std::exception &)
                {
                }
                if (NotifySessionQuit != nullptr)
                {
                    NotifySessionQuit(this->shared_from_this());
                    NotifySessionQuit = nullptr;
                }
            }
        }

        template <typename TSession>
        void TcpSession<TSession>::SetSocket(std::shared_ptr<boost::asio::ip::tcp::socket> s)
        {
            Socket.Update([&](std::shared_ptr<boost::asio::ip::tcp::socket> ss) -> std::shared_ptr<boost::asio::ip::tcp::socket>
            {
                if (ss != nullptr) { throw std::logic_error("InvalidOperationException"); }
                return s;
            });
        }

        template <typename TSession>
        std::shared_ptr<boost::asio::ip::tcp::socket> TcpSession<TSession>::GetSocket()
        {
            return Socket.Check<std::shared_ptr<boost::asio::ip::tcp::socket>>([&](std::shared_ptr<boost::asio::ip::tcp::socket> ss) -> std::shared_ptr<boost::asio::ip::tcp::socket>
            {
                return ss;
            });
        }

        template <typename TSession>
        void TcpSession<TSession>::DoSendAsync(std::shared_ptr<SendAsyncParameters> p)
        {
            std::shared_ptr<boost::asio::ip::tcp::socket> s = Socket.Check<std::shared_ptr<boost::asio::ip::tcp::socket>>([&](std::shared_ptr<boost::asio::ip::tcp::socket> ss) -> std::shared_ptr<boost::asio::ip::tcp::socket>
            {
                return ss;
            });
            if (s == nullptr) { return; }

            auto Release = [=]()
            {
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
            if (IdleTimeout->OnHasValue())
            {
                auto Timer = std::make_shared<boost::asio::deadline_timer>(IoService);
                Timer->expires_from_now(boost::posix_time::milliseconds(IdleTimeout->HasValue));
                Timer->async_wait([=](const boost::system::error_code& error)
                {
                    if (error == boost::system::errc::success)
                    {
                        if (NotifySessionQuit != nullptr)
                        {
                            NotifySessionQuit(this->shared_from_this());
                        }
                    }
                });
                auto WriteHandler = [=](const boost::system::error_code &se, size_t Count)
                {
                    Timer->cancel();
                    Communication::BaseSystem::AutoRelease ar(Release);
                    if (se == boost::system::errc::success)
                    {
                        p->Completed();
                    }
                    else
                    {
                        p->Faulted(se);
                    }
                };
                boost::asio::async_write(*s, boost::asio::buffer(p->Bytes->data() + p->Offset, p->Count), WriteHandler);
            }
            else
            {
                auto WriteHandler = [=](const boost::system::error_code &se, size_t Count)
                {
                    Communication::BaseSystem::AutoRelease ar(Release);
                    if (se == boost::system::errc::success)
                    {
                        p->Completed();
                    }
                    else
                    {
                        p->Faulted(se);
                    }
                };
                boost::asio::async_write(*s, boost::asio::buffer(p->Bytes->data() + p->Offset, p->Count), WriteHandler);
            }
        }

        template <typename TSession>
        void TcpSession<TSession>::StartInner()
        {
        }

        template <typename TSession>
        void TcpSession<TSession>::StopInner()
        {
        }

        template <typename TSession>
        void TcpSession<TSession>::SendAsync(std::shared_ptr<std::vector<uint8_t>> Bytes, int Offset, int Count, std::function<void()> Completed, std::function<void(const boost::system::error_code &se)> Faulted)
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
                q->push(p);
                if (q->size() == 1)
                {
                    DoSendAsync(q->front());
                }
            });
        }

        template <typename TSession>
        void TcpSession<TSession>::ReceiveAsync(std::shared_ptr<std::vector<uint8_t>> Bytes, int Offset, int Count, std::function<void(int)> Completed, std::function<void(const boost::system::error_code &se)> Faulted)
        {
            if ((Offset < 0) || (Count < 0) || (Offset + Count > (int)(Bytes->size()))) { throw std::out_of_range(""); }
            std::shared_ptr<boost::asio::ip::tcp::socket> s = Socket.Check<std::shared_ptr<boost::asio::ip::tcp::socket>>([&](std::shared_ptr<boost::asio::ip::tcp::socket> ss) -> std::shared_ptr<boost::asio::ip::tcp::socket>
            {
                return ss;
            });
            if (s == nullptr) { return; }
            if (IdleTimeout->OnHasValue())
            {
                auto Timer = std::make_shared<boost::asio::deadline_timer>(IoService);
                Timer->expires_from_now(boost::posix_time::milliseconds(IdleTimeout->HasValue));
                Timer->async_wait([=](const boost::system::error_code& error)
                {
                    if (error == boost::system::errc::success)
                    {
                        if (NotifySessionQuit != nullptr)
                        {
                            NotifySessionQuit(this->shared_from_this());
                        }
                    }
                });
                auto ReadHandler = [=](const boost::system::error_code &se, size_t Count)
                {
                    Timer->cancel();
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
                s->async_read_some(boost::asio::buffer(Bytes->data() + Offset, Count), ReadHandler);
            }
            else
            {
                auto ReadHandler = [=](const boost::system::error_code &se, size_t Count)
                {
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
                s->async_read_some(boost::asio::buffer(Bytes->data() + Offset, Count), ReadHandler);
            }
        }
    }
}
