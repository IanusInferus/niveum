#include "TcpSession.h"
#include "TcpServer.h"

#include "BaseSystem/StringUtilities.h"
#include "BaseSystem/Times.h"
#include "Rc4PacketServerTransformer.h"

#include <chrono>
#include <stdexcept>

namespace Server
{
    TcpSession::TcpSession(TcpServer &Server, std::shared_ptr<asio::ip::tcp::socket> Socket, asio::ip::tcp::endpoint RemoteEndPoint, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem)
        :
        Server(Server),
        Socket(Socket),
        RemoteEndPoint(RemoteEndPoint),
        LastActiveTimeValue(std::chrono::steady_clock::now()),
        NumBadCommands(0),
        IsDisposed(false),
        IsRunningValue(false),
        IsExitingValue(false)
    {
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
        Context->SecureConnectionRequired = [=](std::shared_ptr<SecureContext> c)
        {
            rpst->SetSecureContext(c);
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

    void TcpSession::OnShutdownRead()
    {
        asio::error_code ec;
        Socket->shutdown(asio::socket_base::shutdown_receive, ec);
    }
    void TcpSession::OnShutdownWrite()
    {
        asio::error_code ec;
        Socket->shutdown(asio::socket_base::shutdown_send, ec);
        Socket->close();
    }

    void TcpSession::OnWrite(Unit w, std::function<void()> OnSuccess, std::function<void()> OnFailure)
    {
        auto ByteArrays = vts->TakeWriteBuffer();
        if (ByteArrays.size() == 0)
        {
            OnSuccess();
            return;
        }
        int TotalLength = 0;
        for (auto b : ByteArrays)
        {
            TotalLength += static_cast<int>(b->size());
        }
        if ((WriteBuffer == nullptr) || (TotalLength > static_cast<int>(WriteBuffer->size())))
        {
            WriteBuffer = std::make_shared<std::vector<std::uint8_t>>();
            WriteBuffer->resize(GetMinNotLessPowerOfTwo(TotalLength), 0);
        }
        auto Offset = 0;
        for (auto b : ByteArrays)
        {
            ArrayCopy(*b, 0, *WriteBuffer, Offset, b->size());
            Offset += b->size();
        }
        auto WriteHandler = [=](const asio::error_code &ec, size_t Count)
        {
            if (ec)
            {
                auto ex = asio::system_error(ec);
                if (!IsSocketErrorKnown(ex))
                {
                    OnCriticalError(ex);
                }
                OnFailure();
            }
            else
            {
                OnSuccess();
            }
        };
        asio::async_write(*Socket, asio::buffer(*WriteBuffer, TotalLength), WriteHandler);
    }
    void TcpSession::OnExecute(std::shared_ptr<StreamedVirtualTransportServerHandleResult> r, std::function<void()> OnSuccess, std::function<void()> OnFailure)
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
    void TcpSession::OnStartRawRead(std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure)
    {
        auto Completed = [=](int Count)
        {
            LastActiveTimeValue.Update([](std::chrono::steady_clock::time_point v) { return std::chrono::steady_clock::now(); });
            if (Count <= 0)
            {
                OnFailure();
                return;
            }
            if (ssm->IsExited()) { return; }
            auto Results = std::make_shared<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>();
            auto c = Count;
            while (true)
            {
                std::shared_ptr<StreamedVirtualTransportServerHandleResult> Result;
                try
                {
                    Result = vts->Handle(c);
                }
                catch (std::exception &ex)
                {
                    if (dynamic_cast<std::logic_error *>(&ex) != nullptr)
                    {
                        auto e = std::make_shared<SessionLogEntry>();
                        e->Token = Context->SessionTokenString();
                        e->RemoteEndPoint = s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port());
                        e->Time = UtcNow();
                        e->Type = L"Known";
                        e->Name = L"Exception";
                        e->Message = s2w(ex.what());
                        Server.ServerContext()->RaiseSessionLog(e);
                    }
                    else if (!IsSocketErrorKnown(ex))
                    {
                        OnCriticalError(ex);
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
            if (Results->size() == 0)
            {
                OnStartRawRead(OnSuccess, OnFailure);
                return;
            }
            OnSuccess(Results);
        };
        auto Faulted = [=](const std::exception &ex)
        {
            if (!IsSocketErrorKnown(ex))
            {
                OnCriticalError(ex);
            }
            OnFailure();
        };
        auto Buffer = vts->GetReadBuffer();
        auto BufferLength = vts->GetReadBufferOffset() + vts->GetReadBufferLength();
        auto ReadHandler = [=](const asio::error_code &ec, size_t Count)
        {
            if (ec)
            {
                Faulted(asio::system_error(ec));
            }
            else
            {
                Completed(Count);
            }
        };
        Socket->async_read_some(asio::buffer(Buffer->data() + BufferLength, Buffer->size() - BufferLength), ReadHandler);
    }

    void TcpSession::Stop()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        IsExitingValue.Update([](bool b) { return true; });
        ssm->NotifyExit();

        Server.SessionMappings.DoAction([=](std::shared_ptr<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<TcpSession>>> Mappings)
        {
            if (Mappings->count(Context) > 0)
            {
                Mappings->erase(Context);
            }
        });
        Server.ServerContext()->TryUnregisterSession(Context);

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

    TcpSession::~TcpSession()
    {
        Stop();
    }

    void TcpSession::OnExit()
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

    void TcpSession::Start()
    {
        IsRunningValue.Update([=](bool b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }
            return true;
        });

        try
        {
            Context->RemoteEndPoint(s2w(this->RemoteEndPoint.address().to_string()) + L":" + ToString(this->RemoteEndPoint.port()));

            Server.ServerContext()->RegisterSession(Context);
            Server.SessionMappings.DoAction([=](std::shared_ptr<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<TcpSession>>> Mappings) { (*Mappings)[Context] = this->shared_from_this(); });

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
        }
        catch (std::exception &ex)
        {
            OnCriticalError(ex);
            ssm->NotifyFailure();
        }
    }

    bool TcpSession::IsSocketErrorKnown(const std::exception &ex)
    {
        auto se = dynamic_cast<const asio::system_error *>(&ex);
        if (se == nullptr) { return false; }
        const asio::error_code &ec = se->code();
        if (ec == asio::error::connection_aborted) { return true; }
        if (ec == asio::error::eof) { return true; }
        if (ec == asio::error::operation_aborted) { return true; }
        return false;
    }

    int TcpSession::GetMinNotLessPowerOfTwo(int v)
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

    void TcpSession::ArrayCopy(const std::vector<std::uint8_t> &Source, int SourceIndex, std::vector<std::uint8_t> &Destination, int DestinationIndex, int Length)
    {
        if (Length < 0) { throw std::logic_error("InvalidArgument"); }
        if (SourceIndex < 0) { throw std::logic_error("InvalidArgument"); }
        if (DestinationIndex < 0) { throw std::logic_error("InvalidArgument"); }
        if (SourceIndex + Length > static_cast<int>(Source.size())) { throw std::logic_error("InvalidArgument"); }
        if (DestinationIndex + Length > static_cast<int>(Destination.size())) { throw std::logic_error("InvalidArgument"); }
        if (Length == 0) { return; }
        memcpy(&Destination[DestinationIndex], &Source[SourceIndex], Length);
    }

    void TcpSession::RaiseError(std::wstring CommandName, std::wstring Message)
    {
        si->RaiseError(CommandName, Message);
    }
    void TcpSession::RaiseUnknownError(std::wstring CommandName, const std::exception &ex)
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

    void TcpSession::OnCriticalError(const std::exception &ex)
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
