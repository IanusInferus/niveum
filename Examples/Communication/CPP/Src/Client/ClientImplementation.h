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
        /// <summary>����</summary>
        virtual void Error(ClientContext &c, std::shared_ptr<Communication::ErrorEvent> e)
        {
            c.DequeueCallback(e->CommandName);
            auto m = L"����'" + e->CommandName + L"'��������:" + e->Message;
            wprintf(L"%ls\n", m.c_str());
        }

        /// <summary>���յ���Ϣ</summary>
        virtual void MessageReceived(ClientContext &c, std::shared_ptr<Communication::MessageReceivedEvent> e)
        {
            wprintf(L"%ls\n", e->Content.c_str());
        }

        /// <summary>���յ���Ϣ</summary>
        virtual void MessageReceivedAt1(ClientContext &c, std::shared_ptr<Communication::MessageReceivedAt1Event> e)
        {
        }
    };
}
