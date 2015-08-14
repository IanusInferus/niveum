#include "TcpServer.h"
#include "TcpSession.h"

#include "BaseSystem/AutoRelease.h"
#include "BaseSystem/StringUtilities.h"

#include <limits>
#include <algorithm>
#include <chrono>

namespace Server
{
    TcpServer::TcpServer(asio::io_service &IoService, std::shared_ptr<IServerContext> sc, std::function<std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>(std::shared_ptr<ISessionContext>, std::shared_ptr<IBinaryTransformer>)> VirtualTransportServerFactory, std::function<void(std::function<void()>)> QueueUserWorkItem, std::function<void(std::function<void()>)> PurifierQueueUserWorkItem)
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
        SessionMappings(std::make_shared<std::unordered_map<std::shared_ptr<ISessionContext>, std::shared_ptr<TcpSession>>>())
    {
        ServerContext(sc);
    }

    void TcpServer::OnMaxConnectionsExceeded(std::shared_ptr<TcpSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(L"", L"Client host rejected: too many connections, please try again later.");
        }
    }
    void TcpServer::OnMaxConnectionsPerIPExceeded(std::shared_ptr<TcpSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(L"", L"Client host rejected: too many connections from your IP(" + s2w(s->RemoteEndPoint.address().to_string()) + L"), please try again later.");
        }
    }

    void TcpServer::DoTimeoutCheck()
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

    void TcpServer::Start()
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

            auto Purify = [=](std::shared_ptr<TcpSession> StoppingSession)
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
                    }
                });
                StoppingSession->Stop();
            };

            auto Accept = [=](std::shared_ptr<asio::ip::tcp::socket> a)
            {
                asio::error_code ec;
                auto ep = a->remote_endpoint(ec);
                if (ec)
                {
                    a->close(ec);
                    return;
                }
                auto s = std::make_shared<TcpSession>(*this, a, ep, VirtualTransportServerFactory, QueueUserWorkItem);

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

                SessionSets.DoAction([=](std::shared_ptr<ServerSessionSets> ss)
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
                });

                s->Start();
            };
            AcceptConsumer = std::make_shared<BaseSystem::AsyncConsumer<std::shared_ptr<asio::ip::tcp::socket>>>(QueueUserWorkItem, [=](std::shared_ptr<asio::ip::tcp::socket> a) { Accept(a); return true; }, std::numeric_limits<int>::max());

            auto Exceptions = std::make_shared<std::vector<asio::error_code>>();
            for (auto Binding : *BindingsValue)
            {
                auto CreateSocket = [=]() -> std::shared_ptr<asio::ip::tcp::acceptor>
                {
                    auto s = std::make_shared<asio::ip::tcp::acceptor>(IoService, Binding);
                    return s;
                };

                auto Socket = CreateSocket();

                asio::error_code ec;
                Socket->listen(MaxConnectionsValue.OnHasValue() ? (MaxConnectionsValue.Value() + 1) : 128, ec);
                if (ec)
                {
                    Exceptions->push_back(ec);
                    continue;
                }

                auto BindingInfo = std::make_shared<class BindingInfo>();
                BindingInfo->Socket = std::make_shared<BaseSystem::LockedVariable<std::shared_ptr<asio::ip::tcp::acceptor>>>(Socket);
                auto BindingInfoPtr = &*BindingInfo;
                auto Completed = [this, Binding, CreateSocket, BindingInfoPtr](std::shared_ptr<AcceptResult> args) -> bool
                {
                    if (ListeningTaskToken->IsCancellationRequested()) { return false; }
                    if (!args->ec)
                    {
                        auto a = args->AcceptSocket;
                        AcceptConsumer->Push(a);
                    }
                    else
                    {
                        BindingInfoPtr->Socket->Update([=](std::shared_ptr<asio::ip::tcp::acceptor> OriginalSocket) -> std::shared_ptr<asio::ip::tcp::acceptor>
                        {
                            return nullptr;
                        });
                        BindingInfoPtr->Socket->Update([=](std::shared_ptr<asio::ip::tcp::acceptor> OriginalSocket) -> std::shared_ptr<asio::ip::tcp::acceptor>
                        {
                            auto NewSocket = CreateSocket();
                            NewSocket->listen(MaxConnectionsValue.OnHasValue() ? (MaxConnectionsValue.Value() + 1) : 128);
                            return NewSocket;
                        });
                    }
                    BindingInfoPtr->Start();
                    return true;
                };
                BindingInfo->ListenConsumer = std::make_shared<BaseSystem::AsyncConsumer<std::shared_ptr<AcceptResult>>>(QueueUserWorkItem, Completed, 1);
                BindingInfo->Start = [this, BindingInfoPtr]()
                {
                    auto a = std::make_shared<AcceptResult>();
                    a->AcceptSocket = std::make_shared<asio::ip::tcp::socket>(IoService);
                    auto RemoteEndPoint = std::make_shared<asio::ip::tcp::endpoint>();
                    auto bs = BindingInfoPtr->Socket->Check<std::shared_ptr<asio::ip::tcp::acceptor>>([](std::shared_ptr<asio::ip::tcp::acceptor> s) { return s; });
                    bs->async_accept(*a->AcceptSocket, *RemoteEndPoint, [this, a, RemoteEndPoint, BindingInfoPtr](const asio::error_code& ec)
                    {
                        if (ec == asio::error::operation_aborted) { return; }
                        a->ec = ec;
                        BindingInfoPtr->ListenConsumer->Push(a);
                    });
                };

                BindingInfos.push_back(BindingInfo);
            }
            if (BindingInfos.size() == 0)
            {
                throw std::logic_error("NoValidBinding");
            }

            PurifyConsumer = std::make_shared<BaseSystem::AsyncConsumer<std::shared_ptr<TcpSession>>>(PurifierQueueUserWorkItem, [=](std::shared_ptr<TcpSession> s) { Purify(s); return true; }, std::numeric_limits<int>::max());

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

    void TcpServer::Stop()
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
                BindingInfo->Socket->Update([=](std::shared_ptr<asio::ip::tcp::acceptor> OriginalSocket) -> std::shared_ptr<asio::ip::tcp::acceptor>
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

            std::unordered_set<std::shared_ptr<TcpSession>> Sessions;
            SessionSets.DoAction([&](std::shared_ptr<ServerSessionSets> ss)
            {
                Sessions = ss->Sessions;
                ss->Sessions.clear();
                ss->IpSessions.clear();
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

    void TcpServer::NotifySessionQuit(std::shared_ptr<TcpSession> s)
    {
        PurifyConsumer->Push(s);
    }
    void TcpServer::NotifySessionAuthenticated(std::shared_ptr<TcpSession> s)
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

    TcpServer::~TcpServer()
    {
        Stop();
    }
}
