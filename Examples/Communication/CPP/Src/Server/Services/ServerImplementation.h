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
        typedef SessionContext TContext;

        /// <summary>
        /// 本类的所有公共成员均是线程安全的。
        /// </summary>
        class ServerImplementation : public IServerImplementation<SessionContext>
        {
        private:
            std::shared_ptr<ServerContext> sc;
        public:
            ServerImplementation(std::shared_ptr<ServerContext> sc)
            {
                this->sc = sc;
            }

            void RaiseError(SessionContext &c, std::wstring CommandName, std::wstring Message)
            {
                if (Error != nullptr)
                {
                    auto e = std::make_shared<ErrorEvent>();
                    e->CommandName = CommandName;
                    e->Message = Message;
                    Error(c, e);
                }
            }

            /// <summary>服务器时间</summary>
            std::shared_ptr<ServerTimeReply> ServerTime(TContext &c, std::shared_ptr<ServerTimeRequest> r);
            /// <summary>退出</summary>
            std::shared_ptr<QuitReply> Quit(TContext &c, std::shared_ptr<QuitRequest> r);
            /// <summary>发送消息</summary>
            std::shared_ptr<SendMessageReply> SendMessage(TContext &c, std::shared_ptr<SendMessageRequest> r);
            /// <summary>发送消息</summary>
            std::shared_ptr<SendMessageAt1Reply> SendMessageAt1(TContext &c, std::shared_ptr<SendMessageAt1Request> r);
            /// <summary>加法</summary>
            std::shared_ptr<TestAddReply> TestAdd(TContext &c, std::shared_ptr<TestAddRequest> r);
            /// <summary>两百万次浮点乘法</summary>
            std::shared_ptr<TestMultiplyReply> TestMultiply(TContext &c, std::shared_ptr<TestMultiplyRequest> r);
            /// <summary>文本原样返回</summary>
            std::shared_ptr<TestTextReply> TestText(TContext &c, std::shared_ptr<TestTextRequest> r);
            /// <summary>群发消息</summary>
            std::shared_ptr<TestMessageReply> TestMessage(TContext &c, std::shared_ptr<TestMessageRequest> r);
        };
    }
    typedef _Impl::ServerImplementation ServerImplementation;
}
