#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include "Utility.h"

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
        SessionCommandTag_ReadRaw = 2,
        SessionCommandTag_Quit = 3
    };
    class BinarySocketSession::SessionCommand
    {
    public:
        SessionCommandTag _Tag;
        std::shared_ptr<CommandBody> Read;
        std::shared_ptr<CommandBody> Write;
        Unit ReadRaw;
        Unit Quit;

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
        static std::shared_ptr<SessionCommand> CreateQuit()
        {
            auto r = std::make_shared<SessionCommand>();
            r->_Tag = SessionCommandTag_Quit;
            r->Quit = Unit();
            return r;
        }

        Boolean OnRead() { return _Tag == SessionCommandTag_Read; }
        Boolean OnWrite() { return _Tag == SessionCommandTag_Write; }
        Boolean OnReadRaw() { return _Tag == SessionCommandTag_ReadRaw; }
        Boolean OnQuit() { return _Tag == SessionCommandTag_Quit; }
    };

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

    BinarySocketSession::BinarySocketSession(boost::asio::io_service &IoService)
        : Communication::Net::TcpSession(IoService),
          NumBadCommands(0),
          Context(std::make_shared<SessionContext>()),
          NumAsyncOperationUpdated(std::make_shared<Communication::BaseSystem::AutoResetEvent>()),
          NumAsyncOperation(std::make_shared<Communication::BaseSystem::LockedVariable<int>>(0)),
          NumSessionCommandUpdated(std::make_shared<Communication::BaseSystem::AutoResetEvent>()),
          NumSessionCommand(std::make_shared<Communication::BaseSystem::LockedVariable<int>>(0)),
          IsRunningValue(false),
          bsm(std::make_shared<BufferStateMachine>()),
          Buffer(std::make_shared<std::vector<uint8_t>>()),
          BufferLength(0),
          CommandQueue(std::make_shared<std::queue<std::shared_ptr<SessionCommand>>>())
    {
        std::default_random_engine re;
        std::uniform_int_distribution<std::uint8_t> uid(0, 255);
        Context->SessionToken->push_back(uid(re));
        Context->SessionToken->push_back(uid(re));
        Context->SessionToken->push_back(uid(re));
        Context->SessionToken->push_back(uid(re));
        Context->Quit = [=]() { StopAsync(); };
        Buffer->resize(8 * 1024, 0);
    }

    void BinarySocketSession::StartInner()
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
                e->Time = boost::posix_time::second_clock::universal_time();
                e->Type = L"Sys";
                e->Message = L"SessionEnter";
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

    bool BinarySocketSession::MessageLoop(std::shared_ptr<SessionCommand> sc)
    {
        if (!IsRunningValue.Check<bool>([](const bool &b) { return b; }))
        {
            if (!sc->OnWrite() && !sc->OnQuit())
            {
                return true;
            }
        }

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
            LockAsyncOperation();
            SendAsync(Bytes, 0, Bytes->size(), [=]() { ReleaseAsyncOperation(); }, [=](const boost::system::error_code &se)
            {
                if (!IsSocketErrorKnown(se))
                {
                    OnCriticalError(std::logic_error(se.message()));
                }
                StopAsync();
                ReleaseAsyncOperation();
            });
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
                ReleaseAsyncOperation();
            };
            auto Faulted = [=](const boost::system::error_code &se)
            {
                if (!IsSocketErrorKnown(se))
                {
                    OnCriticalError(std::logic_error(se.message()));
                }
                StopAsync();
                ReleaseAsyncOperation();
            };
            if (!TryLockAsyncOperation()) { return true; }
            ReceiveAsync(Buffer, BufferLength, Buffer->size() - BufferLength, Completed, Faulted);
        }
        else if (sc->OnQuit())
        {
            return false;
        }
        else
        {
            throw std::logic_error("InvalidOperationException");
        }
        return true;
    }

    bool BinarySocketSession::TryLockAsyncOperation()
    {
        bool Done = IsRunningValue.Check<bool>([=](const bool &b) -> bool
        {
            if (b)
            {
                NumAsyncOperation->Update([](const int &n) { return n + 1; });
                NumAsyncOperationUpdated->Set();
                return true;
            }
            return false;
        });
        return Done;
    }
    void BinarySocketSession::LockAsyncOperation()
    {
        NumAsyncOperation->Update([](const int &n) { return n + 1; });
        NumAsyncOperationUpdated->Set();
    }
    int BinarySocketSession::ReleaseAsyncOperation()
    {
        int nValue = 0;
        NumAsyncOperation->Update([&](const int &n) -> int
        {
            nValue = n - 1;
            NumAsyncOperationUpdated->Set();
            return n - 1;
        });
        return nValue;
    }
    bool BinarySocketSession::TryLockSessionCommand()
    {
        bool Done = IsRunningValue.Check<bool>([=](const bool &b) -> bool
        {
            if (b)
            {
                NumSessionCommand->Update([](const int &n) { return n + 1; });
                NumSessionCommandUpdated->Set();
                return true;
            }
            return false;
        });
        return Done;
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
                    q->pop();
                }
            });
            if (sc == nullptr) { return; }
            try
            {
                MessageLoop(sc);
            }
            catch (std::exception &ex)
            {
                OnCriticalError(ex);
                StopAsync();
            }
            if (ReleaseSessionCommand() > 0)
            {
                DoCommandAsync();
            }
        };
        IoService.post(a);
    }

    void BinarySocketSession::PushCommand(std::shared_ptr<SessionCommand> sc)
    {
        if (!TryLockSessionCommand()) { return; }

        CommandQueue.DoAction([&](std::shared_ptr<std::queue<std::shared_ptr<SessionCommand>>> &q)
        {
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
    bool BinarySocketSession::IsSocketErrorKnown(const boost::system::error_code &se)
    {
        if (se == boost::system::errc::connection_aborted) { return true; }
        if (se == boost::system::errc::connection_reset) { return true; }
        if (se == boost::asio::error::eof) { return true; }
        if (se == boost::system::errc::operation_canceled) { return true; }
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
            e->Time = boost::posix_time::second_clock::universal_time();
            e->Type = L"In";
            e->Message = L"/" + cmd->CommandName + L" {...}";
            Server->RaiseSessionLog(e);
        }
        if (Server->GetMaxBadCommands() != 0 && (NumBadCommands > Server->GetMaxBadCommands()))
        {
            return;
        }

        auto CommandName = cmd->CommandName;
        auto CommandHash = cmd->CommandHash;
        auto Parameters = cmd->Parameters;

        auto sv = Server->InnerServer();
        if (sv->HasCommand(CommandName, CommandHash))
        {
            auto a = [=]()
            {
                auto s = sv->ExecuteCommand(*Context, CommandName, CommandHash, Parameters);
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
            e->Time = boost::posix_time::second_clock::universal_time();
            e->Type = L"Out";
            e->Message = L"/svr " + CommandName + L" {...}";
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
        Server->RaiseError(*Context, CommandName, Message);
    }
    //线程安全
    void BinarySocketSession::RaiseUnknownError(std::wstring CommandName, const std::exception &ex)
    {
        auto Info = s2w(ex.what());
        if (Server->GetClientDebug())
        {
            Server->RaiseError(*Context, CommandName, Info);
        }
        else
        {
            Server->RaiseError(*Context, CommandName, L"Internal server error.");
        }
        if (Server->GetEnableLogUnknownError())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->RemoteEndPoint = RemoteEndPoint;
            e->Token = Context->GetSessionTokenString();
            e->Time = boost::posix_time::second_clock::universal_time();
            e->Type = L"Unk";
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
            e->Time = boost::posix_time::second_clock::universal_time();
            e->Type = L"Unk";
            e->Message = Info;
            Server->RaiseSessionLog(e);
        }
    }

    void BinarySocketSession::Logon()
    {
    }

    void BinarySocketSession::StopAsync()
    {
        if (!IsRunning()) { return; }
        auto ss = GetSocket();
        if (ss != nullptr)
        {
            try
            {
                ss->shutdown(boost::asio::ip::tcp::socket::shutdown_receive);
            }
            catch (std::exception &)
            {
            }
        }
        if (Server->GetEnableLogSystem())
        {
            auto e = std::make_shared<SessionLogEntry>();
            e->RemoteEndPoint = RemoteEndPoint;
            e->Token = Context->GetSessionTokenString();
            e->Time = boost::posix_time::second_clock::universal_time();
            e->Type = L"Sys";
            e->Message = L"SessionExit";
            Server->RaiseSessionLog(e);
        }
        PushCommand(SessionCommand::CreateQuit());
        IsRunningValue.Update([=](bool b) { return false; });
        if (NotifySessionQuit != nullptr)
        {
            NotifySessionQuit();
        }
    }

    void BinarySocketSession::StopInner()
    {
        if (Server != nullptr)
        {
            Server->SessionMappings.DoAction([&](const std::shared_ptr<BinarySocketServer::TSessionMapping> &Mappings)
            {
                if (Mappings->count(Context) > 0)
                {
                    Mappings->erase(Context);
                }
            });
        }

        PushCommand(SessionCommand::CreateQuit());
        IsRunningValue.Update([=](bool b) { return false; });

        while (NumSessionCommand->Check<bool>([](const int &n) { return n != 0; }))
        {
            NumSessionCommandUpdated->WaitOne();
        }
        while (NumAsyncOperation->Check<bool>([](const int &n) { return n != 0; }))
        {
            NumAsyncOperationUpdated->WaitOne();
        }

        Buffer = nullptr;
        if (Server != nullptr)
        {
            Server = nullptr;
        }
    }
}
