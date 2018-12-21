#include "Services/ServerImplementation.h"

#include <memory>
#include <string>

using namespace Communication;
using namespace Server::Services;

/// <summary>加法</summary>
void ServerImplementation::TestAdd(std::shared_ptr<TestAddRequest> r, std::function<void(std::shared_ptr<TestAddReply>)> Callback, std::function<void(const std::exception &)> OnFailure)
{
    Callback(TestAddReply::CreateResult(r->Left + r->Right));
}

/// <summary>两百万次浮点乘法</summary>
std::shared_ptr<TestMultiplyReply> ServerImplementation::TestMultiply(std::shared_ptr<TestMultiplyRequest> r)
{
    auto v = r->Operand;
    auto o = 0.0;
    for (int k = 1; k <= 1000000; k += 1)
    {
        o += v * (k * 0.000001);
    }
    return TestMultiplyReply::CreateResult(o);
}

/// <summary>文本原样返回</summary>
std::shared_ptr<TestTextReply> ServerImplementation::TestText(std::shared_ptr<TestTextRequest> r)
{
    return TestTextReply::CreateResult(r->Text);
}

/// <summary>群发消息</summary>
std::shared_ptr<TestMessageReply> ServerImplementation::TestMessage(std::shared_ptr<TestMessageRequest> r)
{
    auto m = std::make_shared<TestMessageReceivedEvent>();
    m->Message = r->Message;
    SessionContext->SendMessageCount += 1;
    auto Sessions = ServerContext->Sessions();
    for (int k = 0; k < static_cast<int>(Sessions->size()); k += 1)
    {
        auto rc = (*Sessions)[k];
        if (rc == SessionContext->shared_from_this()) { continue; }
        {
            auto Lock = rc->WriterLock();
            rc->ReceivedMessageCount += 1;
            if (rc->EventPump != nullptr)
            {
                rc->EventPump->TestMessageReceived(m);
            }
        }
    }
    return TestMessageReply::CreateSuccess(Sessions->size());
}
