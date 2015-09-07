﻿#pragma once

#include "IContext.h"
#include "StreamedServer.h"
#include "SessionStateMachine.h"

#include <cstdint>
#include <vector>
#include <map>
#include <set>
#include <functional>
#include <chrono>
#include <utility>
#include <memory>
#include <exception>
#include <asio.hpp>
#include <asio/steady_timer.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Server
{
    class UdpServer;

    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    class UdpSession : public std::enable_shared_from_this<UdpSession>
    {
    public:
        UdpServer &Server;
    private:
        std::shared_ptr<asio::ip::udp::socket> ServerSocket;
    public:
        asio::ip::udp::endpoint RemoteEndPoint;

    private:
        BaseSystem::LockedVariable<std::chrono::steady_clock::time_point> LastActiveTimeValue;
    public:
        std::chrono::steady_clock::time_point LastActiveTime()
        {
            return LastActiveTimeValue.Check<std::chrono::steady_clock::time_point>([](std::chrono::steady_clock::time_point v) { return v; });
        }
    private:
        BaseSystem::LockedVariable<std::shared_ptr<class SecureContext>> NextSecureContextValue;
        BaseSystem::LockedVariable<std::shared_ptr<class SecureContext>> SecureContextValue;
    public:
        std::shared_ptr<class SecureContext> NextSecureContext()
        {
            return NextSecureContextValue.Check<std::shared_ptr<class SecureContext>>([](std::shared_ptr<class SecureContext> v) { return v; });
        }
        void NextSecureContext(std::shared_ptr<class SecureContext> value)
        {
            return NextSecureContextValue.Update([=](std::shared_ptr<class SecureContext> v) { return value; });
        }
        std::shared_ptr<class SecureContext> SecureContext()
        {
            return SecureContextValue.Check<std::shared_ptr<class SecureContext>>([](std::shared_ptr<class SecureContext> v) { return v; });
        }
        void SecureContext(std::shared_ptr<class SecureContext> value)
        {
            return SecureContextValue.Update([=](std::shared_ptr<class SecureContext> v) { return value; });
        }

    private:
        std::shared_ptr<ISessionContext> Context;
        std::shared_ptr<IServerImplementation> si;
        std::shared_ptr<IStreamedVirtualTransportServer> vts;
        int NumBadCommands = 0;
        bool IsDisposed;

    public:
        static int MaxPacketLength() { return 1400; }
        static int ReadingWindowSize() { return 1024; }
        static int WritingWindowSize() { return 32; }
        static int IndexSpace() { return 65536; }
    private:
        static int GetTimeoutMilliseconds(int ResentCount)
        {
            if (ResentCount == 0) { return 400; }
            if (ResentCount == 1) { return 800; }
            if (ResentCount == 2) { return 1600; }
            if (ResentCount == 3) { return 2000; }
            if (ResentCount == 4) { return 3000; }
            return 4000;
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
                p->ResendTime = std::chrono::steady_clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
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
                p->ResendTime = std::chrono::steady_clock::now() + std::chrono::milliseconds(GetTimeoutMilliseconds(0));
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
            std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess;
            std::function<void()> OnFailure;
        };
        class UdpWriteContext
        {
        public:
            std::shared_ptr<PartContext> Parts;
            int WritenIndex;
        };
        BaseSystem::LockedVariable<std::shared_ptr<UdpReadContext>> RawReadingContext;
        BaseSystem::LockedVariable<std::shared_ptr<UdpWriteContext>> CookedWritingContext;

        std::shared_ptr<SessionStateMachine<std::shared_ptr<StreamedVirtualTransportServerHandleResult>, Unit>> ssm;

    public:
        UdpSession(UdpServer &Server, std::shared_ptr<asio::ip::udp::socket> ServerSocket, asio::ip::udp::endpoint RemoteEndPoint, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem);

        int SessionId()
        {
            auto b = Context->SessionToken();
            auto v = static_cast<int>(b[0]) | (static_cast<int>(b[1]) << 8) | (static_cast<int>(b[2]) << 16) | (static_cast<int>(b[3]) << 24);
            return v;
        }

    private:
        void OnShutdownRead();
        void OnShutdownWrite();
        void OnWrite(Unit w, std::function<void()> OnSuccess, std::function<void()> OnFailure);
        void OnExecute(std::shared_ptr<StreamedVirtualTransportServerHandleResult> r, std::function<void()> OnSuccess, std::function<void()> OnFailure);
        void OnStartRawRead(std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure);
        void HandleRawRead(std::shared_ptr<std::vector<std::shared_ptr<Part>>> Parts, std::function<void(std::shared_ptr<std::vector<std::shared_ptr<StreamedVirtualTransportServerHandleResult>>>)> OnSuccess, std::function<void()> OnFailure);

    public:
        void Stop();
        ~UdpSession();

    private:
        void OnExit();

    public:
        void Start();

    private:
        void SendPacket(asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<std::vector<std::uint8_t>> Data);

    public:
        bool PushAux(asio::ip::udp::endpoint RemoteEndPoint, std::shared_ptr<std::vector<int>> Indices);

        bool IsPushed(int Index);
        void PrePush(std::function<void()> a);
        bool Push(asio::ip::udp::endpoint RemoteEndPoint, int Index, std::shared_ptr<std::vector<int>> Indices, std::shared_ptr<std::vector<std::uint8_t>> Buffer, int Offset, int Length);

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
        void RaiseError(std::wstring CommandName, std::wstring Message);
        //线程安全
        void RaiseUnknownError(std::wstring CommandName, const std::exception &ex);

    private:
        //线程安全
        void OnCriticalError(const std::exception &ex);
    };
}