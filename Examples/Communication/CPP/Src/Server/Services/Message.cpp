#include "Services/ServerImplementation.h"

#include <memory>
#include <string>
#include <boost/thread.hpp>

using namespace std;
using namespace Communication;
using namespace Server;

/// <summary>发送消息</summary>
shared_ptr<SendMessageReply> ServerImplementation::SendMessage(SessionContext &c, shared_ptr<SendMessageRequest> r)
{
    if (r->Content.size() > 256)
    {
        return SendMessageReply::CreateTooLong();
    }
    c.SendMessageCount += 1;
    auto Sessions = sc->GetSessions();
    for (int k = 0; k < (int)(Sessions->size()); k += 1)
    {
        auto rc = (*Sessions)[k];
        {
            {
                boost::unique_lock<boost::shared_mutex> WriterLock(rc->SessionLock);
                rc->ReceivedMessageCount += 1;
            }
            if (MessageReceived != nullptr)
            {
                auto e = make_shared<MessageReceivedEvent>();
                e->Content = r->Content;
                MessageReceived(*rc, e);
            }
        }
    }
    return SendMessageReply::CreateSuccess();
}

/// <summary>发送消息</summary>
shared_ptr<SendMessageAt1Reply> ServerImplementation::SendMessageAt1(SessionContext &c, shared_ptr<SendMessageAt1Request> r)
{
    if (MessageReceivedAt1 != nullptr)
    {
        auto e = make_shared<MessageReceivedAt1Event>();
        e->Title = L"System Updated";
        e->Lines = make_shared<vector<wstring>>();
        e->Lines->push_back(L"Please update your client to a recent version.");
        MessageReceivedAt1(c, e);
    }
    return SendMessageAt1Reply::CreateSuccess();
}
