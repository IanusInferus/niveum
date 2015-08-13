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
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    class TcpClient
    {
    private:
        std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient;
        asio::ip::tcp::endpoint RemoteEndPoint;
        asio::ip::tcp::socket Socket;
        BaseSystem::LockedVariable<bool> IsRunningValue;
    public:
        bool IsRunning()
        {
            return IsRunningValue.Check<bool>([](bool v) { return v; });
        }

        TcpClient(asio::io_service &io_service, asio::ip::tcp::endpoint RemoteEndPoint, std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient, std::function<void(asio::system_error)> ExceptionHandler = nullptr)
            : Socket(io_service), IsRunningValue(false), IsDisposed(false)
        {
            this->RemoteEndPoint = RemoteEndPoint;
            this->VirtualTransportClient = VirtualTransportClient;
            VirtualTransportClient->ClientMethod = [=]()
            {
                auto ByteArrays = this->VirtualTransportClient->TakeWriteBuffer();
                std::vector<asio::const_buffer> Buffers;
                for (auto b : ByteArrays)
                {
                    Buffers.push_back(asio::buffer(*b));
                }

                asio::error_code ec;
                asio::write(Socket, Buffers, ec);
                if (ec)
                {
                    if (ExceptionHandler != nullptr)
                    {
                        ExceptionHandler(asio::system_error(ec.value(), ec.category()));
                    }
                    else
                    {
                        throw asio::system_error(ec.value(), ec.category());
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
        void ConnectAsync(asio::ip::tcp::endpoint RemoteEndPoint, std::function<void(void)> Completed, std::function<void(const asio::error_code &)> UnknownFaulted)
        {
            IsRunningValue.Update([](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                return true;
            });
            auto ConnectHandler = [=](const asio::error_code &se)
            {
                if (se)
                {
                    if (!IsSocketErrorKnown(se))
                    {
                        UnknownFaulted(se);
                    }
                }
                else
                {
                    Completed();
                }
            };
            Socket.async_connect(RemoteEndPoint, ConnectHandler);
        }

    private:
        static bool IsSocketErrorKnown(const asio::error_code &se)
        {
            if (se == asio::error::connection_aborted) { return true; }
            if (se == asio::error::eof) { return true; }
            if (se == asio::error::operation_aborted) { return true; }
            return false;
        }

        void Completed(std::size_t Count, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const asio::error_code &)> UnknownFaulted)
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
        void ReceiveAsync(std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const asio::error_code &)> UnknownFaulted)
        {
            auto ReadHandler = [=](const asio::error_code &se, std::size_t Count)
            {
                if (se)
                {
                    if (!IsSocketErrorKnown(se))
                    {
                        UnknownFaulted(se);
                    }
                }
                else
                {
                    Completed(Count, DoResultHandle, UnknownFaulted);
                }
            };
            auto Buffer = VirtualTransportClient->GetReadBuffer();
            auto BufferLength = VirtualTransportClient->GetReadBufferOffset() + VirtualTransportClient->GetReadBufferLength();
            Socket.async_read_some(asio::buffer(&(*Buffer)[BufferLength], Buffer->size() - BufferLength), ReadHandler);
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
            if (Connected)
            {
                asio::error_code e;
                Socket.shutdown(asio::ip::tcp::socket::shutdown_both, e);
            }
                {
                    asio::error_code e;
                    Socket.close(e);
                }
        }
    };
}
