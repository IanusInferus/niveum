#include "Services/ServerImplementation.h"

using namespace Communication;
using namespace Server;

/// <summary>发送消息</summary>
std::shared_ptr<SendMessageAt1Reply> ServerImplementation::SendMessageAt1(std::shared_ptr<SendMessageAt1Request> r)
{
    throw std::logic_error("NotSupported");
}

std::shared_ptr<TestAddAt1Reply> ServerImplementation::TestAddAt1(std::shared_ptr<TestAddAt1Request> r)
{
    return TestAddAt1Reply::CreateResult(r->Operand1 + r->Operand2);
}
