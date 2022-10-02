//==========================================================================
//
//  File:        Program.cpp
//  Location:    Niveum.Examples <C++ 2011>
//  Description: 聊天服务器
//  Version:     2022.10.02.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "Services/ServerImplementation.h"
#include "Servers/TcpServer.h"
#include "Servers/UdpServer.h"
#include "Servers/BinaryCountPacketServer.h"

#include "BaseSystem/StringUtilities.h"
#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Optional.h"
#include "BaseSystem/ExceptionStackTrace.h"
#include "Util/ConsoleLogger.h"

#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <cwchar>
#include <thread>
#include <typeinfo>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif

namespace Server
{
    class Program
    {
    public:
        static int MainInner(int argc, char **argv)
        {
            DisplayTitle();

            if (argc == 2)
            {
                auto v = systemToWideChar(argv[1]);
                if (v == L"/?" || v == L"/help" || v == L"--help")
                {
                    DisplayInfo();
                    return 0;
                }
            }

            if (argc == 3)
            {
                Run(Parse<std::uint16_t>(systemToWideChar(argv[1])), !EqualIgnoreCase(systemToWideChar(argv[2]), L"/nolog"));
            }
            else if (argc == 2)
            {
                Run(Parse<std::uint16_t>(systemToWideChar(argv[1])), true);
            }
            else if (argc == 1)
            {
                Run(8001, true);
            }
            else
            {
                DisplayInfo();
                return -1;
            }
            return 0;
        }

        static void DisplayTitle()
        {
            std::wprintf(L"%ls\n", L"聊天服务器");
            std::wprintf(L"%ls\n", L"Author:      F.R.C.");
            std::wprintf(L"%ls\n", L"Copyright(C) Public Domain");
        }

        static void DisplayInfo()
        {
            std::wprintf(L"%ls\n", L"用法:");
            std::wprintf(L"%ls\n", L"Server [<Port> [/nolog]]");
            std::wprintf(L"%ls\n", L"Port 服务器端口，默认为8001");
            std::wprintf(L"%ls\n", L"/nolog 表示不显示日志");
        }

        static void Run(std::uint16_t Port, bool EnableLogConsole)
        {
            auto ExitEvent = std::make_shared<BaseSystem::AutoResetEvent>();

            int ProcessorCount = (int)(std::thread::hardware_concurrency());

            std::wprintf(L"%ls\n", (L"逻辑处理器数量: " + ToString(ProcessorCount)).c_str());

            auto IoServiceLogNumThread = 1;
            auto IoServicePurifierNumThread = 2;
            auto IoServiceNumThread = ProcessorCount * 2 + 1;

            auto IoServiceLog = std::make_shared<asio::io_service>(IoServiceLogNumThread);
            auto IoServicePurifier = std::make_shared<asio::io_service>(IoServicePurifierNumThread);
            auto IoService = std::make_shared<asio::io_service>(IoServiceNumThread);
            asio::io_service::work WorkLog(*IoServiceLog);
            asio::io_service::work WorkPurifier(*IoServicePurifier);
            asio::io_service::work Work(*IoService);

            ConsoleLogger cl;

            auto ServerContext = std::make_shared<class ServerContext>();
            ServerContext->EnableLogNormalIn(true);
            ServerContext->EnableLogNormalOut(true);
            ServerContext->EnableLogUnknownError(true);
            ServerContext->EnableLogCriticalError(true);
            ServerContext->EnableLogPerformance(true);
            ServerContext->EnableLogSystem(true);
            ServerContext->ServerDebug(true);
            ServerContext->ClientDebug(true);

            ServerContext->Shutdown = [&]()
            {
                ExitEvent->Set();
            };

            if (EnableLogConsole)
            {
                ServerContext->SessionLog = [&](std::shared_ptr<SessionLogEntry> e)
                {
                    IoServiceLog->post([e, &cl]() { cl.Log(e); });
                };
            }

            auto VirtualTransportServerFactory = [=](std::shared_ptr<ISessionContext> Context, std::shared_ptr<IBinaryTransformer> t) -> std::pair<std::shared_ptr<IServerImplementation>, std::shared_ptr<IStreamedVirtualTransportServer>>
            {
                auto p = ServerContext->CreateServerImplementationWithBinaryAdapter(Context);
                auto si = std::get<0>(p);
                auto a = std::get<1>(p);
                auto CheckCommandAllowed = [&](std::u16string CommandName)
                {
                    return true;
                };
                auto bcps = std::make_shared<BinaryCountPacketServer>(a, CheckCommandAllowed, t);
                return std::make_pair(si, bcps);
            };

            auto Servers = std::make_shared<std::vector<std::shared_ptr<IServer>>>();

            {
                auto Server = std::make_shared<TcpServer>(*IoService, ServerContext, VirtualTransportServerFactory, [=](std::function<void()> a) { IoService->post(a); }, [=](std::function<void()> a) { IoServicePurifier->post(a); });

                auto Bindings = std::make_shared<std::vector<asio::ip::tcp::endpoint>>();
                auto LocalEndPoint = asio::ip::tcp::endpoint(asio::ip::tcp::v4(), Port);
                Bindings->push_back(LocalEndPoint);
                Server->Bindings(Bindings);
                Server->SessionIdleTimeout(120);
                Server->UnauthenticatedSessionIdleTimeout(30);
                Server->MaxConnections(32768);
                Server->MaxConnectionsPerIP(32768);
                Server->MaxUnauthenticatedPerIP(32768);
                Server->MaxBadCommands(8);
                Server->TimeoutCheckPeriod(30);

                Server->Start();

                std::wprintf(L"%ls\n", (L"TCP/Binary服务器已启动。结点: " + systemToWideChar(LocalEndPoint.address().to_string()) + L":" + ToString(LocalEndPoint.port()) + L"(TCP)").c_str());

                Servers->push_back(Server);
            }

            {
                auto Server = std::make_shared<UdpServer>(*IoService, ServerContext, VirtualTransportServerFactory, [=](std::function<void()> a) { IoService->post(a); }, [=](std::function<void()> a) { IoServicePurifier->post(a); });

                auto Bindings = std::make_shared<std::vector<asio::ip::udp::endpoint>>();
                auto LocalEndPoint = asio::ip::udp::endpoint(asio::ip::udp::v4(), Port);
                Bindings->push_back(LocalEndPoint);
                Server->Bindings(Bindings);
                Server->SessionIdleTimeout(120);
                Server->UnauthenticatedSessionIdleTimeout(30);
                Server->MaxConnections(32768);
                Server->MaxConnectionsPerIP(32768);
                Server->MaxUnauthenticatedPerIP(32768);
                Server->MaxBadCommands(8);
                Server->TimeoutCheckPeriod(30);

                Server->Start();

                std::wprintf(L"%ls\n", (L"UDP/Binary服务器已启动。结点: " + systemToWideChar(LocalEndPoint.address().to_string()) + L":" + ToString(LocalEndPoint.port()) + L"(UDP)").c_str());

                Servers->push_back(Server);
            }

            std::vector<std::shared_ptr<std::thread>> Threads;
            for (int i = 0; i < IoServiceLogNumThread; i += 1)
            {
                auto t = std::make_shared<std::thread>([&]()
                {
                    IoServiceLog->run();
                });
                Threads.push_back(t);
            }
            for (int i = 0; i < IoServicePurifierNumThread; i += 1)
            {
                auto t = std::make_shared<std::thread>([&]()
                {
                    IoServicePurifier->run();
                });
                Threads.push_back(t);
            }
            for (int i = 0; i < IoServiceNumThread; i += 1)
            {
                auto t = std::make_shared<std::thread>([&]()
                {
                    IoService->run();
                });
                Threads.push_back(t);
            }

            ExitEvent->WaitOne();

            for (auto Server : *Servers)
            {
                Server->Stop();
            }
            Servers->clear();

            IoService->stop();
            IoServicePurifier->stop();
            IoServiceLog->stop();

            ServerContext->SessionLog = nullptr;
            ServerContext = nullptr;

            for (int i = 0; i < (int)(Threads.size()); i += 1)
            {
                auto t = Threads[i];
                t->join();
            }

            std::wprintf(L"%ls\n", L"服务器已关闭。");
        }
    };
}

#ifdef _MSC_VER

#include <io.h>
#include <fcntl.h>

void ModifyStdoutUnicode()
{
    _setmode(_fileno(stdout), _O_U16TEXT);
}

#else

void ModifyStdoutUnicode()
{
}

#endif

#include <locale.h>

void SetLocale()
{
    setlocale(LC_ALL, "");
}

int main(int argc, char **argv)
{
    ModifyStdoutUnicode();
    SetLocale();

    if (ExceptionStackTrace::IsDebuggerAttached())
    {
        return Server::Program::MainInner(argc, argv);
    }
    else
    {
        try
        {
            return ExceptionStackTrace::Execute([=]() { return Server::Program::MainInner(argc, argv); });
        }
        catch (const std::exception &ex)
        {
            auto Message = std::string() + typeid(*(&ex)).name() + "\r\n" + ex.what() + "\r\n" + ExceptionStackTrace::GetStackTrace();
            std::wprintf(L"Error:\n%ls\n", systemToWideChar(Message).c_str());
            return -1;
        }
        catch (const char *ex)
        {
            auto Message = std::string() + ex + "\r\n" + ExceptionStackTrace::GetStackTrace();
            std::wprintf(L"Error:\n%ls\n", systemToWideChar(Message).c_str());
            return -1;
        }
        catch (...)
        {
            // 在Visual C++下需要指定/EHa才能捕捉到SEH异常
            auto Message = std::string() + "SEHException\r\n" + ExceptionStackTrace::GetStackTrace();
            std::wprintf(L"Error:\n%ls\n", systemToWideChar(Message).c_str());
            return -1;
        }
    }
}
