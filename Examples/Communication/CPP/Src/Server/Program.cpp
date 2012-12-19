//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 聊天服务器
//  Version:     2012.06.19.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "Services/ServerImplementation.h"
#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include "Utility.h"
#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Optional.h"
#include "Util/ConsoleLogger.h"

#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <cstdio>
#include <clocale>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/thread.hpp>
#include <boost/format.hpp>
#include <boost/date_time/posix_time/posix_time.hpp>

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
                auto v = s2w(argv[1]);
                if (v == L"/?" || v == L"/help" || v == L"--help")
                {
                    DisplayInfo();
                    return 0;
                }
            }

            if (argc == 3)
            {
                Run(Parse<std::uint16_t>(s2w(argv[1])), !EqualIgnoreCase(s2w(argv[2]), L"/nolog"));
            }
            else if (argc == 2)
            {
                Run(Parse<std::uint16_t>(s2w(argv[1])), true);
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
            auto ExitEvent = std::make_shared<Communication::BaseSystem::AutoResetEvent>();

            int ProcessorCount = (int)(boost::thread::hardware_concurrency());

            std::wprintf(L"%ls\n", (L"逻辑处理器数量: " + ToString(ProcessorCount)).c_str());

            auto IoService = std::make_shared<boost::asio::io_service>(ProcessorCount * 2 + 1);
            boost::asio::io_service::work Work(*IoService);

            ConsoleLogger cl;

            auto Server = std::make_shared<BinarySocketServer>(*IoService);

            Server->SetCheckCommandAllowed([&](std::shared_ptr<SessionContext> sc, std::wstring CommandName)
            {
                return true;
            });

            Server->SetShutdown([&]()
            {
                ExitEvent->Set();
            });

            auto Bindings = std::make_shared<std::vector<boost::asio::ip::tcp::endpoint>>();
            auto LocalEndPoint = boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), Port);
            Bindings->push_back(LocalEndPoint);
            Server->SetBindings(Bindings);
            Server->SetSessionIdleTimeout(Communication::BaseSystem::Optional<int>::CreateHasValue(600 * 1000));

            Server->SetMaxBadCommands(8);
            Server->SetClientDebug(true);

            if (EnableLogConsole)
            {
                Server->SessionLog = [&](std::shared_ptr<SessionLogEntry> e)
                {
                    cl.Log(e);
                };
            }

            Server->Start();

            std::wprintf(L"%ls\n", L"服务器已启动。");
            std::wprintf(L"%ls\n", L"协议类型: Binary");
            std::wprintf(L"%ls\n", (L"服务结点: " +  s2w(LocalEndPoint.address().to_string()) + L":" + ToString(LocalEndPoint.port())).c_str());

            std::vector<std::shared_ptr<boost::thread>> Threads;
            for (int i = 0; i < ProcessorCount; i += 1)
            {
                auto t = std::make_shared<boost::thread>([&]()
                {
                    IoService->run();
                });
                Threads.push_back(t);
            }

            ExitEvent->WaitOne();

            Server->Stop();

            IoService->stop();

            Server->SessionLog = nullptr;

            for (int i = 0; i < (int)(Threads.size()); i += 1)
            {
                auto t = Threads[i];
                t->join();
            }

            std::wprintf(L"%ls\n", L"服务器已关闭。");
        }
    };
}

int main(int argc, char **argv)
{
    std::setlocale(LC_ALL, "");

    try
    {
        return Server::Program::MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        std::wprintf(L"Error:\n%ls\n", s2w(ex.what()).c_str());
        return -1;
    }
}
