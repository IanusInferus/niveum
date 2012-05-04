#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/AutoResetEvent.h"
#include "Net/TcpSession.h"
#include "Net/TcpServer.h"
#include "Util/SessionLogEntry.h"
#include "Context/ServerContext.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <unordered_map>
#include <string>
#include <random>
#include <exception>
#include <stdexcept>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/thread.hpp>
#include <boost/date_time/posix_time/posix_time.hpp>

namespace Server
{
    class BinarySocketServer;
    class BinarySocketSession : public Communication::Net::TcpSession, public std::enable_shared_from_this<BinarySocketSession>
    {
    private:
        int NumBadCommands;

    public:
        std::shared_ptr<SessionContext> Context;
        std::shared_ptr<BinarySocketServer> Server; //循环引用

        BinarySocketSession(boost::asio::io_service &IoService);

        void StartInner();

    private:
        class CommandBody;
        class SessionCommand;
        class TryShiftResult;
        class BufferStateMachine;

        std::shared_ptr<Communication::BaseSystem::AutoResetEvent> NumAsyncOperationUpdated;
        std::shared_ptr<Communication::BaseSystem::LockedVariable<int>> NumAsyncOperation;
        std::shared_ptr<Communication::BaseSystem::AutoResetEvent> NumSessionCommandUpdated;
        std::shared_ptr<Communication::BaseSystem::LockedVariable<int>> NumSessionCommand;
        Communication::BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<SessionCommand>>>> CommandQueue;
        Communication::BaseSystem::LockedVariable<bool> IsRunningValue;

        bool TryLockAsyncOperation();
        void LockAsyncOperation();
        int ReleaseAsyncOperation();
        bool TryLockSessionCommand();
        int ReleaseSessionCommand();

        void DoCommandAsync();

        void PushCommand(std::shared_ptr<SessionCommand> sc);
        void QueueCommand(std::shared_ptr<SessionCommand> sc);
        static bool IsSocketErrorKnown(const boost::system::error_code &se);
        
        std::shared_ptr<BufferStateMachine> bsm;
        std::shared_ptr<std::vector<uint8_t>> Buffer;
        int BufferLength;
        bool MessageLoop(std::shared_ptr<SessionCommand> sc);

        void CompletedInner(int Count);

        void ReadCommand(std::shared_ptr<CommandBody> cmd);

    public:
        bool IsRunning();

        //线程安全
        void WriteCommand(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters);
        //线程安全
        void RaiseError(std::wstring CommandName, std::wstring Message);
        //线程安全
        void RaiseUnknownError(std::wstring CommandName, const std::exception &ex);

    private:
        //线程安全
        void OnCriticalError(const std::exception &ex);

        void Logon();

        void StopAsync();

        void StopInner();
    };
}
