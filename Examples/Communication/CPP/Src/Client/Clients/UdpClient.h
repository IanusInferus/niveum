#pragma once

#include "IContext.h"
#include "ISerializationClient.h"
#include "StreamedClient.h"

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/ExceptionStackTrace.h"
#include "BaseSystem/Cryptography.h"

#include <memory>
#include <cstdint>
#include <climits>
#include <cmath>
#include <cstring>
#include <vector>
#include <set>
#include <unordered_map>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <chrono>
#include <asio.hpp>
#include <asio/steady_timer.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    class UdpClient
    {
    private:
        std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient;
        asio::ip::udp::endpoint RemoteEndPoint;
        std::shared_ptr<BaseSystem::LockedVariable<bool>> IsRunningValue;
    public:
        bool IsRunning()
        {
            return IsRunningValue->Check<bool>([](bool v) { return v; });
        }
    private:
        BaseSystem::LockedVariable<int> SessionIdValue;
    public:
        int SessionId()
        {
            return SessionIdValue.Check<int>([](int v) { return v; });
        }
    private:
        enum ConnectionState
        {
            ConnectionState_Initial,
            ConnectionState_Connecting,
            ConnectionState_Connected
        };
        BaseSystem::LockedVariable<ConnectionState> ConnectionStateValue;
    public:
        bool IsConnected()
        {
            return ConnectionStateValue.Check<ConnectionState>([](ConnectionState v){ return v; }) == ConnectionState_Connected;
        }
    private:
        BaseSystem::LockedVariable<std::shared_ptr<class SecureContext>> SecureContextValue;
    public:
        std::shared_ptr<class SecureContext> SecureContext()
        {
            return SecureContextValue.Check<std::shared_ptr<class SecureContext>>([](std::shared_ptr<class SecureContext> v) { return v; });
        }
        void SecureContext(std::shared_ptr<class SecureContext> sc)
        {
            SecureContextValue.Update([=](std::shared_ptr<class SecureContext> v) { return sc; });
        }
    private:
        asio::io_service &io_service;
        asio::ip::udp::socket Socket;
        std::shared_ptr<std::vector<std::uint8_t>> ReadBuffer;

        static int MaxPacketLength() { return 1400; }
        static int ReadingWindowSize() { return 1024; }
        static int WritingWindowSize() { return 32; }
        static int IndexSpace() { return 65536; }
        static int CheckTimeout() { return 2000; }
        static int GetTimeoutMilliseconds(int ResentCount)
        {
            if (ResentCount == 0) { return 400; }
            if (ResentCount == 1) { return 800; }
            if (ResentCount == 2) { return 1600; }
            if (ResentCount == 3) { return 2000; }
            if (ResentCount == 4) { return 3000; }
            return 4000;
        }

        static void ArrayCopy(const std::vector<std::uint8_t> &Source, int SourceIndex, std::vector<std::uint8_t> &Destination, int DestinationIndex, int Length)
        {
            if (Length < 0) { throw std::logic_error("InvalidArgument"); }
            if (SourceIndex < 0) { throw std::logic_error("InvalidArgument"); }
            if (DestinationIndex < 0) { throw std::logic_error("InvalidArgument"); }
            if (SourceIndex + Length > static_cast<int>(Source.size())) { throw std::logic_error("InvalidArgument"); }
            if (DestinationIndex + Length > static_cast<int>(Destination.size())) { throw std::logic_error("InvalidArgument"); }
            if (Length == 0) { return; }
            std::memcpy(&Destination[DestinationIndex], &Source[SourceIndex], Length);
        }

        class Part
        {
        public:
            int Index;
            std::shared_ptr<std::vector<std::uint8_t>> Data;
            std::chrono::steady_clock::time_point ResendTime;
            int ResentCount;
        };
        class PartContext
        {
        private:
            int WindowSize;
        public:
            PartContext(int WindowSize)
                : MaxHandled(IndexSpace() - 1)
            {
                this->WindowSize = WindowSize;
            }

            int MaxHandled;
            std::unordered_map<int, std::shared_ptr<Part>> Parts;
            std::shared_ptr<Part> TryTakeFirstPart()
            {
                if (Parts.size() == 0) { return nullptr; }
                auto Successor = GetSuccessor(MaxHandled);
                if (Parts.count(Successor) > 0)
                {
                    auto Value = Parts[Successor];
                    Parts.erase(Successor);
                    MaxHandled = Successor;
                    return Value;
                }
                return nullptr;
            }
            bool IsEqualOrAfter(int New, int Original)
            {
                return ((New - Original + IndexSpace()) % IndexSpace()) < WindowSize;
            }
            static bool IsSuccessor(int New, int Original)
            {
                return ((New - Original + IndexSpace()) % IndexSpace()) == 1;
            }
            static int GetSuccessor(int Original)
            {
                return (Original + 1) % IndexSpace();
            }
            bool HasPart(int Index)
            {
                if (IsEqualOrAfter(MaxHandled, Index))
                {
                    return true;
                }
                if (Parts.count(Index) > 0)
                {
                    return true;
                }
                return false;
            }
            bool TryPushPart(int Index, std::shared_ptr<std::vector<std::uint8_t>> Data, int Offset, int Length)
            {
                if (((Index - MaxHandled + IndexSpace()) % IndexSpace()) >= WindowSize)
                {
                    return false;
                }
                auto b = std::make_shared<std::vector<std::uint8_t>>();
                b->resize(Length, 0);
                ArrayCopy(*Data, Offset, *b, 0, Length);
                auto p = std::make_shared<Part>();
                p->Index = Index;
                p->Data = b;
                p->ResendTime = std::chrono::steady_clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
                p->ResentCount = 0;
                Parts[Index] = p;
                return true;
            }
            bool TryPushPart(int Index, std::shared_ptr<std::vector<std::uint8_t>> Data)
            {
                if (((Index - MaxHandled + IndexSpace()) % IndexSpace()) >= WindowSize)
                {
                    return false;
                }
                auto p = std::make_shared<Part>();
                p->Index = Index;
                p->Data = Data;
                p->ResendTime = std::chrono::steady_clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
                p->ResentCount = 0;
                Parts[Index] = p;
                return true;
            }

            void Acknowledge(int Index, const std::vector<int> &Indices, int MaxWritten)
            {
                // Parts (= [MaxHandled, MaxWritten]
                // Index (- [MaxHandled, MaxWritten]
                // Indices (= (Index, MaxWritten]
                // |[MaxHandled, MaxWritten]| < WindowSize
                // any i (- [0, IndexSpace - 1]

                if (MaxWritten == MaxHandled) { return; }
                if (!IsEqualOrAfter(MaxWritten, MaxHandled)) { return; }
                if ((Index < 0) || (Index >= IndexSpace())) { return; }
                if (!IsEqualOrAfter(Index, MaxHandled)) { return; }
                if (!IsEqualOrAfter(MaxWritten, Index)) { return; }
                for (auto i : Indices)
                {
                    if ((i < 0) || (i >= IndexSpace())) { return; }
                    if (IsEqualOrAfter(Index, i)) { return; }
                    if (!IsEqualOrAfter(MaxWritten, i)) { return; }
                }

                while (MaxHandled != Index)
                {
                    auto i = GetSuccessor(MaxHandled);
                    if (Parts.count(i) > 0)
                    {
                        Parts.erase(i);
                    }
                    MaxHandled = i;
                }
                for (auto i : Indices)
                {
                    if (Parts.count(i) > 0)
                    {
                        Parts.erase(i);
                    }
                }
                while (MaxHandled != MaxWritten)
                {
                    auto i = GetSuccessor(MaxHandled);
                    if (Parts.count(i) > 0)
                    {
                        break;
                    }
                    MaxHandled = i;
                }
            }

            void ForEachTimedoutPacket(int SessionId, std::chrono::steady_clock::time_point Time, std::function<void(int, std::shared_ptr<std::vector<std::uint8_t>>)> f)
            {
                for (auto p : Parts)
                {
                    auto Key = std::get<0>(p);
                    auto Value = std::get<1>(p);
                    if (Value->ResendTime <= Time)
                    {
                        f(Key, Value->Data);
                        Value->ResendTime = std::chrono::steady_clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(Value->ResentCount));
                        Value->ResentCount += 1;
                    }
                }
            }
        };
        class UdpReadContext
        {
        public:
            std::shared_ptr<PartContext> Parts;
            std::set<int> NotAcknowledgedIndices;
            std::chrono::steady_clock::time_point LastCheck;
            UdpReadContext()
                : LastCheck(std::chrono::steady_clock::now())
            {
            }
        };
        class UdpWriteContext
        {
        public:
            std::shared_ptr<PartContext> Parts;
            int WritenIndex;
            std::shared_ptr<asio::steady_timer> Timer;
        };
        BaseSystem::LockedVariable<std::shared_ptr<UdpReadContext>> RawReadingContext;
        BaseSystem::LockedVariable<std::shared_ptr<UdpWriteContext>> CookedWritingContext;

#if _MSC_VER
        class connection_reset_command
        {
        public:
            unsigned long b; //false

            connection_reset_command()
                : b(0)
            {
            }

            int name() const
            {
                return SIO_UDP_CONNRESET;
            }

            void *data()
            {
                return reinterpret_cast<void *>(&b);
            }
        };
#endif

    public:
        UdpClient(asio::io_service &io_service, asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient, std::function<void(asio::system_error)> ExceptionHandler = nullptr)
            : io_service(io_service), Socket(io_service), IsRunningValue(nullptr), SessionIdValue(0), ConnectionStateValue(ConnectionState_Initial), SecureContextValue(nullptr), RawReadingContext(nullptr), CookedWritingContext(nullptr), IsDisposed(false)
        {
            this->IsRunningValue = std::make_shared<BaseSystem::LockedVariable<bool>>(false);
            ReadBuffer = std::make_shared<std::vector<std::uint8_t>>();
            ReadBuffer->resize(MaxPacketLength(), 0);
            RawReadingContext.Update([=](std::shared_ptr<UdpReadContext> cc)
            {
                auto c = std::make_shared<UdpReadContext>();
                c->Parts = std::make_shared<PartContext>(ReadingWindowSize());
                return c;
            });
            CookedWritingContext.Update([=](std::shared_ptr<UdpWriteContext> cc)
            {
                auto c = std::make_shared<UdpWriteContext>();
                c->Parts = std::make_shared<PartContext>(WritingWindowSize());
                c->WritenIndex = IndexSpace() - 1;
                c->Timer = nullptr;
                return c;
            });

            this->RemoteEndPoint = RemoteEndPoint;
            this->VirtualTransportClient = VirtualTransportClient;

            auto IsRunningValue = this->IsRunningValue;
            VirtualTransportClient->ClientMethod = [=]()
            {
                IsRunningValue->DoAction([=](bool b)
                {
                    if (!b) { return; }
                    OnWrite(*this->VirtualTransportClient, [=]() {}, [=](asio::error_code se)
                    {
                        if (ExceptionHandler != nullptr)
                        {
                            ExceptionHandler(asio::system_error(se));
                        }
                        else
                        {
                            throw asio::system_error(se);
                        }
                    });
                });
            };
        }

        virtual ~UdpClient()
        {
            Close();
        }

    private:
        void OnWrite(IStreamedVirtualTransportClient &vtc, std::function<void()> OnSuccess, std::function<void(asio::error_code)> OnFailure)
        {
            auto IsRunningValue = this->IsRunningValue;

            auto ByteArrays = vtc.TakeWriteBuffer();
            int TotalLength = 0;
            for (auto b : ByteArrays)
            {
                TotalLength += static_cast<int>(b->size());
            }
            auto WriteBuffer = std::make_shared<std::vector<std::uint8_t>>();
            WriteBuffer->resize(GetMinNotLessPowerOfTwo(TotalLength), 0);
            int Offset = 0;
            for (auto b : ByteArrays)
            {
                ArrayCopy(*b, 0, *WriteBuffer, Offset, static_cast<int>(b->size()));
                Offset += static_cast<int>(b->size());
            }
            auto RemoteEndPoint = this->RemoteEndPoint;
            int SessionId = 0;
            ConnectionState State = ConnectionState_Initial;
            this->ConnectionStateValue.Update([&](ConnectionState v)
            {
                SessionId = this->SessionId();
                State = v;
                if (v == ConnectionState_Initial)
                {
                    return ConnectionState_Connecting;
                }
                return v;
            });
            if (State == ConnectionState_Connecting)
            {
                throw std::logic_error("InvalidOperation");
            }
            auto sc = this->SecureContextValue.Check<std::shared_ptr<Client::SecureContext>>([](std::shared_ptr<Client::SecureContext> v) { return v; });
            std::vector<int> Indices;
            RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
            {
                if (c->NotAcknowledgedIndices.size() == 0) { return; }
                auto MaxHandled = c->Parts->MaxHandled;
                std::vector<int> Acknowledged;
                for (auto i : c->NotAcknowledgedIndices)
                {
                    if (c->Parts->IsEqualOrAfter(MaxHandled, i))
                    {
                        Acknowledged.push_back(i);
                    }
                    else if (PartContext::IsSuccessor(i, MaxHandled))
                    {
                        Acknowledged.push_back(i);
                        MaxHandled = i;
                    }
                }
                for (auto i : Acknowledged)
                {
                    c->NotAcknowledgedIndices.erase(i);
                }
                Indices.push_back(MaxHandled);
                for (auto i : c->NotAcknowledgedIndices)
                {
                    Indices.push_back(i);
                }
                c->NotAcknowledgedIndices.clear();
            });
            if ((ByteArrays.size() == 0) && (Indices.size() == 0))
            {
                return;
            }
            auto se = asio::error_code();
            std::vector<std::shared_ptr<std::vector<std::uint8_t>>> Parts;
            CookedWritingContext.DoAction([&, IsRunningValue](std::shared_ptr<UdpWriteContext> c)
            {
                auto Time = std::chrono::steady_clock::now();
                auto WritingOffset = 0;
                while (WritingOffset < TotalLength)
                {
                    auto Index = PartContext::GetSuccessor(c->WritenIndex);

                    auto NumIndex = static_cast<int>(Indices.size());
                    if (NumIndex > 0xFFFF)
                    {
                        se = asio::error::no_buffer_space;
                        return;
                    }

                    auto IsACK = NumIndex > 0;
                    auto Flag = 0;
                    if (State == ConnectionState_Initial)
                    {
                        Flag |= 4; //INI
                        IsACK = false;
                    }

                    auto Length = std::min(12 + (IsACK ? 2 + NumIndex * 2 : 0) + TotalLength - WritingOffset, MaxPacketLength());
                    auto DataLength = Length - (12 + (IsACK ? 2 + NumIndex * 2 : 0));
                    if (DataLength < 0)
                    {
                        se = asio::error::no_buffer_space;
                        return;
                    }
                    auto Buffer = std::make_shared<std::vector<std::uint8_t>>();
                    Buffer->resize(Length, 0);
                    (*Buffer)[0] = static_cast<std::uint8_t>(SessionId & 0xFF);
                    (*Buffer)[1] = static_cast<std::uint8_t>((SessionId >> 8) & 0xFF);
                    (*Buffer)[2] = static_cast<std::uint8_t>((SessionId >> 16) & 0xFF);
                    (*Buffer)[3] = static_cast<std::uint8_t>((SessionId >> 24) & 0xFF);

                    if (IsACK)
                    {
                        Flag |= 1; //ACK
                        (*Buffer)[12] = static_cast<std::uint8_t>(NumIndex & 0xFF);
                        (*Buffer)[13] = static_cast<std::uint8_t>((NumIndex >> 8) & 0xFF);
                        int j = 0;
                        for (auto i : Indices)
                        {
                            (*Buffer)[14 + j * 2] = static_cast<std::uint8_t>(i & 0xFF);
                            (*Buffer)[14 + j * 2 + 1] = static_cast<std::uint8_t>((i >> 8) & 0xFF);
                            j += 1;
                        }
                        Indices.clear();
                    }

                    ArrayCopy(*WriteBuffer, WritingOffset, *Buffer, 12 + (IsACK ? 2 + NumIndex * 2 : 0), DataLength);
                    WritingOffset += DataLength;

                    if (sc != nullptr)
                    {
                        Flag |= 2; //ENC
                    }
                    (*Buffer)[4] = static_cast<std::uint8_t>(Flag & 0xFF);
                    (*Buffer)[5] = static_cast<std::uint8_t>((Flag >> 8) & 0xFF);
                    (*Buffer)[6] = static_cast<std::uint8_t>(Index & 0xFF);
                    (*Buffer)[7] = static_cast<std::uint8_t>((Index >> 8) & 0xFF);

                    std::int32_t Verification = 0;
                    if (sc != nullptr)
                    {
                        std::vector<std::uint8_t> SHABuffer;
                        SHABuffer.resize(4);
                        ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                        auto SHA256 = Algorithms::Cryptography::SHA256(SHABuffer);
                        std::vector<std::uint8_t> Key;
                        Key.resize(sc->ClientToken.size() + SHA256.size());
                        ArrayCopy(sc->ClientToken, 0, Key, 0, static_cast<int>(sc->ClientToken.size()));
                        ArrayCopy(SHA256, 0, Key, static_cast<int>(sc->ClientToken.size()), static_cast<int>(SHA256.size()));
                        auto HMACBytes = Algorithms::Cryptography::HMACSHA256Simple(Key, *Buffer);
                        HMACBytes.resize(4);
                        Verification = HMACBytes[0] | (static_cast<std::int32_t>(HMACBytes[1]) << 8) | (static_cast<std::int32_t>(HMACBytes[2]) << 16) | (static_cast<std::int32_t>(HMACBytes[3]) << 24);
                    }
                    else
                    {
                        Verification = Algorithms::Cryptography::CRC32(*Buffer);
                    }

                    (*Buffer)[8] = static_cast<std::uint8_t>(Verification & 0xFF);
                    (*Buffer)[9] = static_cast<std::uint8_t>((Verification >> 8) & 0xFF);
                    (*Buffer)[10] = static_cast<std::uint8_t>((Verification >> 16) & 0xFF);
                    (*Buffer)[11] = static_cast<std::uint8_t>((Verification >> 24) & 0xFF);

                    auto Part = std::make_shared<UdpClient::Part>();
                    Part->Index = Index;
                    Part->ResendTime = Time + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
                    Part->Data = Buffer;
                    Part->ResentCount = 0;
                    if (!c->Parts->TryPushPart(Index, Buffer))
                    {
                        se = asio::error::no_buffer_space;
                        return;
                    }
                    Parts.push_back(Part->Data);

                    c->WritenIndex = Index;
                }
                if (c->Timer == nullptr)
                {
                    c->Timer = std::make_shared<asio::steady_timer>(io_service);
                    c->Timer->expires_from_now(std::chrono::milliseconds(CheckTimeout()));
                    c->Timer->async_wait([=](const asio::error_code& error)
                    {
                        if (!error)
                        {
                            Check(IsRunningValue);
                        }
                    });
                }
            });
            for (auto p : Parts)
            {
                try
                {
                    SendPacket(RemoteEndPoint, p);
                }
                catch (...)
                {
                    se = asio::error::interrupted;
                    break;
                }
            }
            if (se)
            {
                OnFailure(se);
            }
            else
            {
                OnSuccess();
            }
        }

        void Check(std::shared_ptr<BaseSystem::LockedVariable<bool>> IsRunningValue)
        {
            IsRunningValue->DoAction([=](bool b)
            {
                if (!b) { return; }
                auto IsRunning = b;

                auto RemoteEndPoint = this->RemoteEndPoint;
                int SessionId = 0;
                this->ConnectionStateValue.Check<ConnectionState>([&](ConnectionState v)
                {
                    SessionId = this->SessionId();
                    return v;
                });
                auto sc = this->SecureContextValue.Check<std::shared_ptr<Client::SecureContext>>([](std::shared_ptr<Client::SecureContext> v) { return v; });
                std::vector<int> Indices;
                RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
                {
                    if (c->NotAcknowledgedIndices.size() == 0) { return; }
                    auto CurrentTime = std::chrono::steady_clock::now();
                    if (std::chrono::duration<double, std::chrono::milliseconds::period>(CurrentTime - c->LastCheck).count() + 1 < CheckTimeout()) { return; }
                    c->LastCheck = CurrentTime;
                    auto NotAcknowledgedIndices = std::set<int>(c->NotAcknowledgedIndices.begin(), c->NotAcknowledgedIndices.end());
                    auto MaxHandled = c->Parts->MaxHandled;
                    std::vector<int> Acknowledged;
                    for (auto i : NotAcknowledgedIndices)
                    {
                        if (c->Parts->IsEqualOrAfter(MaxHandled, i))
                        {
                            Acknowledged.push_back(i);
                        }
                        else if (PartContext::IsSuccessor(i, MaxHandled))
                        {
                            Acknowledged.push_back(i);
                            MaxHandled = i;
                        }
                    }
                    for (auto i : Acknowledged)
                    {
                        NotAcknowledgedIndices.erase(i);
                    }
                    Indices.push_back(MaxHandled);
                    for (auto i : NotAcknowledgedIndices)
                    {
                        Indices.push_back(i);
                    }
                });

                std::shared_ptr<asio::steady_timer> Timer = nullptr;
                std::vector<std::shared_ptr<std::vector<std::uint8_t>>> Parts;
                CookedWritingContext.DoAction([&, IsRunningValue](std::shared_ptr<UdpWriteContext> cc)
                {
                    if (cc->Timer == nullptr) { return; }
                    Timer = cc->Timer;
                    cc->Timer = nullptr;
                    if (!IsRunning) { return; }

                    if (Indices.size() > 0)
                    {
                        auto Index = Indices[0];

                        auto NumIndex = static_cast<int>(Indices.size());
                        if (NumIndex > 0xFFFF)
                        {
                            return;
                        }

                        auto Flag = 8; //AUX

                        auto Length = 12 + 2 + NumIndex * 2;
                        if (Length > MaxPacketLength())
                        {
                            return;
                        }
                        auto Buffer = std::make_shared<std::vector<std::uint8_t>>();
                        Buffer->resize(Length, 0);
                        (*Buffer)[0] = static_cast<std::uint8_t>(SessionId & 0xFF);
                        (*Buffer)[1] = static_cast<std::uint8_t>((SessionId >> 8) & 0xFF);
                        (*Buffer)[2] = static_cast<std::uint8_t>((SessionId >> 16) & 0xFF);
                        (*Buffer)[3] = static_cast<std::uint8_t>((SessionId >> 24) & 0xFF);

                        Flag |= 1; //ACK
                        (*Buffer)[12] = static_cast<std::uint8_t>(NumIndex & 0xFF);
                        (*Buffer)[13] = static_cast<std::uint8_t>((NumIndex >> 8) & 0xFF);
                        int j = 0;
                        for (auto i : Indices)
                        {
                            (*Buffer)[14 + j * 2] = static_cast<std::uint8_t>(i & 0xFF);
                            (*Buffer)[14 + j * 2 + 1] = static_cast<std::uint8_t>((i >> 8) & 0xFF);
                            j += 1;
                        }
                        Indices.clear();

                        if (sc != nullptr)
                        {
                            Flag |= 2; //ENC
                        }
                        (*Buffer)[4] = static_cast<std::uint8_t>(Flag & 0xFF);
                        (*Buffer)[5] = static_cast<std::uint8_t>((Flag >> 8) & 0xFF);
                        (*Buffer)[6] = static_cast<std::uint8_t>(Index & 0xFF);
                        (*Buffer)[7] = static_cast<std::uint8_t>((Index >> 8) & 0xFF);

                        std::int32_t Verification = 0;
                        if (sc != nullptr)
                        {
                            std::vector<std::uint8_t> SHABuffer;
                            SHABuffer.resize(4);
                            ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                            auto SHA256 = Algorithms::Cryptography::SHA256(SHABuffer);
                            std::vector<std::uint8_t> Key;
                            Key.resize(sc->ClientToken.size() + SHA256.size());
                            ArrayCopy(sc->ClientToken, 0, Key, 0, static_cast<int>(sc->ClientToken.size()));
                            ArrayCopy(SHA256, 0, Key, static_cast<int>(sc->ClientToken.size()), static_cast<int>(SHA256.size()));
                            auto HMACBytes = Algorithms::Cryptography::HMACSHA256Simple(Key, *Buffer);
                            HMACBytes.resize(4);
                            Verification = HMACBytes[0] | (static_cast<std::int32_t>(HMACBytes[1]) << 8) | (static_cast<std::int32_t>(HMACBytes[2]) << 16) | (static_cast<std::int32_t>(HMACBytes[3]) << 24);
                        }
                        else
                        {
                            Verification = Algorithms::Cryptography::CRC32(*Buffer);
                        }

                        (*Buffer)[8] = static_cast<std::uint8_t>(Verification & 0xFF);
                        (*Buffer)[9] = static_cast<std::uint8_t>((Verification >> 8) & 0xFF);
                        (*Buffer)[10] = static_cast<std::uint8_t>((Verification >> 16) & 0xFF);
                        (*Buffer)[11] = static_cast<std::uint8_t>((Verification >> 24) & 0xFF);

                        Parts.push_back(Buffer);
                    }

                    if (cc->Parts->Parts.size() == 0) { return; }
                    auto t = std::chrono::steady_clock::now();
                    cc->Parts->ForEachTimedoutPacket(SessionId, t, [&](int i, std::shared_ptr<std::vector<std::uint8_t>> d) { Parts.push_back(d); });
                    auto Wait = std::numeric_limits<int>::max();
                    for (auto Pair : cc->Parts->Parts)
                    {
                        auto p = std::get<1>(Pair);
                        auto pWait = std::chrono::duration<double, std::chrono::milliseconds::period>(p->ResendTime - t).count() + 1;
                        if (pWait < Wait)
                        {
                            Wait = static_cast<int>(pWait);
                        }
                    }
                    cc->Timer = std::make_shared<asio::steady_timer>(io_service);
                    cc->Timer->expires_from_now(std::chrono::milliseconds(Wait));
                    cc->Timer->async_wait([=](const asio::error_code& error)
                    {
                        if (!error)
                        {
                            Check(IsRunningValue);
                        }
                    });
                });
                if (Timer != nullptr)
                {
                    Timer->cancel();
                    Timer = nullptr;
                }
                for (auto p : Parts)
                {
                    try
                    {
                        SendPacket(RemoteEndPoint, p);
                    }
                    catch (...)
                    {
                        break;
                    }
                }
            });
        }

        void SendPacket(asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<std::vector<std::uint8_t>> Data)
        {
            Socket.send_to(asio::buffer(*Data), RemoteEndPoint);
        }

        static int GetMinNotLessPowerOfTwo(int v)
        {
            //计算不小于TotalLength的最小2的幂
            if (v < 1) { return 1; }
            auto n = 0;
            auto z = v - 1;
            while (z != 0)
            {
                z >>= 1;
                n += 1;
            }
            auto Value = 1 << n;
            if (Value == 0) { throw std::logic_error("InvalidOperationException"); }
            return Value;
        }

    public:
        void Connect()
        {
            auto IsRunningValue = this->IsRunningValue;
            IsRunningValue->Update([&](bool b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }

                if (RemoteEndPoint.address().is_v4())
                {
                    Socket.open(asio::ip::udp::v4());
                }
                else
                {
                    Socket.open(asio::ip::udp::v6());
                }

#if _MSC_VER
                //在Windows下关闭SIO_UDP_CONNRESET报告，防止接受数据出错
                //http://support.microsoft.com/kb/263823/en-us
                connection_reset_command command;
                Socket.io_control(command);
#endif

                if (RemoteEndPoint.address().is_v4())
                {
                    Socket.bind(asio::ip::udp::endpoint(asio::ip::address_v4::any(), 0));
                }
                else
                {
                    Socket.bind(asio::ip::udp::endpoint(asio::ip::address_v6::any(), 0));
                }

                return true;
            });
        }

        /// <summary>异步连接</summary>
        /// <param name="Completed">正常连接处理函数</param>
        /// <param name="UnknownFaulted">未知错误处理函数</param>
        void ConnectAsync(std::function<void(void)> Completed, std::function<void(const asio::error_code &)> UnknownFaulted)
        {
            try
            {
                Connect();
            }
            catch (asio::system_error &e)
            {
                UnknownFaulted(e.code());
                return;
            }
            Completed();
        }

    private:
        static bool IsSocketErrorKnown(const asio::error_code &se)
        {
            if (se == asio::error::connection_aborted) { return true; }
            if (se == asio::error::connection_reset) { return true; }
            if (se == asio::error::eof) { return true; }
            if (se == asio::error::operation_aborted) { return true; }
            return false;
        }

        void CompletedSocket(std::shared_ptr<std::vector<std::uint8_t>> Buffer, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const std::u16string &)> UnknownFaulted)
        {
            auto a = [&]()
            {
                if (Buffer->size() < 12)
                {
                    return;
                }
                auto SessionId = (*Buffer)[0] | (static_cast<std::int32_t>((*Buffer)[1]) << 8) | (static_cast<std::int32_t>((*Buffer)[2]) << 16) | (static_cast<std::int32_t>((*Buffer)[3]) << 24);
                auto Flag = (*Buffer)[4] | (static_cast<std::int32_t>((*Buffer)[5]) << 8);
                auto Index = (*Buffer)[6] | (static_cast<std::int32_t>((*Buffer)[7]) << 8);
                auto Verification = (*Buffer)[8] | (static_cast<std::int32_t>((*Buffer)[9]) << 8) | (static_cast<std::int32_t>((*Buffer)[10]) << 16) | (static_cast<std::int32_t>((*Buffer)[11]) << 24);
                (*Buffer)[8] = 0;
                (*Buffer)[9] = 0;
                (*Buffer)[10] = 0;
                (*Buffer)[11] = 0;

                auto IsEncrypted = (Flag & 2) != 0;
                auto sc = this->SecureContextValue.Check<std::shared_ptr<Client::SecureContext>>([](std::shared_ptr<Client::SecureContext> v) { return v; });
                if ((sc != nullptr) != IsEncrypted)
                {
                    return;
                }

                if (IsEncrypted)
                {
                    std::vector<std::uint8_t> SHABuffer;
                    SHABuffer.resize(4);
                    ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                    auto SHA256 = Algorithms::Cryptography::SHA256(SHABuffer);
                    std::vector<std::uint8_t> Key;
                    Key.resize(sc->ServerToken.size() + SHA256.size());
                    ArrayCopy(sc->ServerToken, 0, Key, 0, static_cast<int>(sc->ServerToken.size()));
                    ArrayCopy(SHA256, 0, Key, static_cast<int>(sc->ServerToken.size()), static_cast<int>(SHA256.size()));
                    auto HMACBytes = Algorithms::Cryptography::HMACSHA256Simple(Key, *Buffer);
                    HMACBytes.resize(4);
                    auto HMAC = HMACBytes[0] | (static_cast<std::int32_t>(HMACBytes[1]) << 8) | (static_cast<std::int32_t>(HMACBytes[2]) << 16) | (static_cast<std::int32_t>(HMACBytes[3]) << 24);
                    if (HMAC != Verification) { return; }
                }
                else
                {
                    //如果Flag中不包含ENC，则验证CRC32
                    if (Algorithms::Cryptography::CRC32(*Buffer) != Verification) { return; }

                    //只有尚未连接时可以设定
                    auto Close = false;
                    ConnectionStateValue.Update([&](ConnectionState v)
                    {
                        if (v == ConnectionState_Connecting)
                        {
                            this->SessionIdValue.Update([=](int vv) { return SessionId; });
                            return ConnectionState_Connected;
                        }
                        else
                        {
                            if (SessionId != this->SessionId())
                            {
                                Close = true;
                            }
                            return v;
                        }
                    });
                    if (Close)
                    {
                        return;
                    }
                }

                int Offset = 12;
                std::shared_ptr<std::vector<int>> Indices = nullptr;
                if ((Flag & 1) != 0)
                {
                    if (Buffer->size() < 14)
                    {
                        return;
                    }
                    auto NumIndex = (*Buffer)[Offset] | (static_cast<std::int32_t>((*Buffer)[Offset + 1]) << 8);
                    if (static_cast<int>(Buffer->size()) < 14 + NumIndex * 2)
                    {
                        return;
                    }
                    if (NumIndex > WritingWindowSize()) //若Index数量较大，则丢弃包
                    {
                        return;
                    }
                    Offset += 2;
                    Indices = std::make_shared<std::vector<int>>();
                    Indices->resize(NumIndex, 0);
                    for (int k = 0; k < NumIndex; k += 1)
                    {
                        (*Indices)[k] = (*Buffer)[Offset + k * 2] | (static_cast<std::int32_t>((*Buffer)[Offset + k * 2 + 1]) << 8);
                    }
                    Offset += NumIndex * 2;
                }

                auto Length = static_cast<std::int32_t>(Buffer->size()) - Offset;

                if ((Indices != nullptr) && (Indices->size() > 0))
                {
                    CookedWritingContext.DoAction([&](std::shared_ptr<UdpWriteContext> c)
                    {
                        auto First = (*Indices)[0];
                        Indices->erase(Indices->begin());
                        c->Parts->Acknowledge(First, *Indices, c->WritenIndex);
                    });
                }

                bool Pushed = false;
                std::vector<std::shared_ptr<std::vector<std::uint8_t>>> Parts;
                RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
                {
                    if (c->Parts->HasPart(Index))
                    {
                        Pushed = true;
                        return;
                    }
                    Pushed = c->Parts->TryPushPart(Index, Buffer, Offset, Length);
                    if (Pushed)
                    {
                        c->NotAcknowledgedIndices.insert(Index);
                        std::vector<int> Acknowledged;
                        for (auto i : c->NotAcknowledgedIndices)
                        {
                            if (c->Parts->IsEqualOrAfter(c->Parts->MaxHandled, i))
                            {
                                Acknowledged.push_back(i);
                            }
                        }
                        for (auto i : Acknowledged)
                        {
                            c->NotAcknowledgedIndices.erase(i);
                        }

                        while (true)
                        {
                            auto p = c->Parts->TryTakeFirstPart();
                            if (p == nullptr) { break; }
                            Parts.push_back(p->Data);
                        }
                    }
                });

                for (auto p : Parts)
                {
                    auto ReadBuffer = VirtualTransportClient->GetReadBuffer();
                    auto ReadBufferLength = VirtualTransportClient->GetReadBufferOffset() + VirtualTransportClient->GetReadBufferLength();
                    if (static_cast<int>(p->size()) > static_cast<int>(ReadBuffer->size()) - ReadBufferLength)
                    {
                        UnknownFaulted(systemToUtf16(asio::error_code(asio::error::no_buffer_space).message()));
                        return;
                    }
                    ArrayCopy(*p, 0, *ReadBuffer, ReadBufferLength, static_cast<int>(p->size()));

                    auto c = p->size();
                    while (true)
                    {
                        auto r = VirtualTransportClient->Handle(static_cast<int>(c));
                        if (r->OnContinue())
                        {
                            break;
                        }
                        else if (r->OnCommand())
                        {
                            DoResultHandle(r->Command->HandleResult);
                            auto RemainCount = VirtualTransportClient->GetReadBufferLength();
                            if (RemainCount <= 0)
                            {
                                break;
                            }
                            c = 0;
                        }
                        else
                        {
                            throw std::logic_error("InvalidOperationException");
                        }
                    }
                }
            };
            if (ExceptionStackTrace::IsDebuggerAttached())
            {
                a();
            }
            else
            {
                try
                {
                    ExceptionStackTrace::Execute(a);
                }
                catch (const asio::system_error &ex)
                {
                    auto Message = std::string() + typeid(*(&ex)).name() + "\r\n" + ex.code().message() + "\r\n" + ExceptionStackTrace::GetStackTrace();
                    UnknownFaulted(systemToUtf16(Message));
                }
                catch (const std::exception &ex)
                {
                    auto Message = std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace();
                    UnknownFaulted(systemToUtf16(Message));
                }
            }
        }

        void ReceiveAsyncInner(std::shared_ptr<BaseSystem::LockedVariable<bool>> IsRunningValue, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const std::u16string &)> UnknownFaulted)
        {
            IsRunningValue->DoAction([=](bool b)
            {
                if (!b) { return; }
                auto ReadBuffer = this->ReadBuffer;
                auto ServerEndPoint = std::make_shared<asio::ip::udp::endpoint>(this->RemoteEndPoint);
                auto ReadHandler = [=](const asio::error_code &se, std::size_t Count)
                {
                    if (se)
                    {
                        if (IsSocketErrorKnown(se)) { return; }
                        UnknownFaulted(systemToUtf16(se.message()));
                        return;
                    }
                    auto IsRunning = IsRunningValue->Check<bool>([=](bool b)
                    {
                        if (!b) { return b; }
                        if (*ServerEndPoint != this->RemoteEndPoint) { return b; }
                        auto Buffer = std::make_shared<std::vector<std::uint8_t>>();
                        Buffer->resize(Count, 0);
                        ArrayCopy(*ReadBuffer, 0, *Buffer, 0, static_cast<int>(Count));
                        CompletedSocket(Buffer, DoResultHandle, UnknownFaulted);
                        Buffer = nullptr;
                        return b;
                    });
                    if (!IsRunning)
                    {
                        return;
                    }
                    ReceiveAsyncInner(IsRunningValue, DoResultHandle, UnknownFaulted);
                };
                Socket.async_receive_from(asio::buffer(*ReadBuffer), *ServerEndPoint, ReadHandler);
            });
        }
    public:
        /// <summary>接收消息</summary>
        /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
        /// <param name="UnknownFaulted">未知错误处理函数</param>
        void ReceiveAsync(std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const std::u16string &)> UnknownFaulted)
        {
            ReceiveAsyncInner(this->IsRunningValue, DoResultHandle, UnknownFaulted);
        }

    private:
        bool IsDisposed;
    public:
        void Close()
        {
            auto IsRunningValue = this->IsRunningValue;
            IsRunningValue->Update([=](bool b)
            {
                if (IsDisposed) { return false; }
                IsDisposed = true;

                asio::error_code e;
                Socket.close(e);

                std::shared_ptr<asio::steady_timer> Timer = nullptr;
                CookedWritingContext.DoAction([&](std::shared_ptr<UdpWriteContext> c)
                {
                    if (c->Timer != nullptr)
                    {
                        Timer = c->Timer;
                        c->Timer = nullptr;
                    }
                });
                if (Timer != nullptr)
                {
                    Timer->cancel();
                    Timer = nullptr;
                }
                return false;
            });
        }
    };
}
