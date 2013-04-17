//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 聊天客户端
//  Version:     2013.04.17.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "Utility.h"
#include "Communication.h"
#include "UtfEncoding.h"
#include "CommunicationBinary.h"
#include "Clients/BinarySocketClient.h"

#include <exception>
#include <stdexcept>
#include <string>
#include <cstdio>
#include <clocale>
#include <iostream>
#include <boost/asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <boost/thread.hpp>

namespace Client
{
    class Program
    {
    public:
        static int MainInner(int argc, char **argv)
        {
            DisplayTitle();

            if (argc == 3)
            {
                boost::asio::ip::tcp::endpoint RemoteEndPoint(boost::asio::ip::address::from_string(argv[1]), Parse<uint16_t>(s2w(argv[2])));
                Run(RemoteEndPoint);
            }
            else if (argc == 1)
            {
                boost::asio::ip::tcp::endpoint RemoteEndPoint(boost::asio::ip::address::from_string("127.0.0.1"), 8001);
                Run(RemoteEndPoint);
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
            std::wprintf(L"%ls\n", L"聊天客户端");
            std::wprintf(L"%ls\n", L"Author:      F.R.C.");
            std::wprintf(L"%ls\n", L"Copyright(C) Public Domain");
        }

        static void DisplayInfo()
        {
            std::wprintf(L"%ls\n", L"用法:");
            std::wprintf(L"%ls\n", L"Client [<IpAddress> <Port>]");
            std::wprintf(L"%ls\n", L"复制二进制数据");
            std::wprintf(L"%ls\n", L"IpAddress 服务器IP地址，默认为127.0.0.1");
            std::wprintf(L"%ls\n", L"Port 服务器端口，默认为8001");
        }

        static void ReadLineAndSendLoop(std::shared_ptr<Communication::IApplicationClient> InnerClient, boost::mutex &Lockee)
        {
            InnerClient->Error = [=](std::shared_ptr<Communication::ErrorEvent> e)
            {
                auto m = e->Message;
                wprintf(L"%ls\n", m.c_str());
            };
            InnerClient->MessageReceived = [=](std::shared_ptr<Communication::MessageReceivedEvent> e)
            {
                wprintf(L"%ls\n", e->Content.c_str());
            };

            while (true)
            {
                std::wstring Line;
                std::getline(std::wcin, Line);

                {
                    boost::unique_lock<boost::mutex> Lock(Lockee);
                    if (Line == L"exit") { break; }
                    if (Line == L"shutdown")
                    {
                        InnerClient->Shutdown(std::make_shared<Communication::ShutdownRequest>(), [](std::shared_ptr<Communication::ShutdownReply> r)
                        {
                            if (r->OnSuccess())
                            {
                                std::wprintf(L"%ls\n", L"服务器关闭。");
                            }
                        });
                        break;
                    }
                    auto Request = std::make_shared<Communication::SendMessageRequest>();
                    Request->Content = Line;
                    InnerClient->SendMessage(Request, [](std::shared_ptr<Communication::SendMessageReply> r)
                    {
                        if (r->OnTooLong())
                        {
                            std::wprintf(L"%ls\n", L"消息过长。");
                        }
                    });
                }
            }
        }

        static void Run(boost::asio::ip::tcp::endpoint RemoteEndPoint)
        {
            boost::asio::io_service IoService;
            auto bsc = std::make_shared<BinarySocketClient>(IoService);
            bsc->Connect(RemoteEndPoint);
            std::wprintf(L"%ls\n", L"连接成功。");

            boost::mutex Lockee;
            auto DoHandle = [&](std::function<void(void)> a)
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                a();
            };
            bsc->ReceiveAsync(DoHandle, [](const boost::system::error_code &se) { wprintf(L"%s\n", se.message().c_str()); });
            
            boost::thread t([&]() { IoService.run(); });

            ReadLineAndSendLoop(bsc->InnerClient, Lockee);

            bsc->Close();
            t.join();
        }
    };
}

int main(int argc, char **argv)
{
    std::setlocale(LC_ALL, "");

    try
    {
        return Client::Program::MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        std::wprintf(L"Error:\n%ls\n", s2w(ex.what()).c_str());
        return -1;
    }
}
