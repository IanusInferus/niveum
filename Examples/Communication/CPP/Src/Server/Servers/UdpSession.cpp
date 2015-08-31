#include "UdpSession.h"
#include "UdpServer.h"

#include "BaseSystem/StringUtilities.h"
#include "BaseSystem/Times.h"
#include "BaseSystem/ExceptionStackTrace.h"
#include "Rc4PacketServerTransformer.h"

#include <cstring>
#include <chrono>
#include <stdexcept>
#include <typeinfo>

namespace Server
{
    UdpSession::UdpSession(UdpServer &Server, std::shared_ptr<asio::ip::udp::socket> ServerSocket, asio::ip::udp::endpoint RemoteEndPoint, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem)
        :
        Server(Server),
        ServerSocket(ServerSocket),
        RemoteEndPoint(RemoteEndPoint),
        LastActiveTimeValue(std::chrono::steady_clock::now()),
        NextSecureContextValue(nullptr),
        SecureContextValue(nullptr),
        NumBadCommands(0),
        IsDisposed(false),
        RawReadingContext(nullptr),
        CookedWritingContext(nullptr),
        IsRunningValue(false),
        IsExitingValue(false)
    {
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
            return c;
        });

        ssm = std::make_shared<SessionStateMachine<std::shared_ptr<StreamedVirtualTransportServerHandleResult>, Unit>>(
            [](const std::exception &ex) { return dynamic_cast<const asio::system_error *>(&ex) != nullptr; },
            [this](const std::exception &ex) { return OnCriticalError(ex); },
            [this]() { return OnShutdownRead(); },
            [this]() { return OnShutdownWrite(); },
            [this](Unit w, std::function<void()> OnSuccess, std::function<void()> OnFailure) { return OnWrite(w, OnSuccess, OnFailure); },
            [this](std::shared_ptr<StreamedVirtualTransportServerHandleResult> r, std::function<void()> OnSuccess, std::function<void()> OnFailure) { return OnExecute(r, OnSuccess, OnFailure); },
            [this](std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure) { return OnStartRawRead(OnSuccess, OnFailure); },
            [this]() { return OnExit(); },
            QueueUserWorkItem
        );

        Context = Server.ServerContext()->CreateSessionContext();
        Context->Quit = [this]() { ssm->NotifyExit(); };
        Context->Authenticated = [this]()
        {
            this->Server.NotifySessionAuthenticated(this->shared_from_this());
        };

        auto rpst = std::make_shared<Rc4PacketServerTransformer>();
        auto Pair = VirtualTransportServerFactory(Context, rpst);
        si = std::get<0>(Pair);
        vts = std::get<1>(Pair);
        Context->SecureConnectionRequired = [=](std::shared_ptr<class SecureContext> c)
        {
            rpst->SetSecureContext(c);
            NextSecureContext(c);
        };
        vts->ServerEvent = [this]() { ssm->NotifyWrite(Unit()); };
        vts->InputByteLengthReport = [this](std::wstring CommandName, std::size_t ByteLength)
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->Token = Context->SessionTokenString();
            e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
            e->Time = UtcNow();
            e->Type = L"InBytes";
            e->Name = CommandName;
            e->Message = ToString(ByteLength);
            this->Server.ServerContext()->RaiseSessionLog(e);
        };
        vts->OutputByteLengthReport = [this](std::wstring CommandName, std::size_t ByteLength)
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->Token = Context->SessionTokenString();
            e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
            e->Time = UtcNow();
            e->Type = L"OutBytes";
            e->Name = CommandName;
            e->Message = ToString(ByteLength);
            this->Server.ServerContext()->RaiseSessionLog(e);
        };
    }

    void UdpSession::OnShutdownRead()
    {
        std::function<void()> OnFailure = nullptr;
        RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
        {
            if ((c->OnSuccess != nullptr) && (c->OnFailure != nullptr))
            {
                OnFailure = c->OnFailure;
                c->OnSuccess = nullptr;
                c->OnFailure = nullptr;
            }
        });
        if (OnFailure != nullptr)
        {
            OnFailure();
        }
    }
    void UdpSession::OnShutdownWrite()
    {
    }

    void UdpSession::OnWrite(Unit w, std::function<void()> OnSuccess, std::function<void()> OnFailure)
    {
        auto ByteArrays = vts->TakeWriteBuffer();
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
        auto SessionId = this->SessionId();
        auto SecureContext = this->SecureContext();
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
            OnSuccess();
            return;
        }
        auto Success = true;
        std::vector<std::shared_ptr<std::vector<std::uint8_t>>> Parts;
        CookedWritingContext.DoAction([&](std::shared_ptr<UdpWriteContext> c)
        {
            auto Time = std::chrono::steady_clock::now();
            auto WritingOffset = 0;
            while ((Indices.size() > 0) || (WritingOffset < TotalLength))
            {
                auto Index = PartContext::GetSuccessor(c->WritenIndex);

                auto NumIndex = static_cast<int>(Indices.size());
                if (NumIndex > 0xFFFF)
                {
                    Success = false;
                    return;
                }

                auto IsACK = NumIndex > 0;

                auto Length = std::min(12 + (IsACK ? 2 + NumIndex * 2 : 0) + TotalLength - WritingOffset, MaxPacketLength());
                auto DataLength = Length - (12 + (IsACK ? 2 + NumIndex * 2 : 0));
                auto Buffer = std::make_shared<std::vector<std::uint8_t>>();
                Buffer->resize(Length, 0);
                (*Buffer)[0] = static_cast<std::uint8_t>(SessionId & 0xFF);
                (*Buffer)[1] = static_cast<std::uint8_t>((SessionId >> 8) & 0xFF);
                (*Buffer)[2] = static_cast<std::uint8_t>((SessionId >> 16) & 0xFF);
                (*Buffer)[3] = static_cast<std::uint8_t>((SessionId >> 24) & 0xFF);

                auto Flag = 0;
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

                auto IsEncrypted = (SecureContext != nullptr);
                if (IsEncrypted)
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
                    Success = false;
                    return;
                }
                Parts.push_back(Part->Data);

                c->WritenIndex = Index;
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
                Success = false;
                break;
            }
        }
        if (!Success)
        {
            OnFailure();
        }
        else
        {
            OnSuccess();
        }
    }
    void UdpSession::OnExecute(std::shared_ptr<StreamedVirtualTransportServerHandleResult> r, std::function<void()> OnSuccess, std::function<void()> OnFailure)
    {
        if (r->OnCommand())
        {
            auto CommandName = r->Command->CommandName;

            auto a = [=]()
            {
                auto CurrentTime = UtcNow();
                Context->RequestTime(CurrentTime);
                if (Server.ServerContext()->EnableLogPerformance())
                {
                    auto StartTime = std::chrono::high_resolution_clock::now();
                    auto OnSuccessInner = [=]()
                    {
                        auto EndTime = std::chrono::high_resolution_clock::now();
                        auto Microseconds = std::chrono::duration_cast<std::chrono::microseconds>(EndTime - StartTime).count();
                        auto e = std::make_shared<SessionLogEntry>();
                        e->Token = Context->SessionTokenString();
                        e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
                        e->Time = UtcNow();
                        e->Type = L"Time";
                        e->Name = CommandName;
                        e->Message = ToString(Microseconds) + L"μs";
                        this->Server.ServerContext()->RaiseSessionLog(e);
                        ssm->NotifyWrite(Unit());
                        OnSuccess();
                    };
                    auto OnFailureInner = [=](const std::exception &ex)
                    {
                        RaiseUnknownError(CommandName, ex);
                        OnSuccess();
                    };
                    r->Command->ExecuteCommand(OnSuccessInner, OnFailureInner);
                }
                else
                {
                    auto OnSuccessInner = [=]()
                    {
                        ssm->NotifyWrite(Unit());
                        OnSuccess();
                    };
                    auto OnFailureInner = [=](const std::exception &ex)
                    {
                        RaiseUnknownError(CommandName, ex);
                        OnSuccess();
                    };
                    r->Command->ExecuteCommand(OnSuccessInner, OnFailureInner);
                }
            };

            ssm->AddToActionQueue(a);
        }
        else if (r->OnBadCommand())
        {
            auto CommandName = r->BadCommand->CommandName;

            NumBadCommands += 1;

            // Maximum allowed bad commands exceeded.
            if (Server.MaxBadCommands() != 0 && NumBadCommands > Server.MaxBadCommands())
            {
                RaiseError(CommandName, L"Too many bad commands, closing transmission channel.");
                OnFailure();
            }
            else
            {
                RaiseError(CommandName, L"Not recognized.");
                OnSuccess();
            }
        }
        else if (r->OnBadCommandLine())
        {
            auto CommandLine = r->BadCommandLine->CommandLine;

            NumBadCommands += 1;

            // Maximum allowed bad commands exceeded.
            if (Server.MaxBadCommands() != 0 && NumBadCommands > Server.MaxBadCommands())
            {
                RaiseError(L"", L"\"" + CommandLine + L"\": Too many bad commands, closing transmission channel.");
                OnFailure();
            }
            else
            {
                RaiseError(L"", L"\"" + CommandLine + L"\":  recognized.");
                OnSuccess();
            }
        }
        else
        {
            throw std::logic_error("InvalidOperationException");
        }
    }
    void UdpSession::OnStartRawRead(std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure)
    {
        auto Pushed = true;
        auto Parts = std::make_shared<std::vector<std::shared_ptr<Part>>>();
        RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
        {
            if ((c->OnSuccess == nullptr) && (c->OnFailure == nullptr))
            {
                while (true)
                {
                    auto p = c->Parts->TryTakeFirstPart();
                    if (p == nullptr) { break; }
                    Parts->push_back(p);
                }
                if (Parts->size() == 0)
                {
                    c->OnSuccess = OnSuccess;
                    c->OnFailure = OnFailure;
                }
                Pushed = true;
            }
            else
            {
                Pushed = false;
            }
        });

        if (Parts->size() > 0)
        {
            HandleRawRead(Parts, OnSuccess, OnFailure);
        }
        if (!Pushed)
        {
            OnFailure();
        }
    }

    void UdpSession::HandleRawRead(std::shared_ptr<std::vector<std::shared_ptr<Part>>> Parts, std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure)
    {
        if (ssm->IsExited()) { return; }
        auto Results = std::make_shared<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>();
        for (auto p : *Parts)
        {
            auto Buffer = vts->GetReadBuffer();
            auto BufferLength = vts->GetReadBufferOffset() + vts->GetReadBufferLength();
            if (p->Data->size() > Buffer->size() - BufferLength)
            {
                OnFailure();
                return;
            }
            ArrayCopy(*p->Data, 0, *Buffer, BufferLength, p->Data->size());

            auto c = p->Data->size();
            while (true)
            {
                std::shared_ptr<StreamedVirtualTransportServerHandleResult> Result;
                try
                {
                    ExceptionStackTrace::Execute([&]() { Result = vts->Handle(c); });
                }
                catch (const std::exception &ex)
                {
                    if (dynamic_cast<const std::logic_error *>(&ex) != nullptr)
                    {
                        auto e = std::make_shared<SessionLogEntry>();
                        e->Token = Context->SessionTokenString();
                        e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
                        e->Time = UtcNow();
                        e->Type = L"Known";
                        e->Name = L"Exception";
                        e->Message = s2w(std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace());
                        Server.ServerContext()->RaiseSessionLog(e);
                    }
                    else if (!IsSocketErrorKnown(ex))
                    {
                        auto Message = std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace();
                        OnCriticalError(std::runtime_error(Message));
                    }
                    OnFailure();
                    return;
                }
                c = 0;
                if (Result->OnContinue())
                {
                    break;
                }
                Results->push_back(Result);
            }
        }
        if (Results->size() == 0)
        {
            OnStartRawRead(OnSuccess, OnFailure);
            return;
        }
        OnSuccess(Results);
    }

    void UdpSession::Stop()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        IsExitingValue.Update([](bool b) { return true; });
        ssm->NotifyExit();

        Server.SessionMappings.DoAction([=](std::shared_ptr<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<UdpSession>>> Mappings)
        {
            if (Mappings->count(Context) > 0)
            {
                Mappings->erase(Context);
            }
        });
        Server.ServerContext()->TryUnregisterSession(Context);

        si->Stop();
        si = nullptr;
        vts = nullptr;

        IsRunningValue.Update([](bool b) { return false; });

        while (!ssm->IsExited())
        {
        }

        auto SessionTokenString = Context->SessionTokenString();
        Context = nullptr;

        IsExitingValue.Update([](bool b) { return false; });

        if (Server.ServerContext()->EnableLogSystem())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->Token = SessionTokenString;
            e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
            e->Time = UtcNow();
            e->Type = L"Sys";
            e->Name = L"SessionExit";
            e->Message = L"";
            Server.ServerContext()->RaiseSessionLog(e);
        }
    }

    UdpSession::~UdpSession()
    {
        Stop();
    }

    void UdpSession::OnExit()
    {
        IsExitingValue.Update([=](bool b)
        {
            if (!IsRunningValue.Check<bool>([=](bool bb) { return bb; })) { return b; }
            if (!b)
            {
                Server.NotifySessionQuit(this->shared_from_this());
            }
            return true;
        });
    }

    void UdpSession::Start()
    {
        IsRunningValue.Update([=](bool b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }
            return true;
        });

        try
        {
            ExceptionStackTrace::Execute([=]
            {
                Context->RemoteEndPoint(s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port()));

                Server.ServerContext()->RegisterSession(Context);
                Server.SessionMappings.DoAction([=](std::shared_ptr<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<UdpSession>>> Mappings) { (*Mappings)[Context] = this->shared_from_this(); });

                if (Server.ServerContext()->EnableLogSystem())
                {
                    auto e = std::make_shared<SessionLogEntry>();
                    e->Token = Context->SessionTokenString();
                    e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
                    e->Time = UtcNow();
                    e->Type = L"Sys";
                    e->Name = L"SessionEnter";
                    e->Message = L"";
                    Server.ServerContext()->RaiseSessionLog(e);
                }
                ssm->Start();
            });
        }
        catch (const std::exception &ex)
        {
            auto Message = std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace();
            OnCriticalError(std::runtime_error(Message));
            ssm->NotifyFailure();
        }
    }

    void UdpSession::SendPacket(asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<std::vector<std::uint8_t>> Data)
    {
        ServerSocket->send_to(asio::buffer(*Data), RemoteEndPoint);
    }

    bool UdpSession::PushAux(asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<std::vector<int>> Indices)
    {
        auto SessionId = this->SessionId();
        auto Time = std::chrono::steady_clock::now();
        if ((Indices != nullptr) && (Indices->size() > 0))
        {
            auto l = std::make_shared<std::vector<std::shared_ptr<std::vector<std::uint8_t>>>>();
            CookedWritingContext.DoAction([=](std::shared_ptr<UdpWriteContext> c)
            {
                auto IndicesWithoutFirst = *Indices;
                IndicesWithoutFirst.erase(IndicesWithoutFirst.begin());
                c->Parts->Acknowledge((*Indices)[0], IndicesWithoutFirst, c->WritenIndex);
                c->Parts->ForEachTimedoutPacket(SessionId, Time, [=](int i, std::shared_ptr<std::vector<std::uint8_t>> d) { l->push_back(d); });
            });
            for (auto p : *l)
            {
                try
                {
                    SendPacket(RemoteEndPoint, p);
                }
                catch (...)
                {
                    return false;
                }
            }
        }
        return true;
    }

    bool UdpSession::IsPushed(int Index)
    {
        auto Pushed = false;
        RawReadingContext.DoAction([&](std::shared_ptr<UdpReadContext> c)
        {
            if (c->Parts->HasPart(Index))
            {
                Pushed = true;
                return;
            }
        });
        return Pushed;
    }
    void UdpSession::PrePush(std::function<void()> a)
    {
        ssm->AddToActionQueue(a);
    }
    bool UdpSession::Push(asio::ip::udp::endpoint RemoteEndPoint, int Index, std::shared_ptr<std::vector<int>> Indices, std::shared_ptr<std::vector<std::uint8_t>> Buffer, int Offset, int Length)
    {
        auto SessionId = this->SessionId();
        auto Time = std::chrono::steady_clock::now();
        if ((Indices != nullptr) && (Indices->size() > 0))
        {
            auto l = std::make_shared<std::vector<std::shared_ptr<std::vector<std::uint8_t>>>>();
            CookedWritingContext.DoAction([=](std::shared_ptr<UdpWriteContext> c)
            {
                auto IndicesWithoutFirst = *Indices;
                IndicesWithoutFirst.erase(IndicesWithoutFirst.begin());
                c->Parts->Acknowledge((*Indices)[0], IndicesWithoutFirst, c->WritenIndex);
                c->Parts->ForEachTimedoutPacket(SessionId, Time, [=](int i, std::shared_ptr<std::vector<std::uint8_t>> d) { l->push_back(d); });
            });
            for (auto p : *l)
            {
                try
                {
                    SendPacket(RemoteEndPoint, p);
                }
                catch (...)
                {
                    return false;
                }
            }
        }

        auto Pushed = false;
        auto Parts = std::make_shared<std::vector<std::shared_ptr<Part>>>();
        std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess = nullptr;
        std::function<void()> OnFailure = nullptr;
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

                if ((c->OnSuccess != nullptr) && (c->OnFailure != nullptr))
                {
                    while (true)
                    {
                        auto p = c->Parts->TryTakeFirstPart();
                        if (p == nullptr) { break; }
                        Parts->push_back(p);
                    }

                    if (Parts->size() > 0)
                    {
                        OnSuccess = c->OnSuccess;
                        OnFailure = c->OnFailure;
                        c->OnSuccess = nullptr;
                        c->OnFailure = nullptr;
                    }
                }
            }
        });

        if (Pushed)
        {
            LastActiveTimeValue.Update([](std::chrono::steady_clock::time_point v) { return std::chrono::steady_clock::now(); });
        }
        if (Parts->size() > 0)
        {
            HandleRawRead(Parts, OnSuccess, OnFailure);
        }
        return Pushed;
    }

    bool UdpSession::IsSocketErrorKnown(const std::exception &ex)
    {
        auto se = dynamic_cast<const asio::system_error *>(&ex);
        if (se == nullptr) { return false; }
        const asio::error_code &ec = se->code();
        if (ec == asio::error::connection_aborted) { return true; }
        if (ec == asio::error::eof) { return true; }
        if (ec == asio::error::operation_aborted) { return true; }
        return false;
    }

    int UdpSession::GetMinNotLessPowerOfTwo(int v)
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

    void UdpSession::ArrayCopy(const std::vector<std::uint8_t> &Source, int SourceIndex, std::vector<std::uint8_t> &Destination, int DestinationIndex, int Length)
    {
        if (Length < 0) { throw std::logic_error("InvalidArgument"); }
        if (SourceIndex < 0) { throw std::logic_error("InvalidArgument"); }
        if (DestinationIndex < 0) { throw std::logic_error("InvalidArgument"); }
        if (SourceIndex + Length > static_cast<int>(Source.size())) { throw std::logic_error("InvalidArgument"); }
        if (DestinationIndex + Length > static_cast<int>(Destination.size())) { throw std::logic_error("InvalidArgument"); }
        if (Length == 0) { return; }
        std::memcpy(&Destination[DestinationIndex], &Source[SourceIndex], Length);
    }

    void UdpSession::RaiseError(std::wstring CommandName, std::wstring Message)
    {
        si->RaiseError(CommandName, Message);
    }
    void UdpSession::RaiseUnknownError(std::wstring CommandName, const std::exception &ex)
    {
        auto Info = s2w(ex.what());
        if (Server.ServerContext()->ClientDebug())
        {
            si->RaiseError(CommandName, Info);
        }
        else
        {
            si->RaiseError(CommandName, L"Internal server error.");
        }
        if (Server.ServerContext()->EnableLogUnknownError())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->Token = Context->SessionTokenString();
            e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
            e->Time = UtcNow();
            e->Type = L"Unk";
            e->Name = L"Exception";
            e->Message = Info;
            Server.ServerContext()->RaiseSessionLog(e);
        }
    }

    void UdpSession::OnCriticalError(const std::exception &ex)
    {
        if (Server.ServerContext()->EnableLogCriticalError())
        {
            auto Info = s2w(ex.what());
            auto e = std::make_shared<SessionLogEntry>();
            e->Token = Context->SessionTokenString();
            e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
            e->Time = UtcNow();
            e->Type = L"Crtcl";
            e->Name = L"Exception";
            e->Message = Info;
            Server.ServerContext()->RaiseSessionLog(e);
        }
    }
}
