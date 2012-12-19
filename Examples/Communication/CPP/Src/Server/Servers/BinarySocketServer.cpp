#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include "Utility.h"

#include <stdexcept>
#include <boost/date_time/posix_time/posix_time.hpp>

namespace Server
{
    class BinarySocketServer::BindingInfo
    {
    private:
        boost::asio::io_service &IoService;
        boost::asio::ip::tcp::endpoint LocalEndPoint;
        std::shared_ptr<boost::thread> Task;
        Communication::BaseSystem::LockedVariable<std::shared_ptr<boost::asio::ip::tcp::acceptor>> Acceptor;
        Communication::BaseSystem::CancellationToken ListeningTaskToken;

    public:
        BindingInfo(boost::asio::io_service &IoService)
            : IoService(IoService),
            Task(nullptr),
            Acceptor(nullptr)
        {
        }

        ~BindingInfo()
        {
            ListeningTaskToken.Cancel();
            Acceptor.Update([&](const std::shared_ptr<boost::asio::ip::tcp::acceptor> &a) -> std::shared_ptr<boost::asio::ip::tcp::acceptor>
            {
                if (a != nullptr)
                {
                    boost::asio::ip::tcp::endpoint LoopBackEndPoint;
                    if (LocalEndPoint.protocol() == boost::asio::ip::tcp::v4())
                    {
                        LoopBackEndPoint = boost::asio::ip::tcp::endpoint(boost::asio::ip::address_v4::loopback(), LocalEndPoint.port());
                    }
                    else
                    {
                        LoopBackEndPoint = boost::asio::ip::tcp::endpoint(boost::asio::ip::address_v6::loopback(), LocalEndPoint.port());
                    }
                    try
                    {
                        auto Socket = std::make_shared<boost::asio::ip::tcp::socket>(IoService);
                        try
                        {
                            Socket->connect(LoopBackEndPoint);
                        }
                        catch (std::exception &)
                        {
                        }
                        try
                        {
                            Socket->shutdown(boost::asio::ip::tcp::socket::shutdown_both);
                        }
                        catch (std::exception &)
                        {
                        }
                        Socket->close();
                    }
                    catch (std::exception &)
                    {
                    }
                    if (Task != nullptr)
                    {
                        Task->join();
                        Task = nullptr;
                    }
                    try
                    {
                        a->close();
                    }
                    catch (std::exception &)
                    {
                    }
                }
                return nullptr;
            });
        }

        void Listen(boost::asio::ip::tcp::endpoint LocalEndPoint)
        {
            if (Task != nullptr) { throw std::logic_error("ThreadStarted"); }

            this->LocalEndPoint = LocalEndPoint;
            Acceptor.Update([=](const std::shared_ptr<boost::asio::ip::tcp::acceptor> &a) -> std::shared_ptr<boost::asio::ip::tcp::acceptor>
            {
                return std::make_shared<boost::asio::ip::tcp::acceptor>(IoService, LocalEndPoint);
            });
        }

        void StartAccept(std::function<void(std::shared_ptr<boost::asio::ip::tcp::socket>)> AcceptHandler)
        {
            if (Task != nullptr) { throw std::logic_error("ThreadStarted"); }

            Task = std::make_shared<boost::thread>([=]()
            {
                while (true)
                {
                    if (ListeningTaskToken.IsCancellationRequested())
                    {
                        return;
                    }
                    auto Socket = std::make_shared<boost::asio::ip::tcp::socket>(IoService);
                    boost::system::error_code se;
                    Acceptor.Check<std::shared_ptr<boost::asio::ip::tcp::acceptor>>([](const std::shared_ptr<boost::asio::ip::tcp::acceptor> &a) { return a; })->accept(*Socket, se);
                    if (ListeningTaskToken.IsCancellationRequested())
                    {
                        Socket->close();
                        return;
                    }
                    if (se == boost::system::errc::success)
                    {
                        AcceptHandler(Socket);
                    }
                    else
                    {
                        Relisten(LocalEndPoint);
                    }
                }
            });
        }

    private:
        void Relisten(boost::asio::ip::tcp::endpoint LocalEndPoint)
        {
            this->LocalEndPoint = LocalEndPoint;
            Acceptor.Update([=](const std::shared_ptr<boost::asio::ip::tcp::acceptor> &a) -> std::shared_ptr<boost::asio::ip::tcp::acceptor>
            {
                if (a != nullptr)
                {
                    try
                    {
                        a->close();
                    }
                    catch (std::exception &)
                    {
                    }
                }
                auto b = std::make_shared<boost::asio::ip::tcp::acceptor>(IoService, LocalEndPoint);
                return b;
            });
        }
    };

    BinarySocketServer::BinarySocketServer(boost::asio::io_service &IoService)
        :
        IoService(IoService),
        IsRunningValue(false),
        AcceptedSockets(std::make_shared<std::queue<std::shared_ptr<boost::asio::ip::tcp::socket>>>()),
        Sessions(std::make_shared<TSessionSet>()),
        IpSessions(std::make_shared<TIpAddressMap>()),
        StoppingSessions(std::make_shared<TSessionSet>()),
        BindingsValue(std::make_shared<std::vector<boost::asio::ip::tcp::endpoint>>()),
        SessionIdleTimeoutValue(Communication::BaseSystem::Optional<int>::CreateNotHasValue()),
        MaxConnectionsValue(Communication::BaseSystem::Optional<int>::CreateNotHasValue()),
        MaxConnectionsPerIPValue(Communication::BaseSystem::Optional<int>::CreateNotHasValue()),
        CheckCommandAllowedValue(nullptr),
        ShutdownValue(nullptr),
        MaxBadCommandsValue(8),
        ClientDebugValue(false),
        EnableLogNormalInValue(true),
        EnableLogNormalOutValue(true),
        EnableLogUnknownErrorValue(true),
        EnableLogCriticalErrorValue(true),
        EnableLogPerformanceValue(true),
        EnableLogSystemValue(true),
        SessionMappings(std::make_shared<TSessionMapping>())
    {
        sc = std::make_shared<ServerContext>();
        sc->Shutdown = [=]()
        {
            if (this->ShutdownValue != nullptr)
            {
                this->ShutdownValue();
            }
        };
        sc->GetSessions = [&]() -> std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>
        {
            return SessionMappings.Check<std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>>([&](const std::shared_ptr<TSessionMapping> &Mappings) -> std::shared_ptr<std::vector<std::shared_ptr<SessionContext>>>
            {
                auto l = std::make_shared<std::vector<std::shared_ptr<SessionContext>>>();
                for (auto i = Mappings->begin(); i != Mappings->end(); i.operator ++())
                {
                    l->push_back(std::get<0>(*i));
                }
                return l;
            });
        };
    }

    BinarySocketServer::~BinarySocketServer()
    {
        Stop();
    }

    bool BinarySocketServer::IsRunning()
    {
        return IsRunningValue.Check<bool>([](const bool &s) { return s; });
    }

    std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> BinarySocketServer::GetBindings() const
    {
        return BindingsValue;
    }
    void BinarySocketServer::SetBindings(std::shared_ptr<std::vector<boost::asio::ip::tcp::endpoint>> Bindings)
    {
        IsRunningValue.DoAction([&](bool &b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }
            BindingsValue = Bindings;
        });
    }

    std::shared_ptr<Communication::BaseSystem::Optional<int>> BinarySocketServer::GetSessionIdleTimeout() const
    {
        return SessionIdleTimeoutValue;
    }
    void BinarySocketServer::SetSessionIdleTimeout(std::shared_ptr<Communication::BaseSystem::Optional<int>> ms)
    {
        IsRunningValue.DoAction([&](bool &b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }
            SessionIdleTimeoutValue = ms;
        });
    }

    std::shared_ptr<Communication::BaseSystem::Optional<int>> BinarySocketServer::GetMaxConnections() const
    {
        return MaxConnectionsValue;
    }
    void BinarySocketServer::SetMaxConnections(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
    {
        IsRunningValue.DoAction([&](bool &b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }
            MaxConnectionsValue = v;
        });
    }

    std::shared_ptr<Communication::BaseSystem::Optional<int>> BinarySocketServer::GetMaxConnectionsPerIP() const
    {
        return MaxConnectionsPerIPValue;
    }
    void BinarySocketServer::SetMaxConnectionsPerIP(std::shared_ptr<Communication::BaseSystem::Optional<int>> v)
    {
        IsRunningValue.DoAction([&](bool &b)
        {
            if (b) { throw std::logic_error("InvalidOperationException"); }
            MaxConnectionsPerIPValue = v;
        });
    }

    void BinarySocketServer::DoAccepting()
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

                auto s = std::make_shared<BinarySocketSession>(IoService, this->shared_from_this(), Socket);
                s->RemoteEndPoint = Socket->remote_endpoint();
                s->IdleTimeout = SessionIdleTimeoutValue;

                if (MaxConnectionsValue->OnHasValue())
                {
                    int SessionCount = Sessions.Check<int>([=](std::shared_ptr<TSessionSet> ss) -> int
                    {
                        return (int)(ss->size());
                    });

                    if (SessionCount >= MaxConnectionsValue->HasValue)
                    {
                        PurifyOneInSession();
                    }

                    SessionCount = Sessions.Check<int>([=](std::shared_ptr<TSessionSet> ss) -> int
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
                        OnMaxConnectionsExceeded(s);
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
                        OnMaxConnectionsPerIPExceeded(s);
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

    bool BinarySocketServer::Purify(std::shared_ptr<BinarySocketSession> StoppingSession)
    {
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
        return Removed;
    }

    bool BinarySocketServer::PurifyOneInSession()
    {
        while (true)
        {
            std::shared_ptr<BinarySocketSession> StoppingSession = nullptr;
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
            auto Removed = Purify(StoppingSession);
            if (StoppingSession.use_count() != 1)
            {
                throw std::logic_error("InvalidOperationException");
            }
            if (Removed) { return true; }
        }
        return false;
    }

    void BinarySocketServer::DoPurifiering()
    {
        while (true)
        {
            if (PurifieringTaskToken.IsCancellationRequested()) { return; }
            PurifieringTaskNotifier.WaitOne();
            while (true)
            {
                std::shared_ptr<BinarySocketSession> StoppingSession = nullptr;
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
                Purify(StoppingSession);
                if (StoppingSession.use_count() != 1)
                {
                    throw std::logic_error("InvalidOperationException");
                }
            }
        }
    }

    bool BinarySocketServer::DoStopping(bool b)
    {
        if (!b) { return false; }

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

    void BinarySocketServer::Stop()
    {
        IsRunningValue.Update([&](bool b) -> bool
        {
            return DoStopping(b);
        });
    }

    void BinarySocketServer::Start()
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
                    bi->Listen(Binding);
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
                    throw std::logic_error((std::string("NoValidBinding: ") + std::string((*Exceptions)[0].what())));
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


    std::function<bool(std::shared_ptr<SessionContext>, std::wstring)> BinarySocketServer::GetCheckCommandAllowed() const
    {
        return CheckCommandAllowedValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetCheckCommandAllowed(std::function<bool(std::shared_ptr<SessionContext>, std::wstring)> value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        CheckCommandAllowedValue = value;
    }

    std::function<void()> BinarySocketServer::GetShutdown() const
    {
        return ShutdownValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetShutdown(std::function<void()> value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        ShutdownValue = value;
    }

    int BinarySocketServer::GetMaxBadCommands() const
    {
        return MaxBadCommandsValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetMaxBadCommands(int value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        MaxBadCommandsValue = value;
    }

    bool BinarySocketServer::GetClientDebug() const
    {
        return ClientDebugValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetClientDebug(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        ClientDebugValue = value;
    }

    bool BinarySocketServer::GetEnableLogNormalIn() const
    {
        return EnableLogNormalInValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogNormalIn(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogNormalInValue = value;
    }

    bool BinarySocketServer::GetEnableLogNormalOut() const
    {
        return EnableLogNormalOutValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogNormalOut(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogNormalOutValue = value;
    }

    bool BinarySocketServer::GetEnableLogUnknownError() const
    {
        return EnableLogUnknownErrorValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogUnknownError(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogUnknownErrorValue = value;
    }

    bool BinarySocketServer::GetEnableLogCriticalError() const
    {
        return EnableLogCriticalErrorValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogCriticalError(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogCriticalErrorValue = value;
    }

    bool BinarySocketServer::GetEnableLogPerformance() const
    {
        return EnableLogPerformanceValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogPerformance(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogPerformanceValue = value;
    }

    bool BinarySocketServer::GetEnableLogSystem() const
    {
        return EnableLogSystemValue;
    }
    /// <summary>只能在启动前修改，以保证线程安全</summary>
    void BinarySocketServer::SetEnableLogSystem(bool value)
    {
        if (IsRunning()) { throw std::logic_error("InvalidOperationException"); }
        EnableLogSystemValue = value;
    }

    void BinarySocketServer::RaiseSessionLog(std::shared_ptr<SessionLogEntry> Entry)
    {
        if (SessionLog != nullptr)
        {
            SessionLog(Entry);
        }
    }

    void BinarySocketServer::OnMaxConnectionsExceeded(std::shared_ptr<BinarySocketSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(L"", L"Client host rejected: too many connections, please try again later.");
        }
    }

    void BinarySocketServer::OnMaxConnectionsPerIPExceeded(std::shared_ptr<BinarySocketSession> s)
    {
        if (s != nullptr && s->IsRunning())
        {
            s->RaiseError(L"", L"Client host rejected: too many connections from your IP(" + s2w(s->RemoteEndPoint.address().to_string()) + L"), please try again later.");
        }
    }
}
