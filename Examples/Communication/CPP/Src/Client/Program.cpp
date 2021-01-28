//==========================================================================
//
//  File:        Program.cpp
//  Location:    Niveum.Examples <C++ 2011>
//  Description: 聊天客户端
//  Version:     2020.07.22.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#include "BaseSystem/StringUtilities.h"
#include "BaseSystem/ExceptionStackTrace.h"
#include "Generated/Communication.h"
#include "Generated/CommunicationBinary.h"
#include "Clients/BinaryCountPacketClient.h"
#include "Clients/TcpClient.h"
#include "Clients/UdpClient.h"
#include "Clients/Rc4PacketClientTransformer.h"
#include "Context/SerializationClientAdapter.h"

#include <exception>
#include <stdexcept>
#include <string>
#include <cwchar>
#include <iostream>
#include <algorithm>
#include <chrono>
#include <thread>
#include <typeinfo>
#include <asio.hpp>
#ifdef _MSC_VER
#undef SendMessage
#endif
#include <fmt/format.h>

namespace Client
{
    class Program
    {
    public:
        static int MainInner(int argc, char **argv)
        {
            DisplayTitle();

            if (argc == 5 && (std::string(argv[4]) == "/test"))
            {
                auto TransportProtocol = std::string(argv[1]);
                std::transform(TransportProtocol.begin(), TransportProtocol.end(), TransportProtocol.begin(), ::tolower);

                asio::io_service IoService(16);
                if (TransportProtocol == "tcp")
                {
                    asio::ip::tcp::endpoint RemoteEndPoint(asio::ip::address::from_string(argv[2]), Parse<uint16_t>(systemToUtf16(argv[3])));
                    for (int k = 0; k < 2048; k += 1)
                    {
                        IoService.post([=, &IoService]() { RunTcp(IoService, RemoteEndPoint, Test); });
                    }
                }
                else if (TransportProtocol == "udp")
                {
                    asio::ip::udp::endpoint RemoteEndPoint(asio::ip::address::from_string(argv[2]), Parse<uint16_t>(systemToUtf16(argv[3])));
                    for (int k = 0; k < 2048; k += 1)
                    {
                        IoService.post([=, &IoService]() { RunUdp(IoService, RemoteEndPoint, Test); });
                    }
                }
                std::vector<std::shared_ptr<std::thread>> Threads;
                for (int k = 0; k < 16; k += 1)
                {
                    Threads.push_back(std::make_shared<std::thread>([&]()
                    {
                        auto Exit = false;
                        while (true)
                        {
                            try
                            {
                                IoService.run();
                                Exit = true;
                            }
                            catch (const std::exception &)
                            {
                            }
                            if (Exit) { break; }
                        }
                    }));
                }
                for (int k = 0; k < 2048; k += 1)
                {
                    IoService.post([=]()
                    {
                        auto a = new unsigned char[10000];
                        delete[] a;
                    });
                }
                for (auto t : Threads)
                {
                    t->join();
                }
            }
            else if (argc == 4)
            {
                auto TransportProtocol = std::string(argv[1]);
                std::transform(TransportProtocol.begin(), TransportProtocol.end(), TransportProtocol.begin(), ::tolower);

                asio::io_service IoService;
                auto Work = std::make_shared<asio::io_service::work>(IoService);
                std::thread t([&]()
                {
                    auto Exit = false;
                    while (true)
                    {
                        try
                        {
                            IoService.run();
                            Exit = true;
                        }
                        catch (const std::exception &)
                        {
                        }
                        if (Exit) { break; }
                    }
                });
                if (TransportProtocol == "tcp")
                {
                    asio::ip::tcp::endpoint RemoteEndPoint(asio::ip::address::from_string(argv[2]), Parse<uint16_t>(systemToUtf16(argv[3])));
                    RunTcp(IoService, RemoteEndPoint, ReadLineAndSendLoop);
                }
                else if (TransportProtocol == "udp")
                {
                    asio::ip::udp::endpoint RemoteEndPoint(asio::ip::address::from_string(argv[2]), Parse<uint16_t>(systemToUtf16(argv[3])));
                    RunUdp(IoService, RemoteEndPoint, ReadLineAndSendLoop);
                }
                Work = nullptr;
                t.join();
            }
            else if (argc == 1)
            {
                asio::io_service IoService;
                auto Work = std::make_shared<asio::io_service::work>(IoService);
                std::thread t([&]()
                {
                    auto Exit = false;
                    while (true)
                    {
                        try
                        {
                            IoService.run();
                            Exit = true;
                        }
                        catch (const std::exception &)
                        {
                        }
                        if (Exit) { break; }
                    }
                });
                asio::ip::tcp::endpoint RemoteEndPoint(asio::ip::address::from_string("127.0.0.1"), 8001);
                RunTcp(IoService, RemoteEndPoint, ReadLineAndSendLoop);
                Work = nullptr;
                t.join();
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
            std::wprintf(L"%ls\n", L"Client [<TransportProtocol> <IpAddress> <Port>]");
            std::wprintf(L"%ls\n", L"Client <TransportProtocol> <IpAddress> <Port> /test");
            std::wprintf(L"%ls\n", L"TransportProtocol 传输协议，可为Tcp和Udp，默认为Tcp");
            std::wprintf(L"%ls\n", L"IpAddress 服务器IP地址，默认为127.0.0.1");
            std::wprintf(L"%ls\n", L"Port 服务器端口，默认为8001");
        }

        static void ReadLineAndSendLoop(std::shared_ptr<Communication::IApplicationClient> InnerClient, std::function<void(std::shared_ptr<SecureContext>)> SetSecureContext, std::mutex &Lockee)
        {
            std::wprintf(L"%ls\n", L"输入login登录，输入secure启用安全连接。");

            InnerClient->GlobalErrorHandler = [=](std::u16string CommandName, std::u16string Message)
            {
                wprintf(L"%ls: %ls\n", utf16ToWideChar(CommandName).c_str(), utf16ToWideChar(Message).c_str());
            };
            InnerClient->Error = [=](std::shared_ptr<Communication::ErrorEvent> e)
            {
                auto m = e->Message;
                wprintf(L"%ls\n", utf16ToWideChar(m).c_str());
            };
            InnerClient->MessageReceived = [=](std::shared_ptr<Communication::MessageReceivedEvent> e)
            {
                wprintf(L"%ls\n", utf16ToWideChar(e->Content).c_str());
            };

            auto csvr = std::make_shared<Communication::CheckSchemaVersionRequest>();
            csvr->Hash = ReplaceAllCopy(ToHexU16String(InnerClient->Hash()), u" ", u"");
            InnerClient->CheckSchemaVersion(csvr, [](std::shared_ptr<Communication::CheckSchemaVersionReply> r)
            {
                if (r->OnHead())
                {
                }
                else if (r->OnSupported())
                {
                    std::wprintf(L"%ls\n", L"客户端不是最新版本，但服务器可以支持。");
                }
                else if (r->OnNotSupported())
                {
                    std::wprintf(L"%ls\n", L"客户端版本不受支持。");
                    exit(-1);
                }
            });

            while (true)
            {
                std::wstring Line;
                std::getline(std::wcin, Line);

                {
                    std::unique_lock<std::mutex> Lock(Lockee);
                    if (Line == L"exit")
                    {
                        InnerClient->Quit(std::make_shared<Communication::QuitRequest>(), [](std::shared_ptr<Communication::QuitReply> r)
                        {
                        });
                        break;
                    }
                    if (Line == L"secure")
                    {
                        auto RequestSecure = std::make_shared<Communication::SendMessageRequest>();
                        RequestSecure->Content = wideCharToUtf16(Line);
                        InnerClient->SendMessage(RequestSecure, [=](std::shared_ptr<Communication::SendMessageReply> r)
                        {
                            //生成测试用确定Key
                            auto sc = std::make_shared<SecureContext>();
                            for (int i = 0; i < 41; i += 1)
                            {
                                sc->ServerToken.push_back(static_cast<std::uint8_t>(i));
                            }
                            for (int i = 0; i < 41; i += 1)
                            {
                                sc->ClientToken.push_back(static_cast<std::uint8_t>(40 - i));
                            }
                            SetSecureContext(sc);
                        });
                        continue;
                    }
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
                    Request->Content = wideCharToUtf16(Line);
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

        static void Test(std::shared_ptr<Communication::IApplicationClient> InnerClient, std::function<void(std::shared_ptr<SecureContext>)> SetSecureContext, std::mutex &Lockee)
        {
            {
                std::unique_lock<std::mutex> Lock(Lockee);
                InnerClient->Error = [=](std::shared_ptr<Communication::ErrorEvent> e)
                {
                    auto m = e->Message;
                    wprintf(L"%ls\n", utf16ToWideChar(m).c_str());
                };
            }

            std::wstring Line;
            Line.reserve(2048);
            for (int k = 0; k < 512; k += 1)
            {
                Line += L"TEST";
            }

            {
                std::unique_lock<std::mutex> Lock(Lockee);
                InnerClient->ServerTime(std::make_shared<Communication::ServerTimeRequest>(), [=](std::shared_ptr<Communication::ServerTimeReply> r2)
                {
                    auto Request = std::make_shared<Communication::SendMessageRequest>();
                    Request->Content = wideCharToUtf16(Line);
                    InnerClient->SendMessage(Request, [](std::shared_ptr<Communication::SendMessageReply> r)
                    {
                        if (r->OnTooLong())
                        {
                        }
                        else
                        {
                            std::wprintf(L"%ls\n", L"出错。");
                        }
                    });
                });
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(1000));
        }

        static void RunTcp(asio::io_service &IoService, asio::ip::tcp::endpoint RemoteEndPoint, std::function<void(std::shared_ptr<Communication::IApplicationClient>, std::function<void(std::shared_ptr<SecureContext>)>, std::mutex &)> Action)
        {
            auto bsca = std::make_shared<Client::BinarySerializationClientAdapter>(IoService, 30 * 1000);
            bsca->ClientCommandReceived = [=](std::u16string CommandName, int Milliseconds)
            {
                //std::wprintf(L"%ls\n", fmt::format(L"{0} {1}ms", utf16ToWideChar(CommandName), Milliseconds).c_str());
            };
            bsca->ClientCommandFailed = [=](std::u16string CommandName, int Milliseconds)
            {
                //std::wprintf(L"%ls\n", fmt::format(L"{0} Failed {1}ms", utf16ToWideChar(CommandName), Milliseconds).c_str());
            };
            bsca->ServerCommandReceived = [=](std::u16string CommandName)
            {
                //std::wprintf(L"%ls\n", utf16ToWideChar(CommandName).c_str());
            };

            auto bt = std::make_shared<Rc4PacketClientTransformer>();
            auto ac = bsca->GetApplicationClient();
            auto vtc = std::make_shared<BinaryCountPacketClient>(bsca, bt, 128 * 1024);
            auto bc = std::make_shared<TcpClient>(IoService, RemoteEndPoint, vtc);
            try
            {
                bc->Connect();
            }
            catch (std::exception &e)
            {
                auto Message = systemToWideChar(e.what());
                wprintf(L"%ls\n", Message.c_str());
                exit(-1);
            }

            std::mutex Lockee;
            auto DoHandle = [&](std::function<void(void)> a)
            {
                IoService.post([&, a]()
                {
                    std::unique_lock<std::mutex> Lock(Lockee);
                    a();
                });
            };
            bc->ReceiveAsync(DoHandle, [](const std::u16string &Message)
            {
                wprintf(L"%ls\n", utf16ToWideChar(Message).c_str());
                exit(-1);
            });

            auto SetSecureContext = [=](std::shared_ptr<SecureContext> c)
            {
                bt->SetSecureContext(c);
            };

            Action(ac, SetSecureContext, Lockee);

            bc->Close();
            bc = nullptr;
            vtc = nullptr;
            ac = nullptr;
            bsca = nullptr;
        }

        static void RunUdp(asio::io_service &IoService, asio::ip::udp::endpoint RemoteEndPoint, std::function<void(std::shared_ptr<Communication::IApplicationClient>, std::function<void(std::shared_ptr<SecureContext>)>, std::mutex &)> Action)
        {
            auto bsca = std::make_shared<Client::BinarySerializationClientAdapter>(IoService, 30 * 1000);
            bsca->ClientCommandReceived = [=](std::u16string CommandName, int Milliseconds)
            {
                //std::wprintf(L"%ls\n", fmt::format(L"{0} {1}ms", utf16ToWideChar(CommandName), Milliseconds).c_str());
            };
            bsca->ClientCommandFailed = [=](std::u16string CommandName, int Milliseconds)
            {
                //std::wprintf(L"%ls\n", fmt::format(L"{0} Failed {1}ms", utf16ToWideChar(CommandName), Milliseconds).c_str());
            };
            bsca->ServerCommandReceived = [=](std::u16string CommandName)
            {
                //std::wprintf(L"%ls\n", utf16ToWideChar(CommandName).c_str());
            };

            auto bt = std::make_shared<Rc4PacketClientTransformer>();
            auto ac = bsca->GetApplicationClient();
            auto vtc = std::make_shared<BinaryCountPacketClient>(bsca, bt, 128 * 1024);
            auto bc = std::make_shared<UdpClient>(IoService, RemoteEndPoint, vtc);
            try
            {
                bc->Connect();
            }
            catch (std::exception &e)
            {
                auto Message = systemToWideChar(e.what());
                wprintf(L"%ls\n", Message.c_str());
                exit(-1);
            }

            std::mutex Lockee;
            auto DoHandle = [&](std::function<void(void)> a)
            {
                IoService.post([&, a]()
                {
                    std::unique_lock<std::mutex> Lock(Lockee);
                    a();
                });
            };
            bc->ReceiveAsync(DoHandle, [](const std::u16string &Message)
            {
                wprintf(L"%ls\n", utf16ToWideChar(Message).c_str());
                exit(-1);
            });

            auto SetSecureContext = [=](std::shared_ptr<SecureContext> c)
            {
                bt->SetSecureContext(c);
                bc->SecureContext(c);
            };

            Action(ac, SetSecureContext, Lockee);

            {
                std::unique_lock<std::mutex> Lock(Lockee);
                bc->Close();
                bc = nullptr;
                vtc = nullptr;
                ac = nullptr;
                bsca = nullptr;
            }
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
        return Client::Program::MainInner(argc, argv);
    }
    else
    {
        try
        {
            return ExceptionStackTrace::Execute([=]() { return Client::Program::MainInner(argc, argv); });
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
