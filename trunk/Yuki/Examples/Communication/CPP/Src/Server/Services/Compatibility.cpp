#include "Services/ServerImplementation.h"

using namespace std;
using namespace Communication;
using namespace Server;

/// <summary>发送消息</summary>
shared_ptr<SendMessageAt1Reply> ServerImplementation::SendMessageAt1(shared_ptr<SendMessageAt1Request> r)
{
    throw std::logic_error("NotSupported");
}
