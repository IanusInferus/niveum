#include "Net/TcpServer.h"

namespace Communication
{
    namespace Net
    {
        class TcpServer::BindingInfo
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

        TcpServer::TcpServer(boost::asio::io_service &IoService)
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

        TcpServer::~TcpServer()
        {
            Stop();
        }

        bool TcpServer::IsRunning()
        {
            return IsRunningValue.Check<bool>([](const bool &s) { return s; });
        }

        std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> TcpServer::GetBindings() const
        {
            return BindingsValue;
        }
        void TcpServer::SetBindings(std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> Bindings)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                BindingsValue = Bindings;
            });
        }

        std::shared_ptr<Communication::BaseSystem::Optional<int>> TcpServer::GetSessionIdleTimeout() const
        {
            return SessionIdleTimeoutValue;
        }
        void TcpServer::SetSessionIdleTimeout(std::shared_ptr<Communication::BaseSystem::Optional<int>> ms)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                SessionIdleTimeoutValue = ms;
            });
        }

        std::shared_ptr<Communication::BaseSystem::Optional<int>> TcpServer::GetMaxConnections() const
        {
            return MaxConnectionsValue;
        }
        void TcpServer::SetMaxConnections(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxConnectionsValue = v;
            });
        }

        std::shared_ptr<Communication::BaseSystem::Optional<int>> TcpServer::GetMaxConnectionsPerIP() const
        {
            return MaxConnectionsPerIPValue;
        }
        void TcpServer::SetMaxConnectionsPerIP(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
        {
            IsRunningValue.DoAction([&](bool &b)
            {
                if (b) { throw std::logic_error("InvalidOperationException"); }
                MaxConnectionsPerIPValue = v;
            });
        }

        void TcpServer::DoAccepting()
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

                    auto s = CreateSession();
                    s->NotifySessionQuit = [=]() { NotifySessionQuit(s); };
                    s->RemoteEndPoint = Socket->remote_endpoint();
                    s->IdleTimeout = SessionIdleTimeoutValue;
                    s->SetSocket(Socket);

                    if (MaxConnectionsValue->OnHasValue())
                    {
                        int SessionCount = Sessions.Check<int>([=](std::shared_ptr<TSessionSet> ss) -> int
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
                                MaxConnectionsExceeded(s);
                            }
                            continue;
                        }
                    }

                    auto Address = s->RemoteEndPoint.address();
                    if (MaxConnectionsPerIPValue->OnHasValue())
                    {
                        int IpSessionCount = IpSessions.Check<int>([=](std::shared_ptr<TIpAddressMap> iss) -> int
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
                                MaxConnectionsPerIPExceeded(s);
                            }
                            continue;
                        }
                    }

                    Sessions.DoAction([=](std::shared_ptr<TSessionSet> &ss)
                    {
                        ss->insert(s);
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

        void TcpServer::DoPurifiering()
        {
            while (true)
            {
                if (PurifieringTaskToken.IsCancellationRequested()) { return; }
                PurifieringTaskNotifier.WaitOne();
                while (true)
                {
                    std::shared_ptr<TcpSession> StoppingSession = nullptr;
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

        bool TcpServer::DoStopping(bool b)
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

        void TcpServer::Stop()
        {
            IsRunningValue.Update([&](bool b) -> bool
            {
                return DoStopping(b);
            });
        }

        void TcpServer::Start()
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
