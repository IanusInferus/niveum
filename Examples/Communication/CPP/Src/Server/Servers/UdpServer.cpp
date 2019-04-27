#include "UdpServer.h"
#include "UdpSession.h"

#include "BaseSystem/AutoRelease.h"
#include "BaseSystem/StringUtilities.h"
#include "BaseSystem/Times.h"
#include "BaseSystem/ExceptionStackTrace.h"
#include "BaseSystem/Cryptography.h"

#include <cstring>
#include <limits>
#include <algorithm>
#include <chrono>
#include <exception>
#include <typeinfo>

namespace Server
{
    UdpServer::UdpServer(asio::io_service &IoService, std::shared_ptr<IServerContext> sc, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem, std::function<void(std::function<void()>)> PurifierQueueUserWorkItem)
        :
        IsRunningValue(false),
        IoService(IoService),
        SessionSets(std::make_shared<ServerSessionSets>()),
        VirtualTransportServerFactory(VirtualTransportServerFactory),
        QueueUserWorkItem(QueueUserWorkItem),
        PurifierQueueUserWorkItem(PurifierQueueUserWorkItem),
        MaxBadCommandsValue(8),
        SessionIdleTimeoutValue(Optional<int>::CreateNotHasValue()),
        UnauthenticatedSessionIdleTimeoutValue(Optional<int>::CreateNotHasValue()),
        MaxConnectionsValue(Optional<int>::CreateNotHasValue()),
        MaxConnectionsPerIPValue(Optional<int>::CreateNotHasValue()),
        MaxUnauthenticatedPerIPValue(Optional<int>::CreateNotHasValue()),
        TimeoutCheckPeriodValue(30),
        SessionMappings(std::make_shared<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<UdpSession>>>())
    {
        ServerContext(sc);
    }

    void UdpServer::OnMaxConnectionsExceeded(std::shared_ptr<UdpSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(u"", u"Client host rejected: too many connections, please try again later.");
        }
    }
    void UdpServer::OnMaxConnectionsPerIPExceeded(std::shared_ptr<UdpSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(u"", u"Client host rejected: too many connections from your IP(" + systemToUtf16(s->RemoteEndPoint.address().to_string()) + u"), please try again later.");
        }
    }

    void UdpServer::DoTimeoutCheck()
    {
        auto TimePeriod = std::chrono::seconds(std::max(TimeoutCheckPeriodValue, 1));
        auto WaitHandler = [this, TimePeriod](const asio::error_code& ec)
        {
            if (!ec)
            {
                if (UnauthenticatedSessionIdleTimeoutValue.HasValue)
                {
                    auto CheckTime = std::chrono::steady_clock::now() + std::chrono::seconds(-UnauthenticatedSessionIdleTimeoutValue.Value());
                    SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
                    {
                        for (auto s : ss->Sessions)
                        {
                            auto IpAddress = s->RemoteEndPoint.address();
                            auto isi = ss->IpSessions[IpAddress];
                            if (isi->Authenticated.count(s) == 0)
                            {
                                if (s->LastActiveTime() < CheckTime)
                                {
                                    PurifyConsumer->Push(s);
                                }
                            }
                        }
                    });
                }

                if (SessionIdleTimeoutValue.HasValue)
                {
                    auto CheckTime = std::chrono::steady_clock::now() + std::chrono::seconds(-SessionIdleTimeoutValue.Value());
                    SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
                    {
                        for (auto s : ss->Sessions)
                        {
                            auto IpAddress = s->RemoteEndPoint.address();
                            auto isi = ss->IpSessions[IpAddress];
                            if (isi->Authenticated.count(s) > 0)
                            {
                                if (s->LastActiveTime() < CheckTime)
                                {
                                    PurifyConsumer->Push(s);
                                }
                            }
                        }
                    });
                }

                DoTimeoutCheck();
            }
        };
        LastActiveTimeCheckTimer = std::make_shared<asio::steady_timer>(this->IoService);
        LastActiveTimeCheckTimer->expires_from_now(TimePeriod);
        LastActiveTimeCheckTimer->async_wait(WaitHandler);
    }

    static void ArrayCopy(const std::vector<std::uint8_t> &Source, int SourceIndex, std::vector<std::uint8_t> &Destination, int DestinationIndex, int Length)
    {
        if (Length < 0) { throw std::logic_error("InvalidArgument"); }
        if (SourceIndex < 0) { throw std::logic_error("InvalidArgument"); }
        if (DestinationIndex < 0) { throw std::logic_error("InvalidArgument"); }
        if (SourceIndex + Length > static_cast<int>(Source.size())) { throw std::logic_error("InvalidArgument"); }
        if (DestinationIndex + Length > static_cast<int>(Destination.size())) { throw std::logic_error("InvalidArgument"); }
        if (Length == 0) { return; }
        std::memcpy(&Destination[DestinationIndex], &Source[SourceIndex], Length);
    }

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

    void UdpServer::Start()
    {
        auto Success = false;
        BaseSystem::AutoRelease ar([&]()
        {
            if (!Success)
            {
                Stop();
            }
        });

        IsRunningValue.Update([&](bool b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }

            if (BindingsValue->size() == 0)
            {
                throw std::logic_error("NoValidBinding");
            }

            ListeningTaskToken = std::make_shared<BaseSystem::CancellationToken>();

            auto Purify = [=](std::shared_ptr<UdpSession> StoppingSession)
            {
                SessionSets.DoAction([=](std::shared_ptr<ServerSessionSets> ss)
                {
                    if (ss->Sessions.count(StoppingSession) > 0)
                    {
                        ss->Sessions.erase(StoppingSession);
                        auto IpAddress = StoppingSession->RemoteEndPoint.address();
                        auto isi = ss->IpSessions[IpAddress];
                        if (isi->Authenticated.count(StoppingSession) > 0)
                        {
                            isi->Authenticated.erase(StoppingSession);
                        }
                        isi->Count -= 1;
                        if (isi->Count == 0)
                        {
                            ss->IpSessions.erase(IpAddress);
                        }
                        auto SessionId = StoppingSession->SessionId();
                        ss->SessionIdToSession.erase(SessionId);
                    }
                });
                StoppingSession->Stop();
            };

            auto Accept = [=](std::shared_ptr<AcceptingInfo> a)
            {
                auto ep = a->RemoteEndPoint;
                std::shared_ptr<UdpSession> s = nullptr;

                try
                {
                    ExceptionStackTrace::Execute([&]()
                    {
                        auto Buffer = a->ReadBuffer;
                        if (Buffer->size() < 12) { return; }
                        auto SessionId = (*Buffer)[0] | (static_cast<std::int32_t>((*Buffer)[1]) << 8) | (static_cast<std::int32_t>((*Buffer)[2]) << 16) | (static_cast<std::int32_t>((*Buffer)[3]) << 24);
                        auto Flag = (*Buffer)[4] | (static_cast<std::int32_t>((*Buffer)[5]) << 8);
                        auto Index = (*Buffer)[6] | (static_cast<std::int32_t>((*Buffer)[7]) << 8);
                        auto Verification = (*Buffer)[8] | (static_cast<std::int32_t>((*Buffer)[9]) << 8) | (static_cast<std::int32_t>((*Buffer)[10]) << 16) | (static_cast<std::int32_t>((*Buffer)[11]) << 24);
                        (*Buffer)[8] = 0;
                        (*Buffer)[9] = 0;
                        (*Buffer)[10] = 0;
                        (*Buffer)[11] = 0;

                        //如果Flag中不包含ENC，则验证CRC32
                        if ((Flag & 2) == 0)
                        {
                            if (Algorithms::Cryptography::CRC32(*Buffer) != Verification) { return; }
                        }

                        //如果Flag中包含INI，则初始化
                        if ((Flag & 4) != 0)
                        {
                            if ((Flag & 1) != 0) { return; }
                            if ((Flag & 2) != 0) { return; }
                            if ((Flag & 8) != 0) { return; }
                            auto Offset = 12;

                            s = std::make_shared<UdpSession>(*this, a->Socket, ep, VirtualTransportServerFactory, QueueUserWorkItem);
                            SessionId = s->SessionId();

                            if (MaxConnectionsValue.OnHasValue() && (SessionSets.Check<int>([=](std::shared_ptr<ServerSessionSets> ss) { return static_cast<int>(ss->Sessions.size()); }) >= MaxConnectionsValue.Value()))
                            {
                                PurifyConsumer->DoOne();
                            }
                            if (MaxConnectionsValue.OnHasValue() && (SessionSets.Check<int>([=](std::shared_ptr<ServerSessionSets> ss) { return static_cast<int>(ss->Sessions.size()); }) >= MaxConnectionsValue.Value()))
                            {
                                BaseSystem::AutoRelease ar([&]()
                                {
                                    s = nullptr;
                                });
                                s->Start();
                                OnMaxConnectionsExceeded(s);
                                return;
                            }

                            if (MaxConnectionsPerIPValue.OnHasValue() && (SessionSets.Check<int>([=](std::shared_ptr<ServerSessionSets> ss) { return ss->IpSessions.count(ep.address()) > 0 ? ss->IpSessions[ep.address()]->Count : 0; }) >= MaxConnectionsPerIPValue.Value()))
                            {
                                BaseSystem::AutoRelease ar([&]()
                                {
                                    PurifyConsumer->Push(s);
                                });
                                s->Start();
                                OnMaxConnectionsPerIPExceeded(s);
                                return;
                            }

                            if (MaxUnauthenticatedPerIPValue.OnHasValue() && (SessionSets.Check<int>([=](std::shared_ptr<ServerSessionSets> ss) { return ss->IpSessions.count(ep.address()) > 0 ? ss->IpSessions[ep.address()]->Count : 0; }) >= MaxUnauthenticatedPerIPValue.Value()))
                            {
                                BaseSystem::AutoRelease ar([&]()
                                {
                                    PurifyConsumer->Push(s);
                                });
                                s->Start();
                                OnMaxConnectionsPerIPExceeded(s);
                                return;
                            }

                            SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
                            {
                                ss->Sessions.insert(s);
                                if (ss->IpSessions.count(ep.address()) > 0)
                                {
                                    ss->IpSessions[ep.address()]->Count += 1;
                                }
                                else
                                {
                                    auto isi = std::make_shared<IpSessionInfo>();
                                    isi->Count += 1;
                                    ss->IpSessions[ep.address()] = isi;
                                }
                                while ((SessionId == 0) || (ss->SessionIdToSession.count(SessionId) > 0))
                                {
                                    s = std::make_shared<UdpSession>(*this, a->Socket, ep, VirtualTransportServerFactory, QueueUserWorkItem);
                                    SessionId = s->SessionId();
                                }
                                ss->SessionIdToSession[SessionId] = s;
                            });

                            s->Start();

                            s->PrePush([=]()
                            {
                                if (!s->Push(ep, Index, nullptr, Buffer, Offset, Buffer->size() - Offset))
                                {
                                    PurifyConsumer->Push(s);
                                }
                            });
                        }
                        else
                        {
                            auto Close = false;
                            SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
                            {
                                if (ss->SessionIdToSession.count(SessionId) == 0)
                                {
                                    Close = true;
                                    return;
                                }
                                s = ss->SessionIdToSession[SessionId];
                            });
                            if (Close)
                            {
                                return;
                            }

                            s->PrePush([=]()
                            {
                                auto IsEncrypted = (Flag & 2) != 0;
                                auto NextSecureContext = s->NextSecureContext();
                                auto SecureContext = s->SecureContext();
                                if ((SecureContext == nullptr) && (NextSecureContext != nullptr))
                                {
                                    s->SecureContext(NextSecureContext);
                                    s->NextSecureContext(nullptr);
                                    SecureContext = NextSecureContext;
                                    NextSecureContext = nullptr;
                                }
                                if ((SecureContext != nullptr) != IsEncrypted)
                                {
                                    return;
                                }
                                if (IsEncrypted)
                                {
                                    std::vector<std::uint8_t> SHABuffer;
                                    SHABuffer.resize(4);
                                    ArrayCopy(*Buffer, 4, SHABuffer, 0, 4);
                                    auto SHA256 = Algorithms::Cryptography::SHA256(SHABuffer);
                                    std::vector<std::uint8_t> Key;
                                    Key.resize(SecureContext->ClientToken.size() + SHA256.size());
                                    ArrayCopy(SecureContext->ClientToken, 0, Key, 0, static_cast<int>(SecureContext->ClientToken.size()));
                                    ArrayCopy(SHA256, 0, Key, SecureContext->ClientToken.size(), static_cast<int>(SHA256.size()));
                                    auto HMACBytes = Algorithms::Cryptography::HMACSHA256Simple(Key, *Buffer);
                                    HMACBytes.resize(4);
                                    auto HMAC = HMACBytes[0] | (static_cast<std::int32_t>(HMACBytes[1]) << 8) | (static_cast<std::int32_t>(HMACBytes[2]) << 16) | (static_cast<std::int32_t>(HMACBytes[3]) << 24);
                                    if (HMAC != Verification) { return; }
                                }

                                auto Offset = 12;
                                std::shared_ptr<std::vector<int>> Indices = nullptr;
                                if ((Flag & 1) != 0)
                                {
                                    if (static_cast<int>(Buffer->size()) < 14)
                                    {
                                        return;
                                    }
                                    auto NumIndex = (*Buffer)[Offset] | (static_cast<std::int32_t>((*Buffer)[Offset + 1]) << 8);
                                    if (static_cast<int>(Buffer->size()) < 14 + NumIndex * 2)
                                    {
                                        return;
                                    }
                                    if (NumIndex > UdpSession::WritingWindowSize()) //若Index数量较大，则丢弃包
                                    {
                                        return;
                                    }
                                    Offset += 2;
                                    Indices = std::make_shared<std::vector<int>>();
                                    Indices->resize(NumIndex, 0);
                                    for (int k = 0; k < NumIndex; k += 1)
                                    {
                                        (*Indices)[k] = (*Buffer)[Offset + k * 2] | (static_cast<std::int32_t>((*Buffer)[Offset + k * 2 + 1]) << 8);
                                    }
                                    Offset += NumIndex * 2;
                                }


                                //如果Flag中包含AUX，则判断
                                if ((Flag & 8) != 0)
                                {
                                    if (Indices == nullptr) { return; }
                                    if (Indices->size() < 1) { return; }
                                    if (Index != (*Indices)[0]) { return; }
                                    if (Offset != Buffer->size()) { return; }
                                }

                                auto PreviousRemoteEndPoint = s->RemoteEndPoint;
                                if (PreviousRemoteEndPoint != ep)
                                {
                                    SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
                                    {
                                        auto Authenticated = false;
                                        {
                                            auto PreviousIpAddress = PreviousRemoteEndPoint.address();
                                            auto isi = ss->IpSessions[PreviousIpAddress];
                                            if (isi->Authenticated.count(s) > 0)
                                            {
                                                isi->Authenticated.erase(s);
                                                Authenticated = true;
                                            }
                                            isi->Count -= 1;
                                            if (isi->Count == 0)
                                            {
                                                ss->IpSessions.erase(PreviousIpAddress);
                                            }
                                        }

                                        {
                                            std::shared_ptr<IpSessionInfo> isi;
                                            if (ss->IpSessions.count(ep.address()) > 0)
                                            {
                                                isi = ss->IpSessions[ep.address()];
                                                isi->Count += 1;
                                            }
                                            else
                                            {
                                                isi = std::make_shared<IpSessionInfo>();
                                                isi->Count += 1;
                                                ss->IpSessions[ep.address()] = isi;
                                            }
                                            if (Authenticated)
                                            {
                                                isi->Authenticated.insert(s);
                                            }
                                        }

                                        s->RemoteEndPoint = ep;
                                    });
                                }

                                if ((Flag & 8) != 0)
                                {
                                    if (!s->PushAux(ep, Indices))
                                    {
                                        PurifyConsumer->Push(s);
                                    }
                                }
                                else
                                {
                                    if (!s->Push(ep, Index, Indices, Buffer, Offset, Buffer->size() - Offset))
                                    {
                                        PurifyConsumer->Push(s);
                                    }
                                }
                            });
                        }
                    });
                }
                catch (const std::exception &ex)
                {
                    if (ServerContext()->EnableLogSystem())
                    {
                        auto e = std::make_shared<SessionLogEntry>();
                        e->Token = u"";
                        e->RemoteEndPoint = systemToUtf16(ep.address().to_string()) + u":" + ToU16String(ep.port());
                        e->Time = UtcNow();
                        e->Type = u"Sys";
                        e->Name = u"Exception";
                        e->Message = systemToUtf16(std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace());
                        ServerContext()->RaiseSessionLog(e);
                    }
                    if (s != nullptr)
                    {
                        PurifyConsumer->Push(s);
                    }
                }
            };
            AcceptConsumer = std::make_shared<BaseSystem::AsyncConsumer<std::shared_ptr<AcceptingInfo>>>(QueueUserWorkItem, [=](std::shared_ptr<AcceptingInfo> a) { Accept(a); return true; }, std::numeric_limits<int>::max());

            auto Exceptions = std::make_shared<std::vector<asio::error_code>>();
            auto Bindings = std::make_shared<std::vector<asio::ip::udp::endpoint>>();
            //注意：因为asio不支持遍历网卡接口，所以不能将所有默认地址0.0.0.0换为实际的所有接口地址，这样在多网卡机器上使用0.0.0.0时会出现一些问题，此时应指定自己的IP地址
            for (auto Binding : *BindingsValue)
            {
                if ((Binding.address() == asio::ip::address_v4::any()) || (Binding.address() == asio::ip::address_v6::any()))
                {
                    //此处应将默认地址换为实际的接口地址
                    Bindings->push_back(Binding);
                }
                else
                {
                    Bindings->push_back(Binding);
                }
            }
            for (auto Binding : *Bindings)
            {
                auto CreateSocket = [=]() -> std::shared_ptr<asio::ip::udp::socket>
                {
                    auto s = std::make_shared<asio::ip::udp::socket>(IoService);

                    if (Binding.address().is_v4())
                    {
                        s->open(asio::ip::udp::v4());
                    }
                    else
                    {
                        s->open(asio::ip::udp::v6());
                    }

#if _MSC_VER
                    //在Windows下关闭SIO_UDP_CONNRESET报告，防止接受数据出错
                    //http://support.microsoft.com/kb/263823/en-us
					connection_reset_command command;
					s->io_control(command);
#endif
                    return s;
                };

                auto Socket = CreateSocket();

                asio::error_code ec;
                Socket->bind(Binding, ec);
                if (ec)
                {
                    Exceptions->push_back(ec);
                    continue;
                }

                auto BindingInfo = std::make_shared<class BindingInfo>();
                BindingInfo->EndPoint = Binding;
                BindingInfo->Socket = std::make_shared<BaseSystem::LockedVariable<std::shared_ptr<asio::ip::udp::socket>>>(Socket);
                BindingInfo->ReadBuffer = std::make_shared<std::vector<std::uint8_t>>();
                BindingInfo->ReadBuffer->resize(UdpSession::MaxPacketLength());
                auto BindingInfoPtr = &*BindingInfo;
                auto Completed = [this, Binding, CreateSocket, BindingInfoPtr](std::shared_ptr<AcceptResult> args) -> bool
                {
                    if (ListeningTaskToken->IsCancellationRequested()) { return false; }
                    if (!args->ec)
                    {
                        auto Count = args->BytesTransferred;
                        auto ReadBuffer = std::make_shared<std::vector<std::uint8_t>>();
                        ReadBuffer->resize(Count, 0);
                        ArrayCopy(*BindingInfoPtr->ReadBuffer, 0, *ReadBuffer, 0, Count);
                        auto a = std::make_shared<AcceptingInfo>();
                        a->Socket = BindingInfoPtr->Socket->Check<std::shared_ptr<asio::ip::udp::socket>>([](std::shared_ptr<asio::ip::udp::socket> s) { return s; });
                        a->ReadBuffer = ReadBuffer;
                        a->RemoteEndPoint = args->RemoteEndPoint;
                        AcceptConsumer->Push(a);
                    }
                    else
                    {
                        BindingInfoPtr->Socket->Update([=](std::shared_ptr<asio::ip::udp::socket> OriginalSocket) -> std::shared_ptr<asio::ip::udp::socket>
                        {
                            return nullptr;
                        });
                        BindingInfoPtr->Socket->Update([=](std::shared_ptr<asio::ip::udp::socket> OriginalSocket) -> std::shared_ptr<asio::ip::udp::socket>
                        {
                            auto NewSocket = CreateSocket();
                            NewSocket->bind(Binding);
                            return NewSocket;
                        });
                    }
                    BindingInfoPtr->Start();
                    return true;
                };
                BindingInfo->ListenConsumer = std::make_shared<BaseSystem::AsyncConsumer<std::shared_ptr<AcceptResult>>>(QueueUserWorkItem, Completed, 1);
                BindingInfo->Start = [this, BindingInfoPtr]()
                {
                    auto bs = BindingInfoPtr->Socket->Check<std::shared_ptr<asio::ip::udp::socket>>([](std::shared_ptr<asio::ip::udp::socket> s) { return s; });
                    auto ReadBuffer = BindingInfoPtr->ReadBuffer;
                    auto RemoteEndPoint = std::make_shared<asio::ip::udp::endpoint>();
                    auto ReadHandler = [=](const asio::error_code &ec, std::size_t Count)
                    {
                        if (ec == asio::error::operation_aborted) { return; }
                        auto a = std::make_shared<AcceptResult>();
                        a->ec = ec;
                        a->BytesTransferred = Count;
                        a->RemoteEndPoint = *RemoteEndPoint;
                        BindingInfoPtr->ListenConsumer->Push(a);
                    };
                    bs->async_receive_from(asio::buffer(*ReadBuffer), *RemoteEndPoint, ReadHandler);
                };

                BindingInfos.push_back(BindingInfo);
            }
            if (BindingInfos.size() == 0)
            {
                throw std::logic_error("NoValidBinding");
            }

            PurifyConsumer = std::make_shared<BaseSystem::AsyncConsumer<std::shared_ptr<UdpSession>>>(PurifierQueueUserWorkItem, [=](std::shared_ptr<UdpSession> s) { Purify(s); return true; }, std::numeric_limits<int>::max());

            if (UnauthenticatedSessionIdleTimeoutValue.OnHasValue() || SessionIdleTimeoutValue.OnHasValue())
            {
                DoTimeoutCheck();
            }

            for (auto BindingInfo : BindingInfos)
            {
                BindingInfo->Start();
            }

            Success = true;

            return true;
        });
    }

    void UdpServer::Stop()
    {
        IsRunningValue.Update([&](bool b)
        {
            if (!b) { return false; }

            if (LastActiveTimeCheckTimer != nullptr)
            {
                LastActiveTimeCheckTimer = nullptr;
            }

            if (ListeningTaskToken != nullptr)
            {
                ListeningTaskToken->Cancel();
            }
            for (auto BindingInfo : BindingInfos)
            {
                BindingInfo->Socket->Update([=](std::shared_ptr<asio::ip::udp::socket> OriginalSocket) -> std::shared_ptr<asio::ip::udp::socket>
                {
                    OriginalSocket->close();
                    return nullptr;
                });
            }
            for (auto BindingInfo : BindingInfos)
            {
                BindingInfo->ListenConsumer = nullptr;
            }
            BindingInfos.clear();
            if (ListeningTaskToken != nullptr)
            {
                ListeningTaskToken = nullptr;
            }

            if (AcceptConsumer != nullptr)
            {
                AcceptConsumer = nullptr;
            }

            std::unordered_set<std::shared_ptr<UdpSession>> Sessions;
            SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
            {
                Sessions = ss->Sessions;
                ss->Sessions.clear();
                ss->IpSessions.clear();
                ss->SessionIdToSession.clear();
            });
            for (auto s : Sessions)
            {
                s->Stop();
            }
            Sessions.clear();

            if (PurifyConsumer != nullptr)
            {
                PurifyConsumer = nullptr;
            }

            return false;
        });
    }

    void UdpServer::NotifySessionQuit(std::shared_ptr<UdpSession> s)
    {
        PurifyConsumer->Push(s);
    }
    void UdpServer::NotifySessionAuthenticated(std::shared_ptr<UdpSession> s)
    {
        auto e = s->RemoteEndPoint;
        SessionSets.DoAction([=](std::shared_ptr<ServerSessionSets> ss)
        {
            if (ss->IpSessions.count(e.address()) > 0)
            {
                auto isi = ss->IpSessions[e.address()];
                if (isi->Authenticated.count(s) == 0)
                {
                    isi->Authenticated.insert(s);
                }
            }
        });
    }

    UdpServer::~UdpServer()
    {
        Stop();
    }
}
