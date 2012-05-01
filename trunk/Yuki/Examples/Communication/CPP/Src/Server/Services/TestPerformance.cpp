#include "Services/ServerImplementation.h"

#include <memory>
#include <string>
#include <boost/thread.hpp>

using namespace std;
using namespace Communication;
using namespace Server;

/// <summary>�ӷ�</summary>
std::shared_ptr<TestAddReply> ServerImplementation::TestAdd(SessionContext &c, std::shared_ptr<TestAddRequest> r)
{
    return TestAddReply::CreateResult(r->Left + r->Right);
}

/// <summary>������θ���˷�</summary>
std::shared_ptr<TestMultiplyReply> ServerImplementation::TestMultiply(SessionContext &c, std::shared_ptr<TestMultiplyRequest> r)
{
    auto v = r->Operand;
    auto o = 0.0;
    for (int k = 1; k <= 1000000; k += 1)
    {
        o += v * (k * 0.000001);
    }
    return TestMultiplyReply::CreateResult(o);
}

/// <summary>�ı�ԭ������</summary>
std::shared_ptr<TestTextReply> ServerImplementation::TestText(SessionContext &c, std::shared_ptr<TestTextRequest> r)
{
    return TestTextReply::CreateResult(r->Text);
}

/// <summary>Ⱥ����Ϣ</summary>
std::shared_ptr<TestMessageReply> ServerImplementation::TestMessage(SessionContext &c, std::shared_ptr<TestMessageRequest> r)
{
    auto m = make_shared<TestMessageReceivedEvent>();
    m->Message = r->Message;
    auto Sessions = sc->Sessions();
    for (int k = 0; k < (int)(Sessions->size()); k += 1)
    {
        auto rc = (*Sessions)[k];
        if (rc == c.shared_from_this()) { continue; }
        if (TestMessageReceived != nullptr)
        {
            TestMessageReceived(*rc, m);
        }
    }
    return TestMessageReply::CreateSuccess(Sessions->size());
}
