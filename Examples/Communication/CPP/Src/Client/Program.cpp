//==========================================================================
//
//  File:        Program.cpp
//  Location:    Yuki.Examples <C++ 2011>
//  Description: 聊天客户端
//  Version:     2012.04.26.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "Utility.h"
#include "Communication.h"
#include "CommunicationBinary.h"
#include "ClientImplementation.h"
#include "Clients/BinarySocketClient.h"

#include <exception>
#include <stdexcept>
#include <string>
#include <iostream>
#include <boost/asio.hpp>
#include <boost/thread/thread.hpp>

#undef SendMessage

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
            wprintf(L"%ls\n", L"聊天客户端");
            wprintf(L"%ls\n", L"Author:      F.R.C.");
            wprintf(L"%ls\n", L"Copyright(C) Public Domain");
        }

        static void DisplayInfo()
        {
            wprintf(L"%ls\n", L"用法:");
            wprintf(L"%ls\n", L"Client [<IpAddress> <Port>]");
            wprintf(L"%ls\n", L"复制二进制数据");
            wprintf(L"%ls\n", L"IpAddress 服务器IP地址，默认为127.0.0.1");
            wprintf(L"%ls\n", L"Port 服务器端口，默认为8001");
        }

        static void Run(boost::asio::ip::tcp::endpoint RemoteEndPoint)
        {
            boost::asio::io_service IoService;
            auto ci = std::make_shared<ClientImplementation>();
            auto bsc = std::make_shared<BinarySocketClient>(IoService, RemoteEndPoint, ci);
            bsc->Connect();
            wprintf(L"%ls\n", L"连接成功。");
            bsc->Receive([](const boost::system::error_code &se) { wprintf(L"%s\n", se.message().c_str()); });
            
            boost::thread t([&]() { IoService.run(); });

            while (true)
            {
                std::wstring Line;
                std::getline(std::wcin, Line);
                if (Line == L"exit") { break; }
                auto Request = std::make_shared<Communication::SendMessageRequest>();
                Request->Content = Line;
                bsc->InnerClient->SendMessage(Request, [](ClientContext &c, std::shared_ptr<Communication::SendMessageReply> r)
                {
                    if (r->OnTooLong())
                    {
                        wprintf(L"%ls\n", L"消息过长。");
                    }
                });
            }

            bsc->Close();
            t.join();
        }
    };
}

int main(int argc, char **argv)
{
    setlocale(LC_ALL, "");

    try
    {
        return Client::Program::MainInner(argc, argv);
    }
    catch (std::exception &ex)
    {
        printf("Error:\n%s\n", ex.what());
        return -1;
    }
}
