#pragma once

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/Optional.h"
#include "Communication.h"
#include "UtfEncoding.h"
#include "CommunicationBinary.h"
#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/AutoResetEvent.h"
#include "Util/SessionLogEntry.h"
#include "Net/StreamedAsyncSocket.h"
#include "Context/ServerContext.h"
#include "Context/SessionContext.h"
#include "Services/ServerImplementation.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <queue>
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
    class BinarySocketSession : public std::enable_shared_from_this<BinarySocketSession>
    {
    protected:
        boost::asio::io_service &IoService;
    private:
        std::shared_ptr<Net::StreamedAsyncSocket> Socket;
        bool IsDisposed;
    public:
        boost::asio::ip::tcp::endpoint RemoteEndPoint;
        std::shared_ptr<BaseSystem::Optional<int>> IdleTimeout;

        BinarySocketSession(boost::asio::io_service &IoService, std::shared_ptr<BinarySocketServer> Server, std::shared_ptr<boost::asio::ip::tcp::socket> s);

        virtual ~BinarySocketSession();

        void Start();

        void Stop();

    private:
        std::shared_ptr<BinarySocketServer> Server; //循环引用
        std::shared_ptr<SessionContext> Context;
        std::shared_ptr<ServerImplementation> si;
        std::shared_ptr<Communication::Binary::BinarySerializationServerEventDispatcher> bssed;
        int NumBadCommands;

    private:
        class CommandBody;
        class SessionCommand;
        class TryShiftResult;
        class BufferStateMachine;

        std::shared_ptr<BaseSystem::AutoResetEvent> NumSessionCommandUpdated;
        std::shared_ptr<BaseSystem::LockedVariable<int>> NumSessionCommand;
        BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<SessionCommand>>>> CommandQueue;
        BaseSystem::LockedVariable<bool> IsRunningValue;
        BaseSystem::LockedVariable<bool> IsExitingValue;

        void LockSessionCommand();
        int ReleaseSessionCommand();

        void DoCommandAsync();

        void PushCommand(std::shared_ptr<SessionCommand> sc);
        void QueueCommand(std::shared_ptr<SessionCommand> sc);
        static bool IsSocketErrorKnown(const boost::system::error_code &se);
        
        std::shared_ptr<BufferStateMachine> bsm;
        std::shared_ptr<std::vector<uint8_t>> Buffer;
        int BufferLength;
        void MessageLoop(std::shared_ptr<SessionCommand> sc);

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
    };
}
