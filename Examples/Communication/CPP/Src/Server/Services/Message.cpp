#include "Services/ServerImplementation.h"

#include <memory>
#include <string>

using namespace Communication;
using namespace Server::Services;

/// <summary>发送消息</summary>
std::shared_ptr<SendMessageReply> ServerImplementation::SendMessage(std::shared_ptr<SendMessageRequest> r)
{
    if (r->Content.size() > 256)
    {
        return SendMessageReply::CreateTooLong();
    }
    if (r->Content == L"login")
    {
        SessionContext->RaiseAuthenticated();
        return SendMessageReply::CreateSuccess();
    }
    else if (r->Content == L"secure")
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
        SessionContext->RaiseSecureConnectionRequired(sc);
        return SendMessageReply::CreateSuccess();
    }
    SessionContext->SendMessageCount += 1;
    auto Sessions = ServerContext->Sessions();
    for (int k = 0; k < static_cast<int>(Sessions->size()); k += 1)
    {
        auto rc = (*Sessions)[k];
        {
            auto Lock = rc->WriterLock();
            rc->ReceivedMessageCount += 1;
            if (rc->MessageReceived != nullptr)
            {
                auto e = std::make_shared<MessageReceivedEvent>();
                e->Content = r->Content;
                rc->MessageReceived(e);
            }
        }
    }
    return SendMessageReply::CreateSuccess();
}
