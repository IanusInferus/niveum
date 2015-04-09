#pragma once

#include "IContext.h"
#include "ISerializationClient.h"
#include "StreamedClient.h"

#include "CommunicationBinary.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>

namespace Client
{
    namespace Streamed
    {
        class BinaryCountPacketClient : public IStreamedVirtualTransportClient
        {
        private:
            class Context
            {
            public:
                std::shared_ptr<std::vector<std::uint8_t>> ReadBuffer;
                int ReadBufferOffset;
                int ReadBufferLength;
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

                Context(int ReadBufferSize)
                    : ReadBufferOffset(0), ReadBufferLength(0), State(0), CommandNameLength(0), CommandName(L""), CommandHash(0), ParametersLength(0)
                {
                    ReadBuffer = std::make_shared<std::vector<std::uint8_t>>();
                    ReadBuffer->resize(ReadBufferSize, 0);
                }
            };

            Context c;
            std::shared_ptr<IBinarySerializationClientAdapter> bc;
            std::shared_ptr<IBinaryTransformer> Transformer;

        public:
            BinaryCountPacketClient(std::shared_ptr<IBinarySerializationClientAdapter> bc, std::shared_ptr<IBinaryTransformer> Transformer = nullptr, int ReadBufferSize = 128 * 1024)
                : c(ReadBufferSize)
            {
                this->bc = bc;
                this->Transformer = Transformer;
                bc->ClientEvent = [=](std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
                {
                    Communication::Binary::ByteArrayStream s;
                    s.WriteString(CommandName);
                    s.WriteUInt32(CommandHash);
                    s.WriteInt32(static_cast<std::int32_t>(Parameters->size()));
                    s.WriteBytes(Parameters);
                    s.SetPosition(0);
                    auto Bytes = s.ReadBytes(s.GetLength());
                    if (Transformer != nullptr)
                    {
                        Transformer->Transform(Bytes->data(), 0, static_cast<int>(Bytes->size()));
                    }
                    c.WriteBuffer.push_back(Bytes);
                    if (ClientMethod != nullptr) { ClientMethod(); }
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
                auto WriteBuffer = c.WriteBuffer;
                c.WriteBuffer.clear();
                return WriteBuffer;
            }

            std::shared_ptr<StreamedVirtualTransportClientHandleResult> Handle(int Count)
            {
                auto ret = StreamedVirtualTransportClientHandleResult::CreateContinue();

                auto Buffer = c.ReadBuffer;
                auto FirstPosition = c.ReadBufferOffset;
                auto BufferLength = c.ReadBufferOffset + c.ReadBufferLength;
                if (Transformer != nullptr)
                {
                    Transformer->Inverse(Buffer->data(), BufferLength, Count);
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
                        auto Command = std::make_shared<StreamedVirtualTransportClientHandleResultCommand>();
                        Command->CommandName = CommandName;
                        auto bc = this->bc;
                        Command->HandleResult = [=]() { bc->HandleResult(CommandName, CommandHash, Parameters); };
                        ret = StreamedVirtualTransportClientHandleResult::CreateCommand(Command);
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
                return bc->Hash();
            }

        private:
            class Command
            {
            public:
                std::wstring CommandName;
                uint32_t CommandHash;
                std::shared_ptr<std::vector<uint8_t>> Parameters;
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
                        auto cmd = std::make_shared<Command>();
                        cmd->CommandName = bc.CommandName;
                        cmd->CommandHash = bc.CommandHash;
                        cmd->Parameters = Parameters;
                        auto r = std::make_shared<TryShiftResult>();
                        r->Command = cmd;
                        r->Position = Position + bc.ParametersLength;
                        bc.CommandNameLength = 0;
                        bc.CommandName = L"";
                        bc.CommandHash = 0;
                        bc.ParametersLength = 0;
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
}
