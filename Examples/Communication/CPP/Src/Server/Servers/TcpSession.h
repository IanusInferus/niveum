#pragma once

#include "IContext.h"
#include "StreamedServer.h"
#include "SessionStateMachine.h"

#include <cstdint>
#include <vector>
#include <functional>
#include <chrono>
#include <utility>
#include <memory>
#include <exception>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Server
{
    class TcpServer;

    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    class TcpSession : public std::enable_shared_from_this<TcpSession>
    {
    public:
        TcpServer &Server;
    private:
        std::shared_ptr<asio::ip::tcp::socket> Socket;
    public:
        const asio::ip::tcp::endpoint RemoteEndPoint;

    private:
        BaseSystem::LockedVariable<std::chrono::steady_clock::time_point> LastActiveTimeValue;
    public:
        std::chrono::steady_clock::time_point LastActiveTime()
        {
            return LastActiveTimeValue.Check<std::chrono::steady_clock::time_point>([](std::chrono::steady_clock::time_point v) { return v; });
        }

    private:
        std::shared_ptr<ISessionContext> Context;
        std::shared_ptr<IServerImplementation> si;
        std::shared_ptr<IStreamedVirtualTransportServer> vts;
        int NumBadCommands = 0;
        bool IsDisposed;

        std::shared_ptr<std::vector<std::uint8_t>> WriteBuffer;
        std::shared_ptr<SessionStateMachine<std::shared_ptr<StreamedVirtualTransportServerHandleResult>, Unit>> ssm;

    public:
        TcpSession(TcpServer &Server, std::shared_ptr<asio::ip::tcp::socket> Socket, asio::ip::tcp::endpoint RemoteEndPoint, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem);

    private:
        void OnShutdownRead();
        void OnShutdownWrite();
        void OnWrite(Unit w, std::function<void()> OnSuccess, std::function<void()> OnFailure);
        void OnExecute(std::shared_ptr<StreamedVirtualTransportServerHandleResult> r, std::function<void()> OnSuccess, std::function<void()> OnFailure);
        void OnStartRawRead(std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure);

    public:
        void Stop();
        ~TcpSession();

    private:
        void OnExit();

    public:
        void Start();

    private:
        BaseSystem::LockedVariable<bool> IsRunningValue;
        BaseSystem::LockedVariable<bool> IsExitingValue;
    public:
        bool IsRunning()
        {
            return IsRunningValue.Check<bool>([](bool b) { return b; });
        }

    private:
        static bool IsSocketErrorKnown(const std::exception &ex);

        static int GetMinNotLessPowerOfTwo(int v);

        static void ArrayCopy(const std::vector<std::uint8_t> &Source, int SourceIndex, std::vector<std::uint8_t> &Destination, int DestinationIndex, int Length);
    public:
        //线程安全
        void RaiseError(std::u16string CommandName, std::u16string Message);
        //线程安全
        void RaiseUnknownError(std::u16string CommandName, const std::exception &ex);

    private:
        //线程安全
        void OnCriticalError(const std::exception &ex);
    };
}
