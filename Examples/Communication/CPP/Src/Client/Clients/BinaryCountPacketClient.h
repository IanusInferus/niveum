#pragma once

#include "IContext.h"
#include "ISerializationClient.h"
#include "StreamedClient.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <mutex>

namespace Client
{
    class BinaryCountPacketClient : public IStreamedVirtualTransportClient
    {
    private:
        class IReadableStream
        {
        public:
            virtual std::uint8_t ReadByte() = 0;
            virtual std::vector<std::uint8_t> ReadBytes(std::size_t Size) = 0;

            Unit ReadUnit()
            {
                return Unit();
            }
            Boolean ReadBoolean()
            {
                return ReadByte() != 0;
            }

            std::uint8_t ReadUInt8()
            {
                return ReadByte();
            }
            std::uint16_t ReadUInt16()
            {
                std::uint16_t o;
                o = static_cast<std::uint16_t>(static_cast<std::uint16_t>(ReadByte()) & static_cast<std::uint16_t>(0xFF));
                o = static_cast<std::uint16_t>(o | ((static_cast<std::uint16_t>(ReadByte()) & 0xFF) << 8));
                return o;
            }
            std::uint32_t ReadUInt32()
            {
                std::uint32_t o;
                o = static_cast<std::uint32_t>(ReadByte()) & 0xFF;
                o = o | ((static_cast<std::uint32_t>(ReadByte()) & 0xFF) << 8);
                o = o | ((static_cast<std::uint32_t>(ReadByte()) & 0xFF) << 16);
                o = o | ((static_cast<std::uint32_t>(ReadByte()) & 0xFF) << 24);
                return o;
            }
            std::uint64_t ReadUInt64()
            {
                std::uint64_t o;
                o = static_cast<std::uint64_t>(ReadByte()) & 0xFF;
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 8);
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 16);
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 24);
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 32);
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 40);
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 48);
                o = o | ((static_cast<std::uint64_t>(ReadByte()) & 0xFF) << 56);
                return o;
            }
            std::int8_t ReadInt8()
            {
                return static_cast<std::int8_t>(ReadByte());
            }
            std::int16_t ReadInt16()
            {
                std::int16_t o;
                o = static_cast<std::int16_t>(static_cast<std::int16_t>(ReadByte()) & static_cast<std::int16_t>(0xFF));
                o = static_cast<std::int16_t>(o | ((static_cast<std::int16_t>(ReadByte()) & 0xFF) << 8));
                return o;
            }
            std::int32_t ReadInt32()
            {
                std::int32_t o;
                o = static_cast<std::int32_t>(ReadByte()) & 0xFF;
                o = o | ((static_cast<std::int32_t>(ReadByte()) & 0xFF) << 8);
                o = o | ((static_cast<std::int32_t>(ReadByte()) & 0xFF) << 16);
                o = o | ((static_cast<std::int32_t>(ReadByte()) & 0xFF) << 24);
                return o;
            }
            std::int64_t ReadInt64()
            {
                std::int64_t o;
                o = static_cast<std::int64_t>(ReadByte()) & 0xFF;
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 8);
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 16);
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 24);
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 32);
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 40);
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 48);
                o = o | ((static_cast<std::int64_t>(ReadByte()) & 0xFF) << 56);
                return o;
            }

            float ReadFloat32()
            {
                std::int32_t i = ReadInt32();
                return *reinterpret_cast<float *>(&i);
            }
            double ReadFloat64()
            {
                std::int64_t i = ReadInt64();
                return *reinterpret_cast<double *>(&i);
            }

            String ReadString()
            {
                std::int32_t Length = ReadInt32();
                int n = static_cast<int>(Length) / 2;
                std::u16string v;
                for (int k = 0; k < n; k += 1)
                {
                    v.push_back(static_cast<char16_t>(ReadUInt16()));
                }
                return v;
            }

            virtual ~IReadableStream() {}
        };

        class IWritableStream
        {
        public:
            virtual void WriteByte(std::uint8_t b) = 0;
            virtual void WriteBytes(const std::vector<std::uint8_t> & l) = 0;

            void WriteUnit(Unit v)
            {
            }
            void WriteBoolean(Boolean v)
            {
                if (v)
                {
                    WriteByte(0xFF);
                }
                else
                {
                    WriteByte(0);
                }
            }

            void WriteUInt8(std::uint8_t v)
            {
                WriteByte(v);
            }
            void WriteUInt16(std::uint16_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 8) & 0xFF));
            }
            void WriteUInt32(std::uint32_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 8) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 16) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 24) & 0xFF));
            }
            void WriteUInt64(std::uint64_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 8) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 16) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 24) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 32) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 40) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 48) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 56) & 0xFF));
            }
            void WriteInt8(std::int8_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v));
            }
            void WriteInt16(std::int16_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 8) & 0xFF));
            }
            void WriteInt32(std::int32_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 8) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 16) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 24) & 0xFF));
            }
            void WriteInt64(std::int64_t v)
            {
                WriteByte(static_cast<std::uint8_t>(v & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 8) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 16) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 24) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 32) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 40) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 48) & 0xFF));
                WriteByte(static_cast<std::uint8_t>((v >> 56) & 0xFF));
            }

            void WriteFloat32(float v)
            {
                WriteInt32(*reinterpret_cast<std::int32_t *>(&v));
            }
            void WriteFloat64(double v)
            {
                WriteInt64(*reinterpret_cast<std::int64_t *>(&v));
            }

            void WriteString(String v)
            {
                WriteInt32(static_cast<std::int32_t>(v.size()) * 2);
                for (auto c : v)
                {
                    WriteUInt16(static_cast<std::uint16_t>(c));
                }
            }

            virtual ~IWritableStream() {}
        };

        class IReadableWritableStream : public IReadableStream, public IWritableStream
        {
        public:
            virtual ~IReadableWritableStream() {}
        };

        class ByteArrayStream : public IReadableWritableStream /* final */
        {
        private:
            std::vector<std::uint8_t> Buffer;
            std::size_t Position;
        public:
            ByteArrayStream() : Position(0)
            {
            }

            std::uint8_t ReadByte()
            {
                if (Position + 1 > Buffer.size()) { throw std::out_of_range(""); }
                std::uint8_t b = Buffer[Position];
                Position += 1;
                return b;
            }
            std::vector<std::uint8_t> ReadBytes(std::size_t Size)
            {
                if (Position + Size > Buffer.size()) { throw std::out_of_range(""); }
                std::vector<std::uint8_t> l;
                l.resize(Size, 0);
                if (Size == 0) { return l; }
                std::copy(Buffer.data() + Position, Buffer.data() + Position + Size, l.data());
                Position += Size;
                return l;
            }

            void WriteByte(std::uint8_t b)
            {
                if (Position + 1 > Buffer.size()) { Buffer.resize(Position + 1, 0); }
                Buffer[Position] = b;
                Position += 1;
            }
            void WriteBytes(const std::vector<std::uint8_t> & l)
            {
                auto Size = l.size();
                if (Size == 0) { return; }
                if (Position + Size > Buffer.size()) { Buffer.resize(Position + Size, 0); }
                std::copy(l.data(), l.data() + Size, Buffer.data() + Position);
                Position += Size;
            }

            std::size_t GetPosition()
            {
                return Position;
            }

            void SetPosition(std::size_t Position)
            {
                this->Position = Position;
            }

            std::size_t GetLength()
            {
                return Buffer.size();
            }

            void SetLength(std::size_t Length)
            {
                Buffer.resize(Length, 0);
            }
        };

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
            std::u16string CommandName;
            std::uint32_t CommandHash;
            std::int32_t ParametersLength;

            Context(int ReadBufferSize)
                : ReadBufferOffset(0), ReadBufferLength(0), State(0), CommandNameLength(0), CommandName(u""), CommandHash(0), ParametersLength(0)
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
            bc->ClientEvent = [=](std::u16string CommandName, std::uint32_t CommandHash, std::vector<std::uint8_t> Parameters)
            {
                ByteArrayStream s;
                s.WriteString(CommandName);
                s.WriteUInt32(CommandHash);
                s.WriteInt32(static_cast<std::int32_t>(Parameters.size()));
                s.WriteBytes(Parameters);
                s.SetPosition(0);
                auto Bytes = s.ReadBytes(s.GetLength());
                {
                    std::unique_lock<std::mutex> Lock(c.WriteBufferLockee);
                    if (Transformer != nullptr)
                    {
                        Transformer->Transform(Bytes, 0, static_cast<int>(Bytes.size()));
                    }
                }
                c.WriteBuffer.push_back(std::make_shared<std::vector<std::uint8_t>>(Bytes));
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
            {
                std::unique_lock<std::mutex> Lock(c.WriteBufferLockee);
                auto WriteBuffer = c.WriteBuffer;
                c.WriteBuffer.clear();
                return WriteBuffer;
            }
        }

        std::shared_ptr<StreamedVirtualTransportClientHandleResult> Handle(int Count)
        {
            auto ret = StreamedVirtualTransportClientHandleResult::CreateContinue();

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
            std::u16string CommandName;
            std::uint32_t CommandHash;
            std::vector<std::uint8_t> Parameters;
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
                    ByteArrayStream s;
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
                    ByteArrayStream s;
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
                    ByteArrayStream s;
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
                    ByteArrayStream s;
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
                    std::vector<std::uint8_t> Parameters;
                    Parameters.resize(bc.ParametersLength, 0);
                    for (int k = 0; k < bc.ParametersLength; k += 1)
                    {
                        Parameters[k] = (*Buffer)[Position + k];
                    }
                    auto cmd = std::make_shared<Command>();
                    cmd->CommandName = bc.CommandName;
                    cmd->CommandHash = bc.CommandHash;
                    cmd->Parameters = Parameters;
                    auto r = std::make_shared<TryShiftResult>();
                    r->Command = cmd;
                    r->Position = Position + bc.ParametersLength;
                    bc.CommandNameLength = 0;
                    bc.CommandName = u"";
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
