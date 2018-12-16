#include "Services/ServerImplementation.h"

using namespace Communication;
using namespace Server::Services;

/// <summary>发送消息</summary>
std::shared_ptr<SendMessageAt1Reply> ServerImplementation::SendMessageAt1(std::shared_ptr<SendMessageAt1Request> r)
{
    throw std::logic_error("NotSupported");
}

/// <summary>加法</summary>
void ServerImplementation::TestAddAt1(std::shared_ptr<TestAddAt1Request> r, std::function<void(std::shared_ptr<TestAddAt1Reply>)> Callback, std::function<void(const std::exception &)> OnFailure)
{
    Callback(TestAddAt1Reply::CreateResult(r->Operand1 + r->Operand2));
}
