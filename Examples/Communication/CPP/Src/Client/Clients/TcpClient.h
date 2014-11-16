#pragma once

#include "ISerializationClient.h"
#include "StreamedClient.h"

#include "BaseSystem/LockedVariable.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    namespace Streamed
    {
        class TcpClient
        {
        private:
            std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient;
            boost::asio::ip::tcp::endpoint RemoteEndPoint;
            boost::asio::ip::tcp::socket Socket;
            BaseSystem::LockedVariable<bool> IsRunningValue;
        public:
            bool IsRunning()
            {
                return IsRunningValue.Check<bool>([](bool v) { return v; });
            }

            TcpClient(boost::asio::io_service &io_service, boost::asio::ip::tcp::endpoint RemoteEndPoint, std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient, std::function<void(boost::system::system_error)> ExceptionHandler = nullptr)
                : Socket(io_service), IsRunningValue(false), IsDisposed(false)
            {
                this->RemoteEndPoint = RemoteEndPoint;
                this->VirtualTransportClient = VirtualTransportClient;
                VirtualTransportClient->ClientMethod = [=]()
                {
                    auto ByteArrays = this->VirtualTransportClient->TakeWriteBuffer();
                    std::vector<boost::asio::const_buffer> Buffers;
                    for (auto b : ByteArrays)
                    {
                        Buffers.push_back(boost::asio::buffer(*b));
                    }

                    boost::system::error_code ec;
                    boost::asio::write(Socket, Buffers, ec);
                    if (ec != boost::system::errc::success)
                    {
                        if (ExceptionHandler != nullptr)
                        {
                            ExceptionHandler(boost::system::system_error(ec.value(), ec.category()));
                        }
                        else
                        {
                            throw boost::system::system_error(ec.value(), ec.category());
                        }
                    }
                };
            }

            virtual ~TcpClient()
            {
                Close();
            }

            void Connect()
            {
                IsRunningValue.Update([](bool b)
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }
                    return true;
                });
                Socket.connect(RemoteEndPoint);
            }

            /// <summary>异步连接</summary>
            /// <param name="Completed">正常连接处理函数</param>
            /// <param name="UnknownFaulted">未知错误处理函数</param>
            void ConnectAsync(boost::asio::ip::tcp::endpoint RemoteEndPoint, std::function<void(void)> Completed, std::function<void(const boost::system::error_code &)> UnknownFaulted)
            {
                IsRunningValue.Update([](bool b)
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }
                    return true;
                });
                auto ConnectHandler = [=](const boost::system::error_code &se)
                {
                    if (se == boost::system::errc::success)
                    {
                        Completed();
                    }
                    else
                    {
                        if (!IsSocketErrorKnown(se))
                        {
                            UnknownFaulted(se);
                        }
                    }
                };
                Socket.async_connect(RemoteEndPoint, ConnectHandler);
            }

        private:
            static bool IsSocketErrorKnown(const boost::system::error_code &se)
            {
                if (se == boost::system::errc::connection_aborted) { return true; }
                if (se == boost::system::errc::connection_reset) { return true; }
                if (se == boost::asio::error::eof) { return true; }
                if (se == boost::system::errc::operation_canceled) { return true; }
                return false;
            }

            void Completed(std::size_t Count, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
            {
                if (Count == 0)
                {
                    return;
                }

                while (true)
                {
                    auto r = VirtualTransportClient->Handle(Count);
                    if (r->OnContinue())
                    {
                        break;
                    }
                    else if (r->OnCommand())
                    {
                        DoResultHandle(r->Command->HandleResult);
                        auto RemainCount = static_cast<int>(VirtualTransportClient->GetReadBufferLength());
                        if (RemainCount <= 0)
                        {
                            break;
                        }
                        Count = 0;
                    }
                    else
                    {
                        throw std::logic_error("InvalidOperationException");
                    }
                }
                ReceiveAsync(DoResultHandle, UnknownFaulted);
            }

        public:
            /// <summary>接收消息</summary>
            /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
            /// <param name="UnknownFaulted">未知错误处理函数</param>
            void ReceiveAsync(std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
            {
                auto ReadHandler = [=](const boost::system::error_code &se, std::size_t Count)
                {
                    if (se == boost::system::errc::success)
                    {
                        Completed(Count, DoResultHandle, UnknownFaulted);
                    }
                    else
                    {
                        if (!IsSocketErrorKnown(se))
                        {
                            UnknownFaulted(se);
                        }
                    }
                };
                auto Buffer = VirtualTransportClient->GetReadBuffer();
                auto BufferLength = VirtualTransportClient->GetReadBufferOffset() + VirtualTransportClient->GetReadBufferLength();
                Socket.async_read_some(boost::asio::buffer(&(*Buffer)[BufferLength], Buffer->size() - BufferLength), ReadHandler);
            }

        private:
            bool IsDisposed;
        public:
            void Close()
            {
                if (IsDisposed) { return; }
                IsDisposed = true;

                bool Connected = false;
                IsRunningValue.Update([&](bool b)
                {
                    Connected = b;
                    return false;
                });
                try
                {
                    if (Connected)
                    {
                        Socket.shutdown(boost::asio::ip::tcp::socket::shutdown_both);
                    }
                }
                catch (...)
                {
                }
                try
                {
                    Socket.close();
                }
                catch (...)
                {
                }
            }
        };
    }
}
