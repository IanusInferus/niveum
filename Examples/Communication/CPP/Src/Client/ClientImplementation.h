#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "Context/ClientContext.h"

#include <cstdio>

namespace Client
{
    class ClientImplementation : public Communication::IClientImplementation<ClientContext>
    {
    public:
        /// <summary>错误</summary>
        virtual void Error(ClientContext &c, std::shared_ptr<Communication::ErrorEvent> e)
        {
            c.DequeueCallback(e->CommandName);
            auto m = L"调用'" + e->CommandName + L"'发生错误:" + e->Message;
            wprintf(L"%ls\n", m.c_str());
        }

        /// <summary>接收到消息</summary>
        virtual void MessageReceived(ClientContext &c, std::shared_ptr<Communication::MessageReceivedEvent> e)
        {
            wprintf(L"%ls\n", e->Content.c_str());
        }

        /// <summary>接收到消息</summary>
        virtual void MessageReceivedAt1(ClientContext &c, std::shared_ptr<Communication::MessageReceivedAt1Event> e)
        {
        }
    };
}
