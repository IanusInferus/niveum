#pragma once

#include "IContext.h"
#include "ISerializationServer.h"
#include "StreamedServer.h"

#include "CommunicationBinary.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <mutex>

namespace Server
{
    class BinaryCountPacketServer : public IStreamedVirtualTransportServer
    {
    private:
        class Context
        {
        public:
            std::shared_ptr<std::vector<std::uint8_t>> ReadBuffer;
            int ReadBufferOffset;
            int ReadBufferLength;
            std::mutex WriteBufferLockee;
            std::vector<std::shared_ptr<std::vector<std::uint8_t>>> WriteBuffer;

            int State;
            // 0 初始状态
            // 1 已读取NameLength
            // 2 已读取CommandHash
            // 3 已读取Name
            // 4 已读取ParametersLength

            std::int32_t CommandNameLength;
            std::wstring CommandName;
            std::uint32_t CommandHash;
            std::int32_t ParametersLength;
            std::size_t InputCommandByteLength;

            Context(int ReadBufferSize)
                : ReadBufferOffset(0), ReadBufferLength(0), State(0), CommandNameLength(0), CommandName(L""), CommandHash(0), ParametersLength(0), InputCommandByteLength(0)
            {
                ReadBuffer = std::make_shared<std::vector<std::uint8_t>>();
                ReadBuffer->resize(ReadBufferSize, 0);
            }
        };

        std::shared_ptr<IBinarySerializationServerAdapter> ss;
        Context c;
        std::function<bool(std::wstring)> CheckCommandAllowed;
        std::shared_ptr<IBinaryTransformer> Transformer;

    public:
        BinaryCountPacketServer(std::shared_ptr<IBinarySerializationServerAdapter> SerializationServerAdapter, std::function<bool(std::wstring)> CheckCommandAllowed, std::shared_ptr<IBinaryTransformer> Transformer = nullptr, int ReadBufferSize = 8 * 1024)
            : c(ReadBufferSize)
        {
            this->ss = SerializationServerAdapter;
            this->CheckCommandAllowed = CheckCommandAllowed;
            this->Transformer = Transformer;
            this->ss->ServerEvent = [=](std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
            {
                Communication::Binary::ByteArrayStream s;
                s.WriteString(CommandName);
                s.WriteUInt32(CommandHash);
                s.WriteInt32(static_cast<std::int32_t>(Parameters->size()));
                s.WriteBytes(Parameters);
                s.SetPosition(0);
                auto Bytes = s.ReadBytes(s.GetLength());
                auto BytesLength = Bytes->size();
                {
                    std::unique_lock<std::mutex> Lock(c.WriteBufferLockee);
                    if (Transformer != nullptr)
                    {
                        Transformer->Transform(*Bytes, 0, static_cast<int>(Bytes->size()));
                    }
                }
                c.WriteBuffer.push_back(Bytes);
                if (OutputByteLengthReport != nullptr)
                {
                    OutputByteLengthReport(CommandName, BytesLength);
                }
                if (this->ServerEvent != nullptr)
                {
                    this->ServerEvent();
                }
            };
        }

        std::shared_ptr<std::vector<std::uint8_t>> GetReadBuffer()
        {
            return c.ReadBuffer;
        }
        int GetReadBufferOffset()
        {
            return c.ReadBufferOffset;
        }
        int GetReadBufferLength()
        {
            return c.ReadBufferLength;
        }

        std::vector<std::shared_ptr<std::vector<std::uint8_t>>> TakeWriteBuffer()
        {
            {
                std::unique_lock<std::mutex> Lock(c.WriteBufferLockee);
                auto WriteBuffer = c.WriteBuffer;
                c.WriteBuffer.clear();
                return WriteBuffer;
            }
        }

        std::shared_ptr<StreamedVirtualTransportServerHandleResult> Handle(int Count)
        {
            auto ret = StreamedVirtualTransportServerHandleResult::CreateContinue();

            auto Buffer = c.ReadBuffer;
            auto FirstPosition = c.ReadBufferOffset;
            auto BufferLength = c.ReadBufferOffset + c.ReadBufferLength;
            if (Transformer != nullptr)
            {
                Transformer->Inverse(*Buffer, BufferLength, Count);
            }
            BufferLength += Count;

            while (true)
            {
                auto r = TryShift(c, Buffer, FirstPosition, BufferLength - FirstPosition);
                if (r == nullptr)
                {
                    break;
                }
                FirstPosition = r->Position;

                if (r->Command != nullptr)
                {
                    auto CommandName = r->Command->CommandName;
                    auto CommandHash = r->Command->CommandHash;
                    auto Parameters = r->Command->Parameters;
                    if (InputByteLengthReport != nullptr)
                    {
                        InputByteLengthReport(CommandName, r->Command->ByteLength);
                    }
                    if (ss->HasCommand(CommandName, CommandHash) && (CheckCommandAllowed != nullptr ? CheckCommandAllowed(CommandName) : true))
                    {
                        auto Command = std::make_shared<StreamedVirtualTransportServerHandleResultCommand>();
                        Command->CommandName = CommandName;
                        Command->ExecuteCommand = [=](std::function<void()> OnSuccess, std::function<void(const std::exception &)> OnFailure)
                        {
                            auto OnSuccessInner = [=](std::shared_ptr<std::vector<std::uint8_t>> OutputParameters)
                            {
                                Communication::Binary::ByteArrayStream s;
                                s.WriteString(CommandName);
                                s.WriteUInt32(CommandHash);
                                s.WriteInt32(static_cast<std::int32_t>(OutputParameters->size()));
                                s.WriteBytes(OutputParameters);
                                s.SetPosition(0);
                                auto Bytes = s.ReadBytes(s.GetLength());
                                auto BytesLength = Bytes->size();
                                {
                                    std::unique_lock<std::mutex> Lock(c.WriteBufferLockee);
                                    if (Transformer != nullptr)
                                    {
                                        Transformer->Transform(*Bytes, 0, static_cast<int>(Bytes->size()));
                                    }
                                    c.WriteBuffer.push_back(Bytes);
                                }
                                if (OutputByteLengthReport != nullptr)
                                {
                                    OutputByteLengthReport(CommandName, BytesLength);
                                }
                                OnSuccess();
                            };
                            ss->ExecuteCommand(CommandName, CommandHash, Parameters, OnSuccessInner, OnFailure);
                        };
                        ret = StreamedVirtualTransportServerHandleResult::CreateCommand(Command);
                    }
                    else
                    {
                        auto BadCommand = std::make_shared<StreamedVirtualTransportServerHandleResultBadCommand>();
                        BadCommand->CommandName = CommandName;
                        ret = StreamedVirtualTransportServerHandleResult::CreateBadCommand(BadCommand);
                    }
                    break;
                }
            }

            if ((BufferLength >= static_cast<int>(Buffer->size())) && (FirstPosition > 0))
            {
                auto CopyLength = BufferLength - FirstPosition;
                for (int i = 0; i < CopyLength; i += 1)
                {
                    (*Buffer)[i] = (*Buffer)[FirstPosition + i];
                }
                BufferLength = CopyLength;
                FirstPosition = 0;
            }
            if (FirstPosition >= BufferLength)
            {
                c.ReadBufferOffset = 0;
                c.ReadBufferLength = 0;
            }
            else
            {
                c.ReadBufferOffset = FirstPosition;
                c.ReadBufferLength = BufferLength - FirstPosition;
            }

            return ret;
        }

        std::uint64_t Hash()
        {
            return ss->Hash();
        }

    private:
        class Command
        {
        public:
            std::wstring CommandName;
            std::uint32_t CommandHash;
            std::shared_ptr<std::vector<uint8_t>> Parameters;
            std::int32_t ByteLength;
        };

        class TryShiftResult
        {
        public:
            std::shared_ptr<class Command> Command;
            int Position;
        };

        std::shared_ptr<TryShiftResult> TryShift(Context &bc, std::shared_ptr<std::vector<uint8_t>> Buffer, int Position, int Length)
        {
            if (bc.State == 0)
            {
                if (Length >= 4)
                {
                    Communication::Binary::ByteArrayStream s;
                    for (int k = 0; k < 4; k += 1)
                    {
                        s.WriteByte((*Buffer)[Position + k]);
                    }
                    s.SetPosition(0);
                    bc.CommandNameLength = s.ReadInt32();
                    if (bc.CommandNameLength < 0 || bc.CommandNameLength > 128) { throw std::logic_error("InvalidOperationException"); }
                    auto r = std::make_shared<TryShiftResult>();
                    r->Command = nullptr;
                    r->Position = Position + 4;
                    bc.InputCommandByteLength += 4;
                    bc.State = 1;
                    return r;
                }
                return nullptr;
            }
            else if (bc.State == 1)
            {
                if (Length >= bc.CommandNameLength)
                {
                    Communication::Binary::ByteArrayStream s;
                    s.WriteInt32(bc.CommandNameLength);
                    for (int k = 0; k < bc.CommandNameLength; k += 1)
                    {
                        s.WriteByte((*Buffer)[Position + k]);
                    }
                    s.SetPosition(0);
                    bc.CommandName = s.ReadString();
                    auto r = std::make_shared<TryShiftResult>();
                    r->Command = nullptr;
                    r->Position = Position + bc.CommandNameLength;
                    bc.InputCommandByteLength += bc.CommandNameLength;
                    bc.State = 2;
                    return r;
                }
                return nullptr;
            }
            else if (bc.State == 2)
            {
                if (Length >= 4)
                {
                    Communication::Binary::ByteArrayStream s;
                    for (int k = 0; k < 4; k += 1)
                    {
                        s.WriteByte((*Buffer)[Position + k]);
                    }
                    s.SetPosition(0);
                    bc.CommandHash = s.ReadUInt32();
                    auto r = std::make_shared<TryShiftResult>();
                    r->Command = nullptr;
                    r->Position = Position + 4;
                    bc.InputCommandByteLength += 4;
                    bc.State = 3;
                    return r;
                }
                return nullptr;
            }
            if (bc.State == 3)
            {
                if (Length >= 4)
                {
                    Communication::Binary::ByteArrayStream s;
                    for (int k = 0; k < 4; k += 1)
                    {
                        s.WriteByte((*Buffer)[Position + k]);
                    }
                    s.SetPosition(0);
                    bc.ParametersLength = s.ReadInt32();
                    if (bc.ParametersLength < 0 || bc.ParametersLength > static_cast<int>(Buffer->size())) { throw std::logic_error("InvalidOperationException"); }
                    auto r = std::make_shared<TryShiftResult>();
                    r->Command = nullptr;
                    r->Position = Position + 4;
                    bc.InputCommandByteLength += 4;
                    bc.State = 4;
                    return r;
                }
                return nullptr;
            }
            else if (bc.State == 4)
            {
                if (Length >= bc.ParametersLength)
                {
                    auto Parameters = std::make_shared<std::vector<uint8_t>>();
                    Parameters->resize(bc.ParametersLength, 0);
                    for (int k = 0; k < bc.ParametersLength; k += 1)
                    {
                        (*Parameters)[k] = (*Buffer)[Position + k];
                    }
                    bc.InputCommandByteLength += bc.ParametersLength;
                    auto cmd = std::make_shared<Command>();
                    cmd->CommandName = bc.CommandName;
                    cmd->CommandHash = bc.CommandHash;
                    cmd->Parameters = Parameters;
                    cmd->ByteLength = bc.InputCommandByteLength;
                    auto r = std::make_shared<TryShiftResult>();
                    r->Command = cmd;
                    r->Position = Position + bc.ParametersLength;
                    bc.CommandNameLength = 0;
                    bc.CommandName = L"";
                    bc.CommandHash = 0;
                    bc.ParametersLength = 0;
                    bc.InputCommandByteLength = 0;
                    bc.State = 0;
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
}
