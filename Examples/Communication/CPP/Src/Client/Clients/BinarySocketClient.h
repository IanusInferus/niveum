#pragma once

#include "ISerializationClient.h"

#include "CommunicationBinary.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <queue>
#include <unordered_map>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <boost/asio.hpp>
#include <boost/date_time.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    class BinarySocketClient
    {
    public:
        std::function<void(std::wstring, int)> ClientCommandReceived;
        std::function<void(std::wstring, int)> ClientCommandFailed;
        std::function<void(std::wstring)> ServerCommandReceived;
    private:
        std::shared_ptr<IBinarySerializationClientAdapter> bc;
        boost::asio::io_service &io_service;
        boost::asio::ip::tcp::socket sock;

        static const int BufferMemorySize = 128 * 1024;
        static const int NumTimeoutMilliseconds = 20000;
        std::vector<uint8_t> Buffer;
        int BufferLength;
        bool NeedShutdown;

        struct CommandRequest
        {
            std::wstring Name;
            boost::posix_time::ptime Time;
            std::shared_ptr<boost::asio::deadline_timer> Timer;
            std::shared_ptr<bool> Finished;
        };

        std::shared_ptr<std::unordered_map<std::wstring, std::shared_ptr<std::queue<CommandRequest>>>> CommandRequests;

    public:
        BinarySocketClient(boost::asio::io_service& io_service, std::shared_ptr<IBinarySerializationClientAdapter> bc)
            : io_service(io_service), sock(io_service), BufferLength(0), NeedShutdown(false)
        {
            CommandRequests = std::make_shared<std::unordered_map<std::wstring, std::shared_ptr<std::queue<CommandRequest>>>>();
            this->bc = bc;
            bc->DequeuedCallbackEvent = [=](std::wstring CommandName)
            {
                if (CommandRequests->count(CommandName) > 0)
                {
                    auto q = (*CommandRequests)[CommandName];
                    auto &cq = q->front();

                    *cq.Finished = true;
                    auto TimeSpan = boost::posix_time::microsec_clock::universal_time() - cq.Time;
                    auto Milliseconds = (int)(TimeSpan.total_milliseconds());
                    if (ClientCommandFailed != nullptr)
                    {
                        ClientCommandFailed(cq.Name, Milliseconds);
                    }
                    q->pop();
                    if (q->size() == 0)
                    {
                        CommandRequests->erase(CommandName);
                    }
                }
            };
            bc->ClientEvent = [=](std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
            {
                CommandRequest cq = {};
                cq.Name = CommandName;
                auto Time = boost::posix_time::microsec_clock::universal_time();
                cq.Time = Time;
                auto Finished = std::make_shared<bool>(false);
                auto Timer = std::make_shared<boost::asio::deadline_timer>(this->io_service);
                Timer->expires_from_now(boost::posix_time::milliseconds(NumTimeoutMilliseconds));
                Timer->async_wait([=](const boost::system::error_code& error)
                {
                    if (error == boost::system::errc::success)
                    {
                        if (!*Finished)
                        {
                            throw boost::system::system_error(boost::system::errc::timed_out, boost::system::generic_category());
                        }
                    }
                });
                cq.Timer = Timer;
                cq.Finished = Finished;
                if (CommandRequests->count(CommandName) > 0)
                {
                    auto q = (*CommandRequests)[CommandName];
                    q->push(cq);
                }
                else
                {
                    auto q = std::make_shared<std::queue<CommandRequest>>();
                    q->push(cq);
                    (*CommandRequests)[CommandName] = q;
                }

                Communication::Binary::ByteArrayStream s;
                s.WriteString(CommandName);
                s.WriteUInt32(CommandHash);
                s.WriteInt32((int32_t)Parameters->size());
                s.WriteBytes(Parameters);
                s.SetPosition(0);
                auto Bytes = s.ReadBytes(s.GetLength());

                auto WriteHandler = [=](const boost::system::error_code &se, size_t Count)
                {
                    if (se == boost::system::errc::success)
                    {
                    }
                    else
                    {
                        Timer->cancel();
                        throw boost::system::system_error(se);
                    }
                };
                boost::asio::async_write(sock, boost::asio::buffer(Bytes->data(), Bytes->size()), WriteHandler);
            };
            Buffer.resize(BufferMemorySize, 0);
        }

        virtual ~BinarySocketClient()
        {
            Close();
        }

        void ClearRequests()
        {
            for (auto p : *CommandRequests)
            {
                auto q = std::get<1>(p);
                while (!q->empty())
                {
                    auto &cq = q->front();
                    *cq.Finished = true;
                    bc->DequeueCallback(cq.Name);
                    q->pop();
                }
            }
            CommandRequests->clear();
        }

        void Connect(boost::asio::ip::tcp::endpoint RemoteEndPoint)
        {
            NeedShutdown = true;
            sock.connect(RemoteEndPoint);
        }

        /// <summary>异步连接</summary>
        /// <param name="Completed">正常连接处理函数</param>
        /// <param name="UnknownFaulted">未知错误处理函数</param>
        void ConnectAsync(boost::asio::ip::tcp::endpoint RemoteEndPoint, std::function<void(void)> Completed, std::function<void(const boost::system::error_code &)> UnknownFaulted)
        {
            NeedShutdown = true;
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
            sock.async_connect(RemoteEndPoint, ConnectHandler);
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
                        if (ParametersLength < 0 || ParametersLength > BufferMemorySize) { throw std::logic_error("InvalidOperationException"); }
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
                    auto CommandName = cmd->CommandName;
                    auto CommandHash = cmd->CommandHash;
                    auto Parameters = cmd->Parameters;
                    if (CommandRequests->count(CommandName) > 0)
                    {
                        auto q = (*CommandRequests)[CommandName];
                        auto &cq = q->front();

                        *cq.Finished = true;
                        auto TimeSpan = boost::posix_time::microsec_clock::universal_time() - cq.Time;
                        auto Milliseconds = (int)(TimeSpan.total_milliseconds());
                        if (ClientCommandReceived != nullptr)
                        {
                            ClientCommandReceived(cq.Name, Milliseconds);
                        }
                        q->pop();
                        if (q->size() == 0)
                        {
                            CommandRequests->erase(CommandName);
                        }
                    }
                    else
                    {
                        if (ServerCommandReceived != nullptr)
                        {
                            ServerCommandReceived(CommandName);
                        }
                    }
                    DoResultHandle([=]() { bc->HandleResult(CommandName, CommandHash, Parameters); });
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
            ReceiveAsync(DoResultHandle, UnknownFaulted);
        }

    public:
        /// <summary>接收消息</summary>
        /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
        /// <param name="UnknownFaulted">未知错误处理函数</param>
        void ReceiveAsync(std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
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
            try
            {
                if (NeedShutdown)
                {
                    sock.shutdown(boost::asio::ip::tcp::socket::shutdown_both);
                }
            }
            catch (...)
            {
            }
            sock.close();
        }
    };
}
