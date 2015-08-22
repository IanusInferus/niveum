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
    namespace _Impl
    {
        using namespace Communication;

        /// <summary>
        /// 本类的所有公共成员均是线程安全的。
        /// </summary>
        class ServerImplementation : public IApplicationServer, public IServerImplementation
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
                SessionContext->MessageReceived = [=](std::shared_ptr<MessageReceivedEvent> e) { if (this->MessageReceived != nullptr) { this->MessageReceived(e); } };
                SessionContext->TestMessageReceived = [=](std::shared_ptr<TestMessageReceivedEvent> e) { if (this->TestMessageReceived != nullptr) { this->TestMessageReceived(e); } };
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
                        auto e = std::make_shared<ErrorCommandEvent>();
                        e->CommandName = CommandName;
                        ErrorCommand(e);
                    }
                    if (Error != nullptr)
                    {
                        auto e = std::make_shared<ErrorEvent>();
                        e->Message = CommandName + L": " + Message;
                        Error(e);
                    }
                }
                else
                {
                    if (Error != nullptr)
                    {
                        auto e = std::make_shared<ErrorEvent>();
                        e->Message = Message;
                        Error(e);
                    }
                }
            }

            /// <summary>关闭服务器</summary>
            std::shared_ptr<ShutdownReply> Shutdown(std::shared_ptr<ShutdownRequest> r);
            /// <summary>服务器时间</summary>
            std::shared_ptr<ServerTimeReply> ServerTime(std::shared_ptr<ServerTimeRequest> r);
            /// <summary>退出</summary>
            std::shared_ptr<QuitReply> Quit(std::shared_ptr<QuitRequest> r);
            /// <summary>检测类型结构版本</summary>
            std::shared_ptr<CheckSchemaVersionReply> CheckSchemaVersion(std::shared_ptr<CheckSchemaVersionRequest> r);
            /// <summary>发送消息</summary>
            std::shared_ptr<SendMessageReply> SendMessage(std::shared_ptr<SendMessageRequest> r);
            /// <summary>发送消息</summary>
            std::shared_ptr<SendMessageAt1Reply> SendMessageAt1(std::shared_ptr<SendMessageAt1Request> r);
            /// <summary>加法</summary>
            void TestAdd(std::shared_ptr<TestAddRequest> r, std::function<void(std::shared_ptr<TestAddReply>)> Callback, std::function<void(const std::exception &)> OnFailure);
            /// <summary>两百万次浮点乘法</summary>
            std::shared_ptr<TestMultiplyReply> TestMultiply(std::shared_ptr<TestMultiplyRequest> r);
            /// <summary>文本原样返回</summary>
            std::shared_ptr<TestTextReply> TestText(std::shared_ptr<TestTextRequest> r);
            /// <summary>群发消息</summary>
            std::shared_ptr<TestMessageReply> TestMessage(std::shared_ptr<TestMessageRequest> r);
            /// <summary>加法</summary>
            void TestAddAt1(std::shared_ptr<TestAddAt1Request> r, std::function<void(std::shared_ptr<TestAddAt1Reply>)> Callback, std::function<void(const std::exception &)> OnFailure);
        };
    }
    typedef _Impl::ServerImplementation ServerImplementation;
}
