#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include "Utility.h"
#include "BaseSystem/Times.h"
#include "BaseSystem/ThreadLocalRandom.h"
#include "BaseSystem/ThreadLocalVariable.h"

#include <stdexcept>

namespace Server
{
    class BinarySocketSession::CommandBody
    {
    public:
        std::wstring CommandName;
        uint32_t CommandHash;
        std::shared_ptr<std::vector<uint8_t>> Parameters;
    };

    enum SessionCommandTag
    {
        SessionCommandTag_Read = 0,
        SessionCommandTag_Write = 1,
        SessionCommandTag_ReadRaw = 2
    };
    class BinarySocketSession::SessionCommand
    {
    public:
        SessionCommandTag _Tag;
        std::shared_ptr<CommandBody> Read;
        std::shared_ptr<CommandBody> Write;
        Unit ReadRaw;

        static std::shared_ptr<SessionCommand> CreateRead(std::shared_ptr<CommandBody> Value)
        {
            auto r = std::make_shared<SessionCommand>();
            r->_Tag = SessionCommandTag_Read;
            r->Read = Value;
            return r;
        }
        static std::shared_ptr<SessionCommand> CreateWrite(std::shared_ptr<CommandBody> Value)
        {
            auto r = std::make_shared<SessionCommand>();
            r->_Tag = SessionCommandTag_Write;
            r->Write = Value;
            return r;
        }
        static std::shared_ptr<SessionCommand> CreateReadRaw()
        {
            auto r = std::make_shared<SessionCommand>();
            r->_Tag = SessionCommandTag_ReadRaw;
            r->ReadRaw = Unit();
            return r;
        }

        Boolean OnRead() { return _Tag == SessionCommandTag_Read; }
        Boolean OnWrite() { return _Tag == SessionCommandTag_Write; }
        Boolean OnReadRaw() { return _Tag == SessionCommandTag_ReadRaw; }
    };

    static std::shared_ptr<BaseSystem::ThreadLocalVariable<Communication::Binary::BinarySerializationServer>> bsss(std::make_shared<BaseSystem::ThreadLocalVariable<Communication::Binary::BinarySerializationServer>>([]() { return std::make_shared<Communication::Binary::BinarySerializationServer>(); }));

    static BaseSystem::ThreadLocalRandom RNG;

    BinarySocketSession::BinarySocketSession(asio::io_service &IoService, std::shared_ptr<BinarySocketServer> Server, std::shared_ptr<asio::ip::tcp::socket> s)
        :
        IoService(IoService),
        Socket(std::make_shared<Net::StreamedAsyncSocket>(s)),
        IsDisposed(false),
        IdleTimeout(Optional<int>::CreateNotHasValue()),
        Server(Server),
        si(nullptr),
        bssed(nullptr),
        NumBadCommands(0),
        Context(std::make_shared<SessionContext>()),
        NumSessionCommandUpdated(std::make_shared<BaseSystem::AutoResetEvent>()),
        NumSessionCommand(std::make_shared<BaseSystem::LockedVariable<int>>(0)),
        IsRunningValue(false),
        IsExitingValue(false),
        bsm(std::make_shared<BufferStateMachine>()),
        Buffer(std::make_shared<std::vector<uint8_t>>()),
        BufferLength(0),
        CommandQueue(std::make_shared<std::queue<std::shared_ptr<SessionCommand>>>())
    {
        Context->SessionToken->push_back(RNG.NextInt<std::uint8_t>(0, 255));
        Context->SessionToken->push_back(RNG.NextInt<std::uint8_t>(0, 255));
        Context->SessionToken->push_back(RNG.NextInt<std::uint8_t>(0, 255));
        Context->SessionToken->push_back(RNG.NextInt<std::uint8_t>(0, 255));
        Context->Quit = [=]() { StopAsync(); };
        Buffer->resize(8 * 1024, 0);

        si = std::make_shared<ServerImplementation>(Server->sc, Context);
        si->RegisterCrossSessionEvents();
        bssed = std::make_shared<Communication::Binary::BinarySerializationServerEventDispatcher>(si);
        bssed->ServerEvent = [=](std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
        {
            WriteCommand(CommandName, CommandHash, Parameters);
        };
    }

    BinarySocketSession::~BinarySocketSession()
    {
        Stop();
    }

    void BinarySocketSession::Start()
    {
        IsRunningValue.Update([=](bool b) -> bool
        {
            if (b)
            {
                throw std::logic_error("InvalidOperationException");
            }
            return true;
        });

        try
        {
            Context->RemoteEndPoint = RemoteEndPoint;
            Server->SessionMappings.DoAction([=](std::shared_ptr<BinarySocketServer::TSessionMapping> &Mappings)
            {
                if (Mappings->count(Context) > 0)
                {
                    throw std::logic_error("InvalidOperationException");
                }
                Mappings->insert(std::pair<std::shared_ptr<SessionContext>, std::shared_ptr<BinarySocketSession>>(Context, this->shared_from_this()));
            });

            if (Server->GetEnableLogSystem())
            {
                auto e = std::make_shared<SessionLogEntry>();
                e->RemoteEndPoint = RemoteEndPoint;
                e->Token = Context->GetSessionTokenString();
                e->Time = UtcNow();
                e->Type = L"Sys";
                e->Name = L"SessionEnter";
                e->Message = L"";
                Server->RaiseSessionLog(e);
            }
            PushCommand(SessionCommand::CreateReadRaw());
        }
        catch (std::exception &ex)
        {
            OnCriticalError(ex);
            StopAsync();
        }
    }

    void BinarySocketSession::Stop()
    {
        if (IsDisposed) { return; }
        IsDisposed = true;

        IsExitingValue.Update([](bool b) { return true; });

        if (Server != nullptr)
        {
            if (Server->GetEnableLogSystem())
            {
                auto e = std::make_shared<SessionLogEntry>();
                e->RemoteEndPoint = RemoteEndPoint;
                e->Token = Context->GetSessionTokenString();
                e->Time = UtcNow();
                e->Type = L"Sys";
                e->Name = L"SessionExit";
                e->Message = L"";
                Server->RaiseSessionLog(e);
            }
            Server->SessionMappings.DoAction([&](const std::shared_ptr<BinarySocketServer::TSessionMapping> &Mappings)
            {
                if (Mappings->count(Context) > 0)
                {
                    Mappings->erase(Context);
                }
            });
        }

        si->UnregisterCrossSessionEvents();

        IsRunningValue.Update([=](bool b) { return false; });
        while (NumSessionCommand->Check<bool>([](const int &n) { return n != 0; }))
        {
            NumSessionCommandUpdated->WaitOne();
        }

        if (Socket != nullptr)
        {
            Socket->ShutdownBoth();
            if (Socket.use_count() != 1)
            {
                throw std::logic_error("InvalidOperationException");
            }
            Socket = nullptr;
        }

        if (Buffer != nullptr)
        {
            if (Buffer.use_count() != 1)
            {
                throw std::logic_error("InvalidOperationException");
            }
            Buffer = nullptr;
        }

        if (Server != nullptr)
        {
            Server = nullptr;
        }

        if (si != nullptr)
        {
            if (si.use_count() != 1)
            {
                throw std::logic_error("InvalidOperationException");
            }
            si = nullptr;
        }
        if (bssed != nullptr)
        {
            if (bssed.use_count() != 1)
            {
                throw std::logic_error("InvalidOperationException");
            }
            bssed = nullptr;
        }

        IsExitingValue.Update([=](bool b) { return false; });
    }

    class BinarySocketSession::TryShiftResult
    {
    public:
        std::shared_ptr<CommandBody> Command;
        int Position;
    };

    class BinarySocketSession::BufferStateMachine
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

    void BinarySocketSession::MessageLoop(std::shared_ptr<SessionCommand> sc)
    {
        if (sc->OnRead())
        {
            ReadCommand(sc->Read);
        }
        else if (sc->OnWrite())
        {
            auto cmd = sc->Write;
            Communication::Binary::ByteArrayStream s;
            s.WriteString(cmd->CommandName);
            s.WriteUInt32(cmd->CommandHash);
            s.WriteInt32((int32_t)(cmd->Parameters->size()));
            s.WriteBytes(cmd->Parameters);
            s.SetPosition(0);
            auto Bytes = s.ReadBytes(s.GetLength());
            if (IdleTimeout.OnHasValue())
            {
                auto Timer = std::make_shared<asio::deadline_timer>(IoService);
                Timer->expires_from_now(boost::posix_time::milliseconds(IdleTimeout.HasValue));
                Timer->async_wait([=](const asio::error_code& error)
                {
                    if (!error)
                    {
                        if (Server != nullptr)
                        {
                            Server->NotifySessionQuit(this->shared_from_this());
                        }
                    }
                });
                Socket->SendAsync(Bytes, 0, Bytes->size(), [=]() { Timer->cancel(); }, [=](const asio::error_code &se)
                {
                    Timer->cancel();
                    if (!IsSocketErrorKnown(se))
                    {
                        OnCriticalError(std::logic_error(se.message()));
                    }
                    StopAsync();
                });
            }
            else
            {
                Socket->SendAsync(Bytes, 0, Bytes->size(), [=]() { }, [=](const asio::error_code &se)
                {
                    if (!IsSocketErrorKnown(se))
                    {
                        OnCriticalError(std::logic_error(se.message()));
                    }
                    StopAsync();
                });
            }
        }
        else if (sc->OnReadRaw())
        {
            auto Completed = [=](int Count)
            {
                try
                {
                    CompletedInner(Count);
                }
                catch (std::exception &ex)
                {
                    OnCriticalError(ex);
                    StopAsync();
                }
            };
            auto Faulted = [=](const asio::error_code &se)
            {
                if (!IsSocketErrorKnown(se))
                {
                    OnCriticalError(std::logic_error(se.message()));
                }
                StopAsync();
            };
            if (IdleTimeout.OnHasValue())
            {
                auto Timer = std::make_shared<asio::deadline_timer>(IoService);
                Timer->expires_from_now(boost::posix_time::milliseconds(IdleTimeout.HasValue));
                Timer->async_wait([=](const asio::error_code& error)
                {
                    if (!error)
                    {
                        if (Server != nullptr)
                        {
                            Server->NotifySessionQuit(this->shared_from_this());
                        }
                    }
                });
                auto Completed = [=](int Count)
                {
                    Timer->cancel();
                    try
                    {
                        CompletedInner(Count);
                    }
                    catch (std::exception &ex)
                    {
                        OnCriticalError(ex);
                        StopAsync();
                    }
                };
                auto Faulted = [=](const asio::error_code &se)
                {
                    Timer->cancel();
                    if (!IsSocketErrorKnown(se))
                    {
                        OnCriticalError(std::logic_error(se.message()));
                    }
                    StopAsync();
                };
            }
            if (IsExitingValue.Check<bool>([](const bool &s) { return s; })) { return; }
            Socket->ReceiveAsync(Buffer, BufferLength, Buffer->size() - BufferLength, Completed, Faulted);
        }
        else
        {
            throw std::logic_error("InvalidOperationException");
        }
    }

    void BinarySocketSession::LockSessionCommand()
    {
        NumSessionCommand->Update([](const int &n) { return n + 1; });
        NumSessionCommandUpdated->Set();
    }
    int BinarySocketSession::ReleaseSessionCommand()
    {
        int nValue = 0;
        NumSessionCommand->Update([&](const int &n) -> int
        {
            nValue = n - 1;
            NumSessionCommandUpdated->Set();
            return n - 1;
        });
        return nValue;
    }

    void BinarySocketSession::DoCommandAsync()
    {
        auto a = [=]()
        {
            std::shared_ptr<SessionCommand> sc = nullptr;
            CommandQueue.DoAction([&](std::shared_ptr<std::queue<std::shared_ptr<SessionCommand>>> &q)
            {
                if (q->size() > 0)
                {
                    sc = q->front();
                }
            });
            if (sc == nullptr)
            {
                throw std::logic_error("InvalidOperation");
            }

            BaseSystem::AutoRelease Final([=]() { this->ReleaseSessionCommand(); });
            try
            {
                MessageLoop(sc);
            }
            catch (std::exception &ex)
            {
                OnCriticalError(ex);
                StopAsync();
            }
            CommandQueue.DoAction([&](std::shared_ptr<std::queue<std::shared_ptr<SessionCommand>>> &q)
            {
                q->pop();
                if (q->size() > 0)
                {
                    DoCommandAsync();
                }
            });
        };
        IoService.post(a);
    }

    void BinarySocketSession::PushCommand(std::shared_ptr<SessionCommand> sc)
    {
        CommandQueue.DoAction([&](std::shared_ptr<std::queue<std::shared_ptr<SessionCommand>>> &q)
        {
            LockSessionCommand();
            q->push(sc);
            if (q->size() == 1)
            {
                DoCommandAsync();
            }
        });
    }
    void BinarySocketSession::QueueCommand(std::shared_ptr<SessionCommand> sc)
    {
        PushCommand(sc);
    }
    bool BinarySocketSession::IsSocketErrorKnown(const asio::error_code &se)
    {
        if (se == asio::error::connection_aborted) { return true; }
        if (se == asio::error::connection_reset) { return true; }
        if (se == asio::error::eof) { return true; }
        if (se == asio::error::operation_aborted) { return true; }
        return false;
    }

    void BinarySocketSession::CompletedInner(int Count)
    {
        if (Count == 0)
        {
            StopAsync();
            return;
        }
        auto FirstPosition = 0;
        BufferLength += Count;
        while (true)
        {
            auto r = bsm->TryShift(*Buffer, FirstPosition, BufferLength - FirstPosition);
            if (r == nullptr)
            {
                break;
            }
            FirstPosition = r->Position;

            if (r->Command != nullptr)
            {
                PushCommand(SessionCommand::CreateRead(r->Command));
            }
        }
        if (FirstPosition > 0)
        {
            auto CopyLength = BufferLength - FirstPosition;
            for (int i = 0; i < CopyLength; i += 1)
            {
                (*Buffer)[i] = (*Buffer)[FirstPosition + i];
            }
            BufferLength = CopyLength;
        }
        QueueCommand(SessionCommand::CreateReadRaw());
    }

    void BinarySocketSession::ReadCommand(std::shared_ptr<CommandBody> cmd)
    {
        if (Server->GetEnableLogNormalIn())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->RemoteEndPoint = RemoteEndPoint;
            e->Token = Context->GetSessionTokenString();
            e->Time = UtcNow();
            e->Type = L"In";
            e->Name = cmd->CommandName;
            e->Message = L"{...}";
            Server->RaiseSessionLog(e);
        }
        if (Server->GetMaxBadCommands() != 0 && (NumBadCommands > Server->GetMaxBadCommands()))
        {
            return;
        }

        auto CommandName = cmd->CommandName;
        auto CommandHash = cmd->CommandHash;
        auto Parameters = cmd->Parameters;

        auto sv = bsss->Value();
        if (sv->HasCommand(CommandName, CommandHash) && ((Server->GetCheckCommandAllowed() != nullptr) ? Server->GetCheckCommandAllowed()(Context, CommandName) : true))
        {
            auto a = [=]()
            {
                auto s = sv->ExecuteCommand(si, CommandName, CommandHash, Parameters);
                WriteCommand(CommandName, CommandHash, s);
            };

            try
            {
                a();
            }
            catch (std::exception &ex)
            {
                RaiseUnknownError(CommandName, ex);
            }
        }
        else
        {
            NumBadCommands += 1;

            // Maximum allowed bad commands exceeded.
            if (Server->GetMaxBadCommands() != 0 && (NumBadCommands > Server->GetMaxBadCommands()))
            {
                RaiseError(CommandName, L"Too many bad commands, closing transmission channel.");
                StopAsync();
            }
            else
            {
                RaiseError(CommandName, L"Not recognized.");
            }
        }
    }

    bool BinarySocketSession::IsRunning()
    {
        return IsRunningValue.Check<bool>([](const bool &s) { return s; });
    }

    //线程安全
    void BinarySocketSession::WriteCommand(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
    {
        if (Server->GetEnableLogNormalIn())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->RemoteEndPoint = RemoteEndPoint;
            e->Token = Context->GetSessionTokenString();
            e->Time = UtcNow();
            e->Type = L"Out";
            e->Name = CommandName;
            e->Message = L"{...}";
            Server->RaiseSessionLog(e);
        }
        auto cmd = std::make_shared<CommandBody>();
        cmd->CommandName = CommandName;
        cmd->CommandHash = CommandHash;
        cmd->Parameters = Parameters;
        PushCommand(SessionCommand::CreateWrite(cmd));
    }
    //线程安全
    void BinarySocketSession::RaiseError(std::wstring CommandName, std::wstring Message)
    {
        si->RaiseError(CommandName, Message);
    }
    //线程安全
    void BinarySocketSession::RaiseUnknownError(std::wstring CommandName, const std::exception &ex)
    {
        auto Info = s2w(ex.what());
        if (Server->GetClientDebug())
        {
            si->RaiseError(CommandName, Info);
        }
        else
        {
            si->RaiseError(CommandName, L"Internal server error.");
        }
        if (Server->GetEnableLogUnknownError())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->RemoteEndPoint = RemoteEndPoint;
            e->Token = Context->GetSessionTokenString();
            e->Time = UtcNow();
            e->Type = L"Unk";
            e->Name = L"Exception";
            e->Message = Info;
            Server->RaiseSessionLog(e);
        }
    }

    //线程安全
    void BinarySocketSession::OnCriticalError(const std::exception &ex)
    {
        if (Server->GetEnableLogCriticalError())
        {
            auto Info = s2w(ex.what());
            auto e = std::make_shared<SessionLogEntry>();
            e->RemoteEndPoint = RemoteEndPoint;
            e->Token = Context->GetSessionTokenString();
            e->Time = UtcNow();
            e->Type = L"Unk";
            e->Name = L"Exception";
            e->Message = Info;
            Server->RaiseSessionLog(e);
        }
    }

    void BinarySocketSession::Logon()
    {
    }

    void BinarySocketSession::StopAsync()
    {
        bool Done;
        IsExitingValue.Update([&](bool b) -> bool
        {
            Done = b;
            return true;
        });

        if (Done) { return; }

        Socket->ShutdownReceive();
        if (Server != nullptr)
        {
            Server->NotifySessionQuit(this->shared_from_this());
        }
    }
}
