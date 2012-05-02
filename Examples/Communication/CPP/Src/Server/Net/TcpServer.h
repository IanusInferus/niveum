#pragma once

#include "Net/TcpSession.h"

#include "BaseSystem/LockedVariable.h"
#include "BaseSystem/CancellationToken.h"
#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Optional.h"
#include "BaseSystem/AutoRelease.h"

#include <memory>
#include <cstdint>
#include <vector>
#include <queue>
#include <unordered_set>
#include <unordered_map>
#include <string>
#include <exception>
#include <stdexcept>
#include <functional>
#include <boost/asio.hpp>
#include <boost/thread.hpp>

namespace Communication
{
    namespace Net
    {
        template <typename TServer, typename TSession>
        class TcpServer : public std::enable_shared_from_this<TServer>
        {
        private:
            class BindingInfo
            {
            private:
                boost::asio::io_service &IoService;

            public:
                BindingInfo(boost::asio::io_service &IoService)
                    : IoService(IoService)
                {
                }

                std::shared_ptr<boost::asio::ip::tcp::acceptor> Acceptor;
                void StartAccept(std::function<void(std::shared_ptr<boost::asio::ip::tcp::socket>)> AcceptHandler)
                {
                    auto Socket = std::make_shared<boost::asio::ip::tcp::socket>(IoService);
                    Acceptor->async_accept(*Socket, [=](const boost::system::error_code &se)
                    {
                        if (se == boost::system::errc::success)
                        {
                            AcceptHandler(Socket);
                        }
                        else if (se == boost::system::errc::interrupted)
                        {
                            return;
                        }
                        StartAccept(AcceptHandler);
                    });
                }
            };

            Communication::BaseSystem::LockedVariable<bool> IsRunningValue;

        private:
            template <typename T>
            struct SharedPtrHash
            {
                std::size_t operator() (const std::shared_ptr<T> &p) const
                {
                    return (std::size_t)(p.get());
                }
            };
            typedef std::unordered_set<std::shared_ptr<TSession>, SharedPtrHash<TSession>> TSessionSet;
            struct IpAddressHash
            {
                std::size_t operator() (const boost::asio::ip::address &p) const
                {
                    if (p.is_v4())
                    {
                        auto Bytes = p.to_v4().to_bytes();
                        auto a = (uint8_t (*)[sizeof(Bytes)])(Bytes.data());
                        return std::hash<decltype(*a)>()(*a);
                    }
                    else if (p.is_v6())
                    {
                        auto Bytes = p.to_v6().to_bytes();
                        auto a = (uint8_t (*)[sizeof(Bytes)])(Bytes.data());
                        return std::hash<decltype(*a)>()(*a);
                    }
                    else
                    {
                        auto s = p.to_string();
                        return std::hash<decltype(s)>()(s);
                    }
                }
            };
            typedef std::unordered_map<boost::asio::ip::address, int, IpAddressHash> TIpAddressMap;

            boost::asio::io_service &IoService;

            std::vector<std::shared_ptr<BindingInfo>> BindingInfos;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>>> AcceptedSockets;
            std::shared_ptr<boost::thread> AcceptingTask;
            Communication::BaseSystem::CancellationToken AcceptingTaskToken;
            Communication::BaseSystem::AutoResetEvent AcceptingTaskNotifier;
            std::shared_ptr<boost::thread> PurifieringTask;
            Communication::BaseSystem::CancellationToken PurifieringTaskToken;
            Communication::BaseSystem::AutoResetEvent PurifieringTaskNotifier;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<TSessionSet>> Sessions;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<TIpAddressMap>> IpSessions;
            Communication::BaseSystem::LockedVariable<std::shared_ptr<TSessionSet>> StoppingSessions;

            std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> BindingsValue;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> SessionIdleTimeoutValue;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> MaxConnectionsValue;
            std::shared_ptr<Communication::BaseSystem::Optional<int>> MaxConnectionsPerIPValue;

        public:
            TcpServer(boost::asio::io_service &IoService)
                : IoService(IoService),
                  IsRunningValue(false),
                  AcceptedSockets(std::make_shared<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>>()),
                  Sessions(std::make_shared<TSessionSet>()),
                  IpSessions(std::make_shared<TIpAddressMap>()),
                  StoppingSessions(std::make_shared<TSessionSet>()),
                  BindingsValue(std::make_shared<std::vector<boost::asio::ip::tcp::endpoint>>()),
                  SessionIdleTimeoutValue(Communication::BaseSystem::Optional<int>::CreateNotHasValue()),
                  MaxConnectionsValue(Communication::BaseSystem::Optional<int>::CreateNotHasValue()),
                  MaxConnectionsPerIPValue(Communication::BaseSystem::Optional<int>::CreateNotHasValue())
            {
            }

            virtual ~TcpServer()
            {
                Stop();
            }

            bool IsRunning()
            {
                return IsRunningValue.Check<bool>([](const bool &s) { return s; });
            }

            std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> GetBindings() const
            {
                return BindingsValue;
            }
            void SetBindings(std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> Bindings)
            {
                IsRunningValue.DoAction([&](bool &b)
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }
                    BindingsValue = Bindings;
                });
            }

            std::shared_ptr<Communication::BaseSystem::Optional<int>> GetSessionIdleTimeout() const
            {
                return SessionIdleTimeoutValue;
            }
            void SetSessionIdleTimeout(std::shared_ptr<Communication::BaseSystem::Optional<int>> ms)
            {
                IsRunningValue.DoAction([&](bool &b)
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }
                    SessionIdleTimeoutValue = ms;
                });
            }

            std::shared_ptr<Communication::BaseSystem::Optional<int>> GetMaxConnections() const
            {
                return MaxConnectionsValue;
            }
            void SetMaxConnections(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
            {
                IsRunningValue.DoAction([&](bool &b)
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }
                    MaxConnectionsValue = v;
                });
            }

            std::shared_ptr<Communication::BaseSystem::Optional<int>> GetMaxConnectionsPerIP() const
            {
                return MaxConnectionsPerIPValue;
            }
            void SetMaxConnectionsPerIP(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
            {
                IsRunningValue.DoAction([&](bool &b)
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }
                    MaxConnectionsPerIPValue = v;
                });
            }

            void Start()
            {
                bool Success = false;
                Communication::BaseSystem::AutoRelease ar([&]()
                {
                    if (!Success)
                    {
                        Stop();
                    }
                });

                auto AcceptHandler = [&](std::shared_ptr<boost::asio::ip::tcp::socket> Socket)
                {
                    AcceptedSockets.DoAction([&](std::shared_ptr<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>> &Sockets)
                    {
                        Sockets->push(Socket);
                        AcceptingTaskNotifier.Set();
                    });
                };

                auto AcceptingHandler = [=]() { DoAccepting(); };
                auto PurifieringHandler = [=]() { DoPurifiering(); };

                IsRunningValue.Update([&](bool b) -> bool
                {
                    if (b) { throw std::logic_error("InvalidOperationException"); }

                    if (BindingsValue->size() == 0)
                    {
                        throw std::logic_error("NoValidBinding");
                    }

                    auto Exceptions = std::make_shared<std::vector<std::exception>>();
                    for (int k = 0; k < (int)(BindingsValue->size()); k += 1)
                    {
                        auto &Binding = (*BindingsValue)[k];
                        auto bi = std::make_shared<BindingInfo>(IoService);
                        try
                        {
                            auto Acceptor = std::make_shared<boost::asio::ip::tcp::acceptor>(IoService, Binding);
                            bi->Acceptor = Acceptor;
                            Acceptor->listen();
                        }
                        catch (std::exception &ex)
                        {
                            Exceptions->push_back(ex);
                            continue;
                        }
                        bi->StartAccept(AcceptHandler);
                        BindingInfos.push_back(bi);
                    }
                    if (BindingInfos.size() == 0)
                    {
                        if (Exceptions->size() > 0)
                        {
                            throw std::logic_error((*Exceptions)[0].what());
                        }
                        else
                        {
                            throw std::logic_error("NoValidBinding");
                        }
                    }

                    AcceptingTask = std::make_shared<boost::thread>(AcceptingHandler);

                    PurifieringTask = std::make_shared<boost::thread>(PurifieringHandler);

                    Success = true;

                    return true;
                });
            }

        private:
            void DoAccepting()
            {
                while (true)
                {
                    if (AcceptingTaskToken.IsCancellationRequested()) { return; }
                    AcceptingTaskNotifier.WaitOne();
                    while (true)
                    {
                        std::shared_ptr<boost::asio::ip::tcp::socket> Socket = nullptr;
                        AcceptedSockets.DoAction([&](std::shared_ptr<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>> &Sockets)
                        {
                            if (Sockets->size() > 0)
                            {
                                Socket = Sockets->front();
                                Sockets->pop();
                            }
                        });
                        if (Socket == nullptr)
                        {
                            break;
                        }

                        auto ts = std::make_shared<TSession>(IoService);
                        std::shared_ptr<TcpSession<TServer, TSession>> s = ts;
                        s->Server = this->shared_from_this();
                        s->NotifySessionQuit = [&](std::shared_ptr<TSession> s) { NotifySessionQuit(s); };
                        s->RemoteEndPoint = Socket->remote_endpoint();
                        s->IdleTimeout = SessionIdleTimeoutValue;
                        s->SetSocket(Socket);

                        if (MaxConnectionsValue->OnHasValue() && (Sessions.Check<int>([](std::shared_ptr<TSessionSet> ss) { return (int)(ss->size()); }) >= MaxConnectionsValue->HasValue))
                        {
                            Communication::BaseSystem::AutoRelease Final([&]()
                            {
                                s->Stop();
                            });
                            s->Start();
                            if (MaxConnectionsExceeded != nullptr)
                            {
                                MaxConnectionsExceeded(ts);
                            }
                            continue;
                        }

                        auto Address = s->RemoteEndPoint.address();
                        if (MaxConnectionsPerIPValue->OnHasValue() && (IpSessions.Check<int>([&](std::shared_ptr<TIpAddressMap> iss) { return iss->count(Address) > 0 ? (*iss)[Address] : 0; }) >= MaxConnectionsPerIPValue->HasValue))
                        {
                            Communication::BaseSystem::AutoRelease Final([&]()
                            {
                                s->Stop();
                            });
                            s->Start();
                            if (MaxConnectionsPerIPExceeded != nullptr)
                            {
                                MaxConnectionsPerIPExceeded(ts);
                            }
                            continue;
                        }

                        Sessions.DoAction([=](std::shared_ptr<TSessionSet> &ss)
                        {
                            ss->insert(ts);
                        });
                        IpSessions.DoAction([=](std::shared_ptr<TIpAddressMap> &iss)
                        {
                            if (iss->count(Address) > 0)
                            {
                                (*iss)[Address] += 1;
                            }
                            else
                            {
                                (*iss)[Address] = 1;
                            }
                        });

                        s->Start();
                    }
                }
            }

            void DoPurifiering()
            {
                while (true)
                {
                    if (PurifieringTaskToken.IsCancellationRequested()) { return; }
                    PurifieringTaskNotifier.WaitOne();
                    while (true)
                    {
                        std::shared_ptr<TSession> StoppingSession = nullptr;
                        StoppingSessions.DoAction([&](std::shared_ptr<TSessionSet> &Sessions)
                        {
                            if (Sessions->size() > 0)
                            {
                                StoppingSession = *(Sessions->begin());
                                Sessions->erase(StoppingSession);
                            }
                        });
                        if (StoppingSession == nullptr)
                        {
                            break;
                        }
                        auto Removed = false;
                        Sessions.DoAction([&](std::shared_ptr<TSessionSet> &ss)
                        {
                            if (ss->count(StoppingSession) > 0)
                            {
                                ss->erase(StoppingSession);
                                Removed = true;
                            }
                        });
                        if (Removed)
                        {
                            IpSessions.DoAction([&](std::shared_ptr<TIpAddressMap> &iss)
                            {
                                auto Address = StoppingSession->RemoteEndPoint.address();
                                if (iss->count(Address) > 0)
                                {
                                    (*iss)[Address] -= 1;
                                    if ((*iss)[Address] == 0)
                                    {
                                        iss->erase(Address);
                                    }
                                }
                            });
                        }
                        StoppingSession->Stop();
                    }
                }
            }

        public:
            void Stop()
            {
                IsRunningValue.Update([&](bool b) -> bool
                {
                    if (!b) { return false; }

                    for (int k = 0; k < (int)(BindingInfos.size()); k += 1)
                    {
                        auto bi = BindingInfos[k];
                        bi->Acceptor->close();
                    }
                    BindingInfos.clear();

                    if (AcceptingTask != nullptr)
                    {
                        AcceptingTaskToken.Cancel();
                        AcceptingTaskNotifier.Set();
                    }
                    if (PurifieringTask != nullptr)
                    {
                        PurifieringTaskToken.Cancel();
                        PurifieringTaskNotifier.Set();
                    }
                    if (AcceptingTask != nullptr)
                    {
                        AcceptingTask->join();
                        AcceptingTask = nullptr;
                    }
                    if (PurifieringTask != nullptr)
                    {
                        PurifieringTask->join();
                        PurifieringTask = nullptr;
                    }

                    Sessions.DoAction([=](std::shared_ptr<TSessionSet> &ss)
                    {
                        for (auto i = ss->begin(); i != ss->end(); i.operator ++())
                        {
                            auto s = *i;
                            s->Stop();
                        }
                        ss->clear();
                    });
                    IpSessions.DoAction([=](std::shared_ptr<TIpAddressMap> &iss)
                    {
                        iss->clear();
                    });

                    return false;
                });
            }

            void NotifySessionQuit(std::shared_ptr<TSession> s)
            {
                StoppingSessions.DoAction([&](std::shared_ptr<TSessionSet> &Sessions)
                {
                    Sessions->insert(s);
                });
                PurifieringTaskNotifier.Set();
            }

            std::function<void(std::shared_ptr<TSession>)> MaxConnectionsExceeded;
            std::function<void(std::shared_ptr<TSession>)> MaxConnectionsPerIPExceeded;
        };
    }
}
