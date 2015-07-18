#include "Services/ServerImplementation.h"

#include <memory>
#include <string>

using namespace std;
using namespace Communication;
using namespace Server;

/// <summary>发送消息</summary>
shared_ptr<SendMessageReply> ServerImplementation::SendMessage(shared_ptr<SendMessageRequest> r)
{
    if (r->Content.size() > 256)
    {
        return SendMessageReply::CreateTooLong();
    }
    c->SendMessageCount += 1;
    auto Sessions = sc->Sessions();
    for (int k = 0; k < (int)(Sessions->size()); k += 1)
    {
        auto rc = (*Sessions)[k];
        {
            auto Lock = rc->WriterLock();
            rc->ReceivedMessageCount += 1;
            if (rc->MessageReceived != nullptr)
            {
                auto e = make_shared<MessageReceivedEvent>();
                e->Content = r->Content;
                rc->MessageReceived(e);
            }
        }
    }
    return SendMessageReply::CreateSuccess();
}
