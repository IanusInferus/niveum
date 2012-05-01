//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 聊天服务器
//  Version:     2012.05.01.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "Utility.h"
#include "Communication.h"
#include "CommunicationBinary.h"
#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Optional.h"
#include "Services/ServerImplementation.h"
#include "Servers/BinarySocketSession.h"
#include "Servers/BinarySocketServer.h"

#include <vector>
#include <string>
#include <exception>
#include <stdexcept>
#include <iostream>
#include <boost/asio.hpp>
#include <boost/thread.hpp>
#include <boost/format.hpp>
#include <boost/date_time/posix_time/posix_time.hpp>

#undef SendMessage

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
                Run(Parse<std::uint16_t>(s2w(argv[1])));
            }
            else if (argc == 1)
            {
                Run(8001);
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
            wprintf(L"%ls\n", L"聊天服务器");
            wprintf(L"%ls\n", L"Author:      F.R.C.");
            wprintf(L"%ls\n", L"Copyright(C) Public Domain");
        }

        static void DisplayInfo()
        {
            wprintf(L"%ls\n", L"用法:");
            wprintf(L"%ls\n", L"Server [<Port>]");
            wprintf(L"%ls\n", L"Port 服务器端口，默认为8001");
        }

        static void Run(std::uint16_t Port)
        {
            auto ExitEvent = std::make_shared<Communication::BaseSystem::AutoResetEvent>();

            int ProcessorCount = (int)(boost::thread::hardware_concurrency());

            auto IoService = std::make_shared<boost::asio::io_service>(ProcessorCount);

            boost::asio::signal_set Signals(*IoService, SIGINT, SIGTERM);
            Signals.async_wait([=](const boost::system::error_code& error, int signal_number)
            {
                if (error == boost::system::errc::success)
                {
                    IoService->stop();
                    ExitEvent->Set();
                }
            });

            auto Server = std::make_shared<BinarySocketServer>(*IoService);
            auto Bindings = std::make_shared<std::vector<boost::asio::ip::tcp::endpoint>>();
            Bindings->push_back(boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v4(), Port));
            Bindings->push_back(boost::asio::ip::tcp::endpoint(boost::asio::ip::tcp::v6(), Port));
            Server->SetBindings(Bindings);
            Server->SetSessionIdleTimeout(Communication::BaseSystem::Optional<int>::CreateHasValue(10 * 1000));

            Server->SetMaxBadCommands(8);
            Server->SetClientDebug(true);

            Server->SessionLog = [=](std::shared_ptr<SessionLogEntry> e) { ConsoleLog(e); };

            Server->Start();

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

            Server->SessionLog = nullptr;

            for (int i = 0; i < (int)(Threads.size()); i += 1)
            {
                auto t = Threads[i];
                t->join();
            }
        }

        static void ConsoleLog(std::shared_ptr<SessionLogEntry> e)
        {
            auto Line = L"\"" + boost::posix_time::to_iso_extended_wstring(e->Time) + L"Z" + L"\"" + L"\t" + L"\"" + e->Type + L"\"" + L"\t" + L"\"" + e->Message + L"\"";
            wprintf(L"%ls\n", Line.c_str());
        }
    };
}

int main(int argc, char **argv)
{
    setlocale(LC_ALL, "");

    try
    {
        return Server::Program::MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        printf("Error:\n%s\n", ex.what());
        return -1;
    }
}
