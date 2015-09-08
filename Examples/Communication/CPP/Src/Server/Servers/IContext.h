#pragma once

#include "Util/SessionLogEntry.h"
#include "ISerializationServer.h"

#include <cstdint>
#include <utility>
#include <vector>
#include <string>
#include <functional>
#include <memory>
#include <chrono>

namespace Server
{
    class IServerImplementation
    {
    public:
        virtual ~IServerImplementation() {}

        virtual void Stop() = 0;
        virtual void RaiseError(std::wstring CommandName, std::wstring Message) = 0;
    };

    class ISessionContext;
    class IServerContext
    {
    public:
        virtual ~IServerContext() {}

        //跨线程共享只读访问
        virtual bool EnableLogNormalIn() = 0;
        virtual bool EnableLogNormalOut() = 0;
        virtual bool EnableLogUnknownError() = 0;
        virtual bool EnableLogCriticalError() = 0;
        virtual bool EnableLogPerformance() = 0;
        virtual bool EnableLogSystem() = 0;
        virtual bool ServerDebug() = 0;
        virtual bool ClientDebug() = 0;

        virtual void RaiseSessionLog(std::shared_ptr<SessionLogEntry> Entry) = 0;

        virtual void RegisterSession(std::shared_ptr<ISessionContext> SessionContext) = 0;
        virtual bool TryUnregisterSession(std::shared_ptr<ISessionContext> SessionContext) = 0;

        virtual std::shared_ptr<ISessionContext> CreateSessionContext() = 0;
        virtual std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IBinarySerializationServerAdapter>> CreateServerImplementationWithBinaryAdapter(std::shared_ptr<ISessionContext> SessionContext) = 0;
    };

    class SecureContext
    {
    public:
        std::vector<std::uint8_t> ServerToken; //服务器到客户端数据的Token
        std::vector<std::uint8_t> ClientToken; //客户端到服务器数据的Token
    };

    class IBinaryTransformer
    {
    public:
        virtual ~IBinaryTransformer() {}

        virtual void Transform(std::vector<std::uint8_t> &Buffer, int Start, int Count) = 0;
        virtual void Inverse(std::vector<std::uint8_t> &Buffer, int Start, int Count) = 0;
    };

    class ISessionContext
    {
    public:
        virtual ~ISessionContext() {}

        //跨线程共享只读访问

        std::function<void()> Quit; //跨线程事件(订阅者需要保证线程安全)
        std::function<void()> Authenticated; //跨线程事件(订阅者需要保证线程安全)
        std::function<void(std::shared_ptr<SecureContext>)> SecureConnectionRequired; //跨线程事件(订阅者需要保证线程安全)

        virtual std::wstring RemoteEndPoint() = 0;
        virtual void RemoteEndPoint(std::wstring value) = 0;
        /// <summary>长度为4</summary>
        virtual std::vector<std::uint8_t> SessionToken() = 0;
        virtual std::wstring SessionTokenString() = 0;
        virtual std::chrono::system_clock::time_point RequestTime() = 0;
        virtual void RequestTime(std::chrono::system_clock::time_point value) = 0;
    };
}
