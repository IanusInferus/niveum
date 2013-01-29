#pragma once

#include "Communication.h"
#include "UtfEncoding.h"
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
        class ServerImplementation : public IApplicationServer
        {
        private:
            std::shared_ptr<ServerContext> sc;
            std::shared_ptr<SessionContext> c;
        public:
            ServerImplementation(std::shared_ptr<ServerContext> sc, std::shared_ptr<SessionContext> c)
            {
                this->sc = sc;
                this->c = c;
            }

            void RegisterCrossSessionEvents()
            {
                boost::unique_lock<boost::shared_mutex> WriterLock(c->SessionLock);
                c->MessageReceived = [=](std::shared_ptr<MessageReceivedEvent> e) { if (this->MessageReceived != nullptr) { this->MessageReceived(e); } };
                c->TestMessageReceived = [=](std::shared_ptr<TestMessageReceivedEvent> e) { if (this->TestMessageReceived != nullptr) { this->TestMessageReceived(e); } };
            }

            void UnregisterCrossSessionEvents()
            {
                boost::unique_lock<boost::shared_mutex> WriterLock(c->SessionLock);
                c->MessageReceived = nullptr;
                c->TestMessageReceived = nullptr;
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
            /// <summary>发送消息</summary>
            std::shared_ptr<SendMessageReply> SendMessage(std::shared_ptr<SendMessageRequest> r);
            /// <summary>发送消息</summary>
            std::shared_ptr<SendMessageAt1Reply> SendMessageAt1(std::shared_ptr<SendMessageAt1Request> r);
            /// <summary>加法</summary>
            std::shared_ptr<TestAddReply> TestAdd(std::shared_ptr<TestAddRequest> r);
            /// <summary>两百万次浮点乘法</summary>
            std::shared_ptr<TestMultiplyReply> TestMultiply(std::shared_ptr<TestMultiplyRequest> r);
            /// <summary>文本原样返回</summary>
            std::shared_ptr<TestTextReply> TestText(std::shared_ptr<TestTextRequest> r);
            /// <summary>群发消息</summary>
            std::shared_ptr<TestMessageReply> TestMessage(std::shared_ptr<TestMessageRequest> r);
        };
    }
    typedef _Impl::ServerImplementation ServerImplementation;
}
