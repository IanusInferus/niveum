#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "Context/ClientContext.h"

#include <cstdio>

namespace Client
{
    namespace _Impl
    {
        using namespace Communication;
        typedef ClientContext TContext;

        class ClientImplementation : public IClientImplementation<ClientContext>
        {
        public:
            /// <summary>错误</summary>
            virtual void Error(TContext &c, std::shared_ptr<ErrorEvent> e)
            {
                c.DequeueCallback(e->CommandName);
                auto m = L"调用'" + e->CommandName + L"'发生错误:" + e->Message;
                wprintf(L"%ls\n", m.c_str());
            }

            /// <summary>接收到消息</summary>
            virtual void MessageReceived(TContext &c, std::shared_ptr<MessageReceivedEvent> e)
            {
                wprintf(L"%ls\n", e->Content.c_str());
            }

            /// <summary>接收到消息</summary>
            virtual void MessageReceivedAt1(TContext &c, std::shared_ptr<MessageReceivedAt1Event> e)
            {
            }

            /// <summary>接到群发消息</summary>
            virtual void TestMessageReceived(TContext &c, std::shared_ptr<TestMessageReceivedEvent> e)
            {
            }
        };
    }
    typedef _Impl::ClientImplementation ClientImplementation;
}
