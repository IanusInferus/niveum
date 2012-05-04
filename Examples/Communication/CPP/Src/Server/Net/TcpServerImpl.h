#pragma once

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
#ifdef __GNUC__
#include <boost/functional/hash.hpp>
#endif
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/thread.hpp>

namespace Communication
{
    namespace Net
    {
        template <typename TServer, typename TSession>
        class TcpServer<TServer, TSession>::BindingInfo
        {
        private:
            boost::asio::io_service &IoService;
            std::shared_ptr<boost::thread> Task;
            Communication::BaseSystem::LockedVariable<bool> IsExited;

        public:
            std::shared_ptr<boost::asio::ip::tcp::acceptor> Acceptor;

            BindingInfo(boost::asio::io_service &IoService)
                : IoService(IoService),
                Task(nullptr),
                IsExited(false)
            {
            }

            ~BindingInfo()
            {
                IsExited.Update([](const bool &b) { return false; });
                if (Acceptor != nullptr)
                {
                    Acceptor->close();
                }
                if (Task != nullptr)
                {
                    Task->join();
                    Task = nullptr;
                }
            }

            void StartAccept(std::function<void(std::shared_ptr<boost::asio::ip::tcp::socket>)> AcceptHandler)
            {
                if (Task != nullptr) { throw std::logic_error("ThreadStarted"); }

                Task = std::make_shared<boost::thread>([=]()
                {
                    while (true)
                    {
                        if (IsExited.Check<bool>([](const bool &b) { return b; }))
                        {
                            return;
                        }
                        auto Socket = std::make_shared<boost::asio::ip::tcp::socket>(IoService);
                        boost::system::error_code se;
                        Acceptor->accept(*Socket, se);
                        if (se == boost::system::errc::success)
                        {
                            AcceptHandler(Socket);
                        }
                        else if (se == boost::system::errc::interrupted)
                        {
                            return;
                        }
                        else
                        {
                            auto Message = "UnexpectedError: " + se.message();
                            throw std::logic_error(Message);
                        }
                    }
                });
            }
        };

        template <typename TServer, typename TSession>
        TcpServer<TServer, TSession>::TcpServer(boost::asio::io_service &IoService)
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

        template <typename TServer, typename TSession>
        TcpServer<TServer, TSession>::~TcpServer()
        {
            Stop();
        }

        template <typename TServer, typename TSession>
        bool TcpServer<TServer, TSession>::IsRunning()
        {
            return IsRunningValue.Check<bool>([](const bool &s) { return s; });
        }

        template <typename TServer, typename TSession>
        std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> TcpServer<TServer, TSession>::GetBindings() const
        {
            return BindingsValue;
        }
        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::SetBindings(std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> Bindings)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                BindingsValue = Bindings;
            });
        }

        template <typename TServer, typename TSession>
        std::shared_ptr<Communication::BaseSystem::Optional<int>> TcpServer<TServer, TSession>::GetSessionIdleTimeout() const
        {
            return SessionIdleTimeoutValue;
        }
        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::SetSessionIdleTimeout(std::shared_ptr<Communication::BaseSystem::Optional<int>> ms)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                SessionIdleTimeoutValue = ms;
            });
        }

        template <typename TServer, typename TSession>
        std::shared_ptr<Communication::BaseSystem::Optional<int>> TcpServer<TServer, TSession>::GetMaxConnections() const
        {
            return MaxConnectionsValue;
        }
        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::SetMaxConnections(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxConnectionsValue = v;
            });
        }

        template <typename TServer, typename TSession>
        std::shared_ptr<Communication::BaseSystem::Optional<int>> TcpServer<TServer, TSession>::GetMaxConnectionsPerIP() const
        {
            return MaxConnectionsPerIPValue;
        }
        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::SetMaxConnectionsPerIP(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxConnectionsPerIPValue = v;
            });
        }

        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::DoAccepting()
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

                    auto ts = CreateSession();
                    auto s = static_cast<std::shared_ptr<TcpSession<TSession>>>(ts);
                    s->NotifySessionQuit = [&](std::shared_ptr<TSession> s) { NotifySessionQuit(s); };
                    s->RemoteEndPoint = Socket->remote_endpoint();
                    s->IdleTimeout = SessionIdleTimeoutValue;
                    s->SetSocket(Socket);

                    if (MaxConnectionsValue->OnHasValue())
                    {
                        int SessionCount = Sessions.template Check<int>([=](std::shared_ptr<TSessionSet> ss) -> int
                        {
                            return (int)(ss->size());
                        });

                        if (SessionCount >= MaxConnectionsValue->HasValue)
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
                    }

                    auto Address = s->RemoteEndPoint.address();
                    if (MaxConnectionsPerIPValue->OnHasValue())
                    {
                        int IpSessionCount = IpSessions.template Check<int>([=](std::shared_ptr<TIpAddressMap> iss) -> int
                        {
                            return iss->count(Address) > 0 ? (*iss)[Address] : 0;
                        });

                        if (IpSessionCount >= MaxConnectionsPerIPValue->HasValue)
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

        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::DoPurifiering()
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

        template <typename TServer, typename TSession>
        bool TcpServer<TServer, TSession>::DoStopping(bool b)
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
        }

        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::Stop()
        {
            IsRunningValue.Update([&](bool b) -> bool
            {
                return DoStopping(b);
            });
        }

        template <typename TServer, typename TSession>
        void TcpServer<TServer, TSession>::Start()
        {
            bool Success = false;
            Communication::BaseSystem::AutoRelease ar([&]()
            {
                if (!Success)
                {
                    Stop();
                }
            });

            auto AcceptHandler = [this](std::shared_ptr<boost::asio::ip::tcp::socket> Socket)
            {
                this->AcceptedSockets.DoAction([=](std::shared_ptr<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>> &Sockets)
                {
                    Sockets->push(Socket);
                });
                this->AcceptingTaskNotifier.Set();
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
    }
}
