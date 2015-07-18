#pragma once

#include "IContext.h"
#include "ISerializationClient.h"
#include "StreamedClient.h"

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/Cryptography.h"

#include <memory>
#include <cstdint>
#include <climits>
#include <cmath>
#include <cstring>
#include <vector>
#include <set>
#include <map>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <chrono>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Client
{
    namespace Streamed
    {
        class UdpClient
        {
        private:
            std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient;
            boost::asio::ip::udp::endpoint RemoteEndPoint;
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
            void SetSecureContext(std::shared_ptr<class SecureContext> sc)
            {
                SecureContextValue.Update([=](std::shared_ptr<class SecureContext> v) { return sc; });
            }
        private:
            boost::asio::io_service &io_service;
            boost::asio::ip::udp::socket Socket;
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
                return 4100;
            }

            static void ArrayCopy(const std::vector<std::uint8_t> &Source, int SourceIndex, std::vector<std::uint8_t> &Destination, int DestinationIndex, int Length)
            {
                if (Length < 0) { throw std::logic_error("InvalidArgument"); }
                if (SourceIndex < 0) { throw std::logic_error("InvalidArgument"); }
                if (DestinationIndex < 0) { throw std::logic_error("InvalidArgument"); }
                if (SourceIndex + Length > static_cast<int>(Source.size())) { throw std::logic_error("InvalidArgument"); }
                if (DestinationIndex + Length > static_cast<int>(Destination.size())) { throw std::logic_error("InvalidArgument"); }
                memcpy(&Destination[DestinationIndex], &Source[SourceIndex], Length);
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
                std::map<int, std::shared_ptr<Part>> Parts;
                std::shared_ptr<Part> TryTakeFirstPart()
                {
                    if (Parts.size() == 0) { return nullptr; }
                    auto First = *Parts.begin();
                    auto Key = std::get<0>(First);
                    auto Value = std::get<1>(First);
                    if (IsSuccessor(Key, MaxHandled))
                    {
                        Parts.erase(Key);
                        MaxHandled = Key;
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
                    if (((Index - MaxHandled + IndexSpace()) % IndexSpace()) > WindowSize)
                    {
                        return false;
                    }
                    auto b = std::make_shared<std::vector<std::uint8_t>>();
                    b->resize(Length, 0);
                    ArrayCopy(*Data, Offset, *b, 0, Length);
                    auto p = std::make_shared<Part>();
                    p->Index = Index;
                    p->Data = b;
                    p->ResendTime = std::chrono::steady_clock::time_point::clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
                    p->ResentCount = 0;
                    Parts[Index] = p;
                    return true;
                }
                bool TryPushPart(int Index, std::shared_ptr<std::vector<std::uint8_t>> Data)
                {
                    if (((Index - MaxHandled + IndexSpace()) % IndexSpace()) > WindowSize)
                    {
                        return false;
                    }
                    auto p = std::make_shared<Part>();
                    p->Index = Index;
                    p->Data = Data;
                    p->ResendTime = std::chrono::steady_clock::time_point::clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
                    p->ResentCount = 0;
                    Parts[Index] = p;
                    return true;
                }

                void Acknowledge(int Index, const std::vector<int> &Indices, int WritenIndex)
                {
                    MaxHandled = Index;
                    while (true)
                    {
                        if (Parts.size() == 0) { return; }
                        auto First = *Parts.begin();
                        auto Key = std::get<0>(First);
                        auto Value = std::get<1>(First);
                        if (Key <= Index)
                        {
                            Parts.erase(Key);
                        }
                        if (Key >= Index)
                        {
                            break;
                        }
                    }
                    for (auto i : Indices)
                    {
                        if (Parts.count(i) > 0)
                        {
                            Parts.erase(i);
                        }
                    }
                    while ((MaxHandled != WritenIndex) && IsEqualOrAfter(WritenIndex, MaxHandled))
                    {
                        auto Next = GetSuccessor(MaxHandled);
                        if (Parts.count(Next) == 0)
                        {
                            MaxHandled = Next;
                        }
                        else
                        {
                            break;
                        }
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
                            Value->ResendTime = std::chrono::steady_clock::time_point::clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(Value->ResentCount));
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
                    : LastCheck(std::chrono::steady_clock::time_point::clock::now())
                {
                }
            };
            class UdpWriteContext
            {
            public:
                std::shared_ptr<PartContext> Parts;
                int WritenIndex;
                std::shared_ptr<boost::asio::deadline_timer> Timer;
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
            UdpClient(boost::asio::io_service &io_service, boost::asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<IStreamedVirtualTransportClient> VirtualTransportClient, std::function<void(boost::system::system_error)> ExceptionHandler = nullptr)
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
                        OnWrite(*this->VirtualTransportClient, [=]() {}, [=](boost::system::errc::errc_t se)
                        {
                            if (ExceptionHandler != nullptr)
                            {
                                ExceptionHandler(boost::system::system_error(se, boost::system::generic_category()));
                            }
                            else
                            {
                                throw boost::system::system_error(se, boost::system::generic_category());
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
            void OnWrite(IStreamedVirtualTransportClient &vtc, std::function<void()> OnSuccess, std::function<void(boost::system::errc::errc_t)> OnFailure)
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
                auto SecureContext = this->SecureContextValue.Check<std::shared_ptr<class SecureContext>>([](std::shared_ptr<class SecureContext> v) { return v; });
                std::vector<int> Indices;
                RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
                {
                    if (c->NotAcknowledgedIndices.size() == 0) { return; }
                    auto MaxHandled = c->Parts->MaxHandled;
                    while (c->NotAcknowledgedIndices.size() > 0)
                    {
                        auto First = *c->NotAcknowledgedIndices.begin();
                        if (c->Parts->IsEqualOrAfter(MaxHandled, First))
                        {
                            c->NotAcknowledgedIndices.erase(First);
                        }
                        else if (PartContext::IsSuccessor(First, MaxHandled))
                        {
                            c->NotAcknowledgedIndices.erase(First);
                            MaxHandled = First;
                        }
                        else
                        {
                            break;
                        }
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
                auto se = boost::system::errc::success;
                std::vector<std::shared_ptr<std::vector<std::uint8_t>>> Parts;
                CookedWritingContext.DoAction([&, IsRunningValue](std::shared_ptr<UdpWriteContext> c)
                {
                    auto Time = std::chrono::steady_clock::time_point::clock::now();
                    auto WritingOffset = 0;
                    while (WritingOffset < TotalLength)
                    {
                        auto Index = PartContext::GetSuccessor(c->WritenIndex);

                        auto NumIndex = static_cast<int>(Indices.size());
                        if (NumIndex > 0xFFFF)
                        {
                            se = boost::system::errc::no_buffer_space;
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

                        if (SecureContext != nullptr)
                        {
                            Flag |= 2; //ENC
                        }
                        (*Buffer)[4] = static_cast<std::uint8_t>(Flag & 0xFF);
                        (*Buffer)[5] = static_cast<std::uint8_t>((Flag >> 8) & 0xFF);
                        (*Buffer)[6] = static_cast<std::uint8_t>(Index & 0xFF);
                        (*Buffer)[7] = static_cast<std::uint8_t>((Index >> 8) & 0xFF);

                        std::int32_t Verification = 0;
                        if (SecureContext != nullptr)
                        {
                            std::vector<std::uint8_t> SHABuffer;
                            SHABuffer.resize(4);
                            ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                            auto SHA1 = Algorithms::Cryptography::SHA1(SHABuffer);
                            std::vector<std::uint8_t> Key;
                            Key.resize(SecureContext->ServerToken.size() + SHA1.size());
                            ArrayCopy(SecureContext->ServerToken, 0, Key, 0, static_cast<int>(SecureContext->ServerToken.size()));
                            ArrayCopy(SHA1, 0, Key, SecureContext->ServerToken.size(), static_cast<int>(SHA1.size()));
                            auto HMACBytes = Algorithms::Cryptography::HMACSHA1(Key, *Buffer);
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

                        auto Part = std::make_shared<class Part>();
                        Part->Index = Index;
                        Part->ResendTime = Time + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
                        Part->Data = Buffer;
                        Part->ResentCount = 0;
                        if (!c->Parts->TryPushPart(Index, Buffer))
                        {
                            se = boost::system::errc::no_buffer_space;
                            return;
                        }
                        Parts.push_back(Part->Data);

                        c->WritenIndex = Index;
                    }
                    if (c->Timer == nullptr)
                    {
                        c->Timer = std::make_shared<boost::asio::deadline_timer>(io_service);
                        c->Timer->expires_from_now(boost::posix_time::milliseconds(CheckTimeout()));
                        c->Timer->async_wait([=](const boost::system::error_code& error)
                        {
                            if (error == boost::system::errc::success)
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
                        se = boost::system::errc::interrupted;
                        break;
                    }
                }
                if (se != boost::system::errc::success)
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
                    auto SecureContext = this->SecureContextValue.Check<std::shared_ptr<class SecureContext>>([](std::shared_ptr<class SecureContext> v) { return v; });
                    std::vector<int> Indices;
                    RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
                    {
                        if (c->NotAcknowledgedIndices.size() == 0) { return; }
                        auto CurrentTime = std::chrono::steady_clock::time_point::clock::now();
                        if (std::chrono::duration<double, std::chrono::milliseconds::period>(CurrentTime - c->LastCheck).count() + 1 < CheckTimeout()) { return; }
                        c->LastCheck = CurrentTime;
                        auto NotAcknowledgedIndices = std::set<int>(c->NotAcknowledgedIndices.begin(), c->NotAcknowledgedIndices.end());
                        auto MaxHandled = c->Parts->MaxHandled;
                        while (NotAcknowledgedIndices.size() > 0)
                        {
                            auto First = *NotAcknowledgedIndices.begin();
                            if (c->Parts->IsEqualOrAfter(MaxHandled, First))
                            {
                                NotAcknowledgedIndices.erase(First);
                            }
                            else if (PartContext::IsSuccessor(First, MaxHandled))
                            {
                                NotAcknowledgedIndices.erase(First);
                                MaxHandled = First;
                            }
                            else
                            {
                                break;
                            }
                        }
                        Indices.push_back(MaxHandled);
                        for (auto i : NotAcknowledgedIndices)
                        {
                            Indices.push_back(i);
                        }
                    });

                    std::shared_ptr<boost::asio::deadline_timer> Timer = nullptr;
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

                            auto NumIndex = Indices.size();
                            if (NumIndex > 0xFFFF)
                            {
                                return;
                            }

                            auto Flag = 8; //AUX

                            auto Length = 12 + 2 + NumIndex * 2;
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

                            if (SecureContext != nullptr)
                            {
                                Flag |= 2; //ENC
                            }
                            (*Buffer)[4] = static_cast<std::uint8_t>(Flag & 0xFF);
                            (*Buffer)[5] = static_cast<std::uint8_t>((Flag >> 8) & 0xFF);
                            (*Buffer)[6] = static_cast<std::uint8_t>(Index & 0xFF);
                            (*Buffer)[7] = static_cast<std::uint8_t>((Index >> 8) & 0xFF);

                            std::int32_t Verification = 0;
                            if (SecureContext != nullptr)
                            {
                                std::vector<std::uint8_t> SHABuffer;
                                SHABuffer.resize(4);
                                ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                                auto SHA1 = Algorithms::Cryptography::SHA1(SHABuffer);
                                std::vector<std::uint8_t> Key;
                                Key.resize(SecureContext->ServerToken.size() + SHA1.size());
                                ArrayCopy(SecureContext->ServerToken, 0, Key, 0, static_cast<int>(SecureContext->ServerToken.size()));
                                ArrayCopy(SHA1, 0, Key, SecureContext->ServerToken.size(), static_cast<int>(SHA1.size()));
                                auto HMACBytes = Algorithms::Cryptography::HMACSHA1(Key, *Buffer);
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
                        auto t = std::chrono::steady_clock::time_point::clock::now();
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
                        cc->Timer = std::make_shared<boost::asio::deadline_timer>(io_service);
                        cc->Timer->expires_from_now(boost::posix_time::milliseconds(Wait));
                        cc->Timer->async_wait([=](const boost::system::error_code& error)
                        {
                            if (error == boost::system::errc::success)
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

            void SendPacket(boost::asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<std::vector<std::uint8_t>> Data)
            {
                Socket.send_to(boost::asio::buffer(*Data), RemoteEndPoint);
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
                        Socket.open(boost::asio::ip::udp::v4());
                    }
                    else
                    {
                        Socket.open(boost::asio::ip::udp::v6());
                    }

#if _MSC_VER
                    //在Windows下关闭SIO_UDP_CONNRESET报告，防止接受数据出错
                    //http://support.microsoft.com/kb/263823/en-us
                    Socket.io_control(connection_reset_command());
#endif

                    if (RemoteEndPoint.address().is_v4())
                    {
                        Socket.bind(boost::asio::ip::udp::endpoint(boost::asio::ip::address_v4::any(), 0));
                    }
                    else
                    {
                        Socket.bind(boost::asio::ip::udp::endpoint(boost::asio::ip::address_v6::any(), 0));
                    }

                    return true;
                });
            }

            /// <summary>异步连接</summary>
            /// <param name="Completed">正常连接处理函数</param>
            /// <param name="UnknownFaulted">未知错误处理函数</param>
            void ConnectAsync(boost::asio::ip::udp::endpoint RemoteEndPoint, std::function<void(void)> Completed, std::function<void(const boost::system::error_code &)> UnknownFaulted)
            {
                try
                {
                    Connect();
                }
                catch (boost::system::system_error &e)
                {
                    UnknownFaulted(e.code());
                    return;
                }
                Completed();
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

            void CompletedSocket(std::shared_ptr<std::vector<std::uint8_t>> Buffer, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
            {
                try
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
                    auto SecureContext = this->SecureContextValue.Check<std::shared_ptr<class SecureContext>>([](std::shared_ptr<class SecureContext> v) { return v; });
                    if ((SecureContext != nullptr) != IsEncrypted)
                    {
                        return;
                    }

                    if (IsEncrypted)
                    {
                        std::vector<std::uint8_t> SHABuffer;
                        SHABuffer.resize(4);
                        ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                        auto SHA1 = Algorithms::Cryptography::SHA1(SHABuffer);
                        std::vector<std::uint8_t> Key;
                        Key.resize(SecureContext->ClientToken.size() + SHA1.size());
                        ArrayCopy(SecureContext->ClientToken, 0, Key, 0, static_cast<int>(SecureContext->ClientToken.size()));
                        ArrayCopy(SHA1, 0, Key, SecureContext->ClientToken.size(), static_cast<int>(SHA1.size()));
                        auto HMACBytes = Algorithms::Cryptography::HMACSHA1(Key, *Buffer);
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
                    std::vector<int> Indices;
                    if ((Flag & 1) != 0)
                    {
                        auto NumIndex = (*Buffer)[Offset] | (static_cast<std::int32_t>((*Buffer)[Offset + 1]) << 8);
                        if (NumIndex > WritingWindowSize()) //若Index数量较大，则丢弃包
                        {
                            return;
                        }
                        Offset += 2;
                        Indices.resize(NumIndex, 0);
                        for (int k = 0; k < NumIndex; k += 1)
                        {
                            Indices[k] = (*Buffer)[Offset + k * 2] | (static_cast<std::int32_t>((*Buffer)[Offset + k * 2 + 1]) << 8);
                        }
                        Offset += NumIndex * 2;
                    }

                    auto Length = static_cast<std::int32_t>(Buffer->size()) - Offset;

                    if (Indices.size() > 0)
                    {
                        CookedWritingContext.DoAction([&](std::shared_ptr<UdpWriteContext> c)
                        {
                            auto First = Indices[0];
                            Indices.erase(Indices.begin());
                            c->Parts->Acknowledge(First, Indices, c->WritenIndex);
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
                            while (c->NotAcknowledgedIndices.size() > 0)
                            {
                                auto First = *c->NotAcknowledgedIndices.begin();
                                if (c->Parts->IsEqualOrAfter(c->Parts->MaxHandled, First))
                                {
                                    c->NotAcknowledgedIndices.erase(First);
                                }
                                else
                                {
                                    break;
                                }
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
                            UnknownFaulted(boost::system::error_code(boost::system::errc::no_buffer_space, boost::system::system_category()));
                            return;
                        }
                        ArrayCopy(*p, 0, *ReadBuffer, ReadBufferLength, static_cast<int>(p->size()));

                        auto c = p->size();
                        while (true)
                        {
                            auto r = VirtualTransportClient->Handle(c);
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
                }
                catch (boost::system::system_error &e)
                {
                    UnknownFaulted(e.code());
                    return;
                }
            }

            void ReceiveAsyncInner(std::shared_ptr<BaseSystem::LockedVariable<bool>> IsRunningValue, std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
            {
                IsRunningValue->DoAction([=](bool b)
                {
                    if (!b) { return; }
                    auto ReadBuffer = this->ReadBuffer;
                    auto ServerEndPoint = std::make_shared<boost::asio::ip::udp::endpoint>(this->RemoteEndPoint);
                    auto ReadHandler = [=](const boost::system::error_code &se, std::size_t Count)
                    {
                        if (se != boost::system::errc::success)
                        {
                            if (IsSocketErrorKnown(se)) { return; }
                            UnknownFaulted(se);
                            return;
                        }
                        auto IsRunning = IsRunningValue->Check<bool>([=](bool b)
                        {
                            if (!b) { return b; }
                            if (*ServerEndPoint != this->RemoteEndPoint) { return b; }
                            auto Buffer = std::make_shared<std::vector<std::uint8_t>>();
                            Buffer->resize(Count, 0);
                            ArrayCopy(*ReadBuffer, 0, *Buffer, 0, Count);
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
                    Socket.async_receive_from(boost::asio::buffer(*ReadBuffer), *ServerEndPoint, ReadHandler);
                });
            }
        public:
            /// <summary>接收消息</summary>
            /// <param name="DoResultHandle">运行处理消息函数，应保证不多线程同时访问BinarySocketClient</param>
            /// <param name="UnknownFaulted">未知错误处理函数</param>
            void ReceiveAsync(std::function<void(std::function<void(void)>)> DoResultHandle, std::function<void(const boost::system::error_code &)> UnknownFaulted)
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

                    try
                    {
                        Socket.close();
                    }
                    catch (...)
                    {
                    }
                    std::shared_ptr<boost::asio::deadline_timer> Timer = nullptr;
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
}
