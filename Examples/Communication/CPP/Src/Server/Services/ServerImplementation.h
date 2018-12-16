#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "Context/ServerContext.h"
#include "Context/SessionContext.h"

#include <memory>
#include <string>

#undef SendMessage

namespace Server
{
    namespace Services
    {
        /// <summary>
        /// 本类的所有公共成员均是线程安全的。
        /// </summary>
        class ServerImplementation : public Communication::IApplicationServer, public IServerImplementation
        {
        private:
            std::shared_ptr<class ServerContext> ServerContext;
            std::shared_ptr<class SessionContext> SessionContext;
        public:
            ServerImplementation(std::shared_ptr<class ServerContext> sc, std::shared_ptr<class SessionContext> c)
            {
                this->ServerContext = sc;
                this->SessionContext = c;
                RegisterCrossSessionEvents();
            }
            ~ServerImplementation()
            {
                Stop();
            }
            void Stop()
            {
                UnregisterCrossSessionEvents();
            }

            void RegisterCrossSessionEvents()
            {
                auto Lock = SessionContext->WriterLock();
                SessionContext->MessageReceived = [=](std::shared_ptr<Communication::MessageReceivedEvent> e) { if (this->MessageReceived != nullptr) { this->MessageReceived(e); } };
                SessionContext->TestMessageReceived = [=](std::shared_ptr<Communication::TestMessageReceivedEvent> e) { if (this->TestMessageReceived != nullptr) { this->TestMessageReceived(e); } };
            }

            void UnregisterCrossSessionEvents()
            {
                auto Lock = SessionContext->WriterLock();
                SessionContext->MessageReceived = nullptr;
                SessionContext->TestMessageReceived = nullptr;
            }

            void RaiseError(std::wstring CommandName, std::wstring Message)
            {
                if (CommandName != L"")
                {
                    if (ErrorCommand != nullptr)
                    {
                        auto e = std::make_shared<Communication::ErrorCommandEvent>();
                        e->CommandName = CommandName;
                        ErrorCommand(e);
                    }
                    if (Error != nullptr)
                    {
                        auto e = std::make_shared<Communication::ErrorEvent>();
                        e->Message = CommandName + L": " + Message;
                        Error(e);
                    }
                }
                else
                {
                    if (Error != nullptr)
                    {
                        auto e = std::make_shared<Communication::ErrorEvent>();
                        e->Message = Message;
                        Error(e);
                    }
                }
            }

            /// <summary>关闭服务器</summary>
            std::shared_ptr<Communication::ShutdownReply> Shutdown(std::shared_ptr<Communication::ShutdownRequest> r) override;
            /// <summary>服务器时间</summary>
            std::shared_ptr<Communication::ServerTimeReply> ServerTime(std::shared_ptr<Communication::ServerTimeRequest> r) override;
            /// <summary>退出</summary>
            std::shared_ptr<Communication::QuitReply> Quit(std::shared_ptr<Communication::QuitRequest> r) override;
            /// <summary>检测类型结构版本</summary>
            std::shared_ptr<Communication::CheckSchemaVersionReply> CheckSchemaVersion(std::shared_ptr<Communication::CheckSchemaVersionRequest> r) override;
            /// <summary>发送消息</summary>
            std::shared_ptr<Communication::SendMessageReply> SendMessage(std::shared_ptr<Communication::SendMessageRequest> r) override;
            /// <summary>发送消息</summary>
            std::shared_ptr<Communication::SendMessageAt1Reply> SendMessageAt1(std::shared_ptr<Communication::SendMessageAt1Request> r) override;
            /// <summary>加法</summary>
            void TestAdd(std::shared_ptr<Communication::TestAddRequest> r, std::function<void(std::shared_ptr<Communication::TestAddReply>)> Callback, std::function<void(const std::exception &)> OnFailure) override;
            /// <summary>两百万次浮点乘法</summary>
            std::shared_ptr<Communication::TestMultiplyReply> TestMultiply(std::shared_ptr<Communication::TestMultiplyRequest> r) override;
            /// <summary>文本原样返回</summary>
            std::shared_ptr<Communication::TestTextReply> TestText(std::shared_ptr<Communication::TestTextRequest> r) override;
            /// <summary>群发消息</summary>
            std::shared_ptr<Communication::TestMessageReply> TestMessage(std::shared_ptr<Communication::TestMessageRequest> r) override;
            /// <summary>加法</summary>
            void TestAddAt1(std::shared_ptr<Communication::TestAddAt1Request> r, std::function<void(std::shared_ptr<Communication::TestAddAt1Reply>)> Callback, std::function<void(const std::exception &)> OnFailure) override;
            /// <summary>服务器时间</summary>
            std::shared_ptr<class CommunicationDuplication::ServerTimeReply> CommunicationDuplicationDotServerTime(std::shared_ptr<class CommunicationDuplication::ServerTimeRequest> r) override;
        };
    }
}
