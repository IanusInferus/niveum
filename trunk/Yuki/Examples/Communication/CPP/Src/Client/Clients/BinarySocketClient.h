#pragma once

#include "Communication.h"
#include "UtfEncoding.h"
#include "CommunicationBinary.h"
#include "Context/ClientContext.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    class BinarySocketClient
    {
    private:
        std::shared_ptr<Communication::IClientImplementation<ClientContext>> ci;
    public:
        std::shared_ptr<Communication::Binary::BinaryClient<ClientContext>> InnerClient;
    private:
        std::shared_ptr<ClientContext> Context;

        boost::asio::ip::tcp::endpoint RemoteEndPoint;
        boost::asio::ip::tcp::socket sock;

        std::vector<uint8_t> Buffer;
        int BufferLength;

        class BinarySender : public Communication::Binary::IBinarySender
        {
        private:
            boost::asio::ip::tcp::socket &sock;
        public:
            BinarySender(boost::asio::ip::tcp::socket &sock)
                : sock(sock)
            {
            }

            void Send(std::wstring CommandName, uint32_t CommandHash, std::shared_ptr<std::vector<uint8_t>> Parameters)
            {
                Communication::Binary::ByteArrayStream s;
                s.WriteString(CommandName);
                s.WriteUInt32(CommandHash);
                s.WriteInt32((int32_t)Parameters->size());
                s.WriteBytes(Parameters);
                s.SetPosition(0);
                auto Bytes = s.ReadBytes(s.GetLength());
                sock.send(boost::asio::buffer(Bytes->data(), Bytes->size()));
            }
        };

        std::shared_ptr<BinarySender> bs;

    public:
        BinarySocketClient(boost::asio::io_service& io_service, boost::asio::ip::tcp::endpoint RemoteEndPoint, std::shared_ptr<Communication::IClientImplementation<ClientContext>> ci)
            : sock(io_service), BufferLength(0)
        {
            this->RemoteEndPoint = RemoteEndPoint;
            this->ci = ci;
            bs = std::make_shared<BinarySender>(sock);
            InnerClient = std::make_shared<Communication::Binary::BinaryClient<ClientContext>>(bs, ci);
            Context = std::make_shared<ClientContext>();
            Context->DequeueCallback = [=](std::wstring CommandName) { return InnerClient->DequeueCallback(CommandName); };
            Buffer.resize(8 * 1024, 0);
        }

        void Connect()
        {
            sock.connect(RemoteEndPoint);
        }

    private:
        class CommandBody
        {
        public:
            std::wstring CommandName;
            uint32_t CommandHash;
            std::shared_ptr<std::vector<uint8_t>> Parameters;
        };

        class TryShiftResult
        {
        public:
            std::shared_ptr<CommandBody> Command;
            int Position;
        };

        class BufferStateMachine
        {
        private:
            int State;
            // 0 初始状态
            // 1 已读取NameLength
            // 2 已读取CommandHash
            // 3 已读取Name
            // 4 已读取ParametersLength

            int32_t CommandNameLength;
            std::wstring CommandName;
            uint32_t CommandHash;
            int32_t ParametersLength;

        public:
            BufferStateMachine()
            {
                State = 0;
            }

            std::shared_ptr<TryShiftResult> TryShift(const std::vector<uint8_t> &Buffer, int Position, int Length)
            {
                if (State == 0)
                {
                    if (Length >= 4)
                    {
                        Communication::Binary::ByteArrayStream s;
                        for (int k = 0; k < 4; k += 1)
                        {
                            s.WriteByte(Buffer[Position + k]);
                        }
                        s.SetPosition(0);
                        CommandNameLength = s.ReadInt32();
                        if (CommandNameLength < 0 || CommandNameLength > 128) { throw std::logic_error("InvalidOperationException"); }
                        auto r = std::make_shared<TryShiftResult>();
                        r->Command = nullptr;
                        r->Position = Position + 4;
                        State = 1;
                        return r;
                    }
                    return nullptr;
                }
                else if (State == 1)
                {
                    if (Length >= CommandNameLength)
                    {
                        Communication::Binary::ByteArrayStream s;
                        s.WriteInt32(CommandNameLength);
                        for (int k = 0; k < CommandNameLength; k += 1)
                        {
                            s.WriteByte(Buffer[Position + k]);
                        }
                        s.SetPosition(0);
                        CommandName = s.ReadString();
                        auto r = std::make_shared<TryShiftResult>();
                        r->Command = nullptr;
                        r->Position = Position + CommandNameLength;
                        State = 2;
                        return r;
                    }
                    return nullptr;
                }
                else if (State == 2)
                {
                    if (Length >= 4)
                    {
                        Communication::Binary::ByteArrayStream s;
                        for (int k = 0; k < 4; k += 1)
                        {
                            s.WriteByte(Buffer[Position + k]);
                        }
                        s.SetPosition(0);
                        CommandHash = s.ReadUInt32();
                        auto r = std::make_shared<TryShiftResult>();
                        r->Command = nullptr;
                        r->Position = Position + 4;
                        State = 3;
                        return r;
                    }
                    return nullptr;
                }
                if (State == 3)
                {
                    if (Length >= 4)
                    {
                        Communication::Binary::ByteArrayStream s;
                        for (int k = 0; k < 4; k += 1)
                        {
                            s.WriteByte(Buffer[Position + k]);
                        }
                        s.SetPosition(0);
                        ParametersLength = s.ReadInt32();
                        if (ParametersLength < 0 || ParametersLength > 8 * 1024) { throw std::logic_error("InvalidOperationException"); }
                        auto r = std::make_shared<TryShiftResult>();
                        r->Command = nullptr;
                        r->Position = Position + 4;
                        State = 4;
                        return r;
                    }
                    return nullptr;
                }
                else if (State == 4)
                {
                    if (Length >= ParametersLength)
                    {
                        auto Parameters = std::make_shared<std::vector<uint8_t>>();
                        Parameters->resize(ParametersLength, 0);
                        for (int k = 0; k < ParametersLength; k += 1)
                        {
                            (*Parameters)[k] = Buffer[Position + k];
                        }
                        auto cmd = std::make_shared<CommandBody>();
                        cmd->CommandName = CommandName;
                        cmd->CommandHash = CommandHash;
                        cmd->Parameters = Parameters;
                        auto r = std::make_shared<TryShiftResult>();
                        r->Command = cmd;
                        r->Position = Position + ParametersLength;
                        CommandNameLength = 0;
                        CommandName = L"";
                        CommandHash = 0;
                        ParametersLength = 0;
                        State = 0;
                        return r;
                    }
                    return nullptr;
                }
                else
                {
                    throw std::logic_error("InvalidOperationException");
                }
                return nullptr;
            }
        };

        BufferStateMachine bsm;

        static bool IsSocketErrorKnown(const boost::system::error_code &se)
        {
            if (se == boost::system::errc::connection_aborted) { return true; }
            if (se == boost::system::errc::connection_reset) { return true; }
            if (se == boost::asio::error::eof) { return true; }
            if (se == boost::system::errc::operation_canceled) { return true; }
            return false;
        }

        void Completed(size_t Count, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
        {
            if (Count == 0)
            {
                return;
            }
            auto FirstPosition = 0;
            BufferLength += Count;
            while (true)
            {
                auto r = bsm.TryShift(Buffer, FirstPosition, BufferLength - FirstPosition);
                if (r == nullptr)
                {
                    break;
                }
                FirstPosition = r->Position;
                
                if (r->Command != nullptr)
                {
                    auto cmd = r->Command;
                    DoResultHandle([=]() { InnerClient->HandleResult(*Context, cmd->CommandName, cmd->CommandHash, cmd->Parameters); });
                }
            }
            if (FirstPosition > 0)
            {
                auto CopyLength = BufferLength - FirstPosition;
                for (int i = 0; i < CopyLength; i += 1)
                {
                    Buffer[i] = Buffer[FirstPosition + i];
                }
                BufferLength = CopyLength;
            }
            Receive(DoResultHandle, UnknownFaulted);
        }

    public:
        /// <summary>接收消息</summary>
        /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
        /// <param name="UnknownFaulted">未知错误处理函数</param>
        void Receive(std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
        {
            auto ReadHandler = [=](const boost::system::error_code &se, size_t Count)
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
            sock.async_read_some(boost::asio::buffer(Buffer.data() + BufferLength, Buffer.size() - BufferLength), ReadHandler);
        }

        void Close()
        {
            sock.shutdown(boost::asio::ip::tcp::socket::shutdown_both);
            sock.close();
        }
    };
}
