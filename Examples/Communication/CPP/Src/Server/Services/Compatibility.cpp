#include "Services/ServerImplementation.h"

#include "BaseSystem/StringUtilities.h"

using namespace Communication;
using namespace Server::Services;

std::shared_ptr<IEventPump> ServerImplementation::CreateEventPump(std::function<std::function<std::u16string()>(std::vector<std::u16string>)> GetVersionResolver)
{
    auto ep = std::make_shared<EventPump>();
    ep->Error = [=](std::shared_ptr<class ErrorEvent> e) { if (Error != nullptr) { Error(e); } };
    ep->ErrorCommand = [=](std::shared_ptr<class ErrorCommandEvent> e) { if (ErrorCommand != nullptr) { ErrorCommand(e); } };
    ep->ServerShutdown = [=](std::shared_ptr<class ServerShutdownEvent> e) { if (ServerShutdown != nullptr) { ServerShutdown(e); } };
    auto MessageReceivedResolver = GetVersionResolver({ u"2" });
    ep->MessageReceived = [=](std::shared_ptr<class MessageReceivedEvent> eHead)
    {
        auto Version = MessageReceivedResolver();
        if (Version == u"")
        {
            if (MessageReceived != nullptr) { MessageReceived(eHead); }
            return;
        }
        if (Version == u"2")
        {
            auto e = MessageReceivedAt2EventFromHead(eHead);
            if (MessageReceivedAt2 != nullptr) { MessageReceivedAt2(e); }
            return;
        }
        throw std::logic_error("InvalidOperation");
    };
    ep->CommunicationDuplicationDotError = [=](std::shared_ptr<class CommunicationDuplication::ErrorEvent> e) { if (CommunicationDuplicationDotError != nullptr) { CommunicationDuplicationDotError(e); } };
    ep->TestMessageReceived = [=](std::shared_ptr<class TestMessageReceivedEvent> e) { if (TestMessageReceived != nullptr) { TestMessageReceived(e); } };
    return ep;
}

std::shared_ptr<class SendMessageAt1Reply> ServerImplementation::SendMessageAt1(std::shared_ptr<class SendMessageAt1Request> r)
{
    auto HeadRequest = SendMessageAt1RequestToHead(r);
    auto HeadReply = SendMessage(HeadRequest);
    auto Reply = SendMessageAt1ReplyFromHead(HeadReply);
    return Reply;
}
std::shared_ptr<class SendMessageRequest> ServerImplementation::SendMessageAt1RequestToHead(std::shared_ptr<class SendMessageAt1Request> o)
{
    auto ho = std::make_shared<std::shared_ptr<class SendMessageRequest>::element_type>();
    ho->Content = (o->Message->Title != u"" ? o->Message->Title + u"\r\n" : u"") + JoinStrings(o->Message->Lines, u"\r\n");
    return ho;
}
std::shared_ptr<class SendMessageAt1Reply> ServerImplementation::SendMessageAt1ReplyFromHead(std::shared_ptr<class SendMessageReply> ho)
{
    if (ho->OnSuccess())
    {
        return SendMessageAt1Reply::CreateSuccess();
    }
    if (ho->OnTooLong())
    {
        return SendMessageAt1Reply::CreateLinesTooLong();
    }
    throw std::logic_error("InvalidOperation");
}

std::shared_ptr<class SendMessageAt2Reply> ServerImplementation::SendMessageAt2(std::shared_ptr<class SendMessageAt2Request> r)
{
    auto HeadRequest = SendMessageAt2RequestToHead(r);
    auto HeadReply = SendMessage(HeadRequest);
    auto Reply = SendMessageAt2ReplyFromHead(HeadReply);
    return Reply;
}
std::shared_ptr<class SendMessageRequest> ServerImplementation::SendMessageAt2RequestToHead(std::shared_ptr<class SendMessageAt2Request> o)
{
    auto ho = std::make_shared<std::shared_ptr<class SendMessageRequest>::element_type>();
    ho->Content = (o->Message->Title != u"" ? o->Message->Title + u"\r\n" : u"") + JoinStrings(o->Message->Lines, u"\r\n");
    return ho;
}
std::shared_ptr<class SendMessageAt2Reply> ServerImplementation::SendMessageAt2ReplyFromHead(std::shared_ptr<class SendMessageReply> ho)
{
    if (ho->OnSuccess())
    {
        return SendMessageAt2Reply::CreateSuccess();
    }
    if (ho->OnTooLong())
    {
        return SendMessageAt2Reply::CreateLinesTooLong();
    }
    throw std::logic_error("InvalidOperation");
}

std::shared_ptr<class MessageReceivedAt2Event> ServerImplementation::MessageReceivedAt2EventFromHead(std::shared_ptr<class MessageReceivedEvent> ho)
{
    auto o = std::make_shared<std::shared_ptr<class MessageReceivedAt2Event>::element_type>();
    o->Title = u"";
    o->Lines = SplitString(ReplaceAllCopy(ho->Content, u"\r\n", u"\n"), u"\n");
    return o;
}

void ServerImplementation::TestAddAt1(std::shared_ptr<class TestAddAt1Request> r, std::function<void(std::shared_ptr<class TestAddAt1Reply>)> Callback, std::function<void(const std::exception &)> OnFailure)
{
    auto HeadRequest = TestAddAt1RequestToHead(r);
    TestAdd(HeadRequest, [=](std::shared_ptr<class TestAddReply> HeadReply) { Callback(TestAddAt1ReplyFromHead(HeadReply)); }, OnFailure);
}
std::shared_ptr<class TestAddRequest> ServerImplementation::TestAddAt1RequestToHead(std::shared_ptr<class TestAddAt1Request> o)
{
    auto ho = std::make_shared<std::shared_ptr<class TestAddRequest>::element_type>();
    ho->Left = o->Operand1;
    ho->Right = o->Operand2.value_or(0);
    return ho;
}
std::shared_ptr<class TestAddAt1Reply> ServerImplementation::TestAddAt1ReplyFromHead(std::shared_ptr<class TestAddReply> ho)
{
    if (ho->OnResult())
    {
        return TestAddAt1Reply::CreateResult(ho->Result);
    }
    throw std::logic_error("InvalidOperation");
}

void ServerImplementation::TestAddAt2(std::shared_ptr<class TestAddAt2Request> r, std::function<void(std::shared_ptr<class TestAddAt2Reply>)> Callback, std::function<void(const std::exception&)> OnFailure)
{
    auto HeadRequest = TestAddAt2RequestToHead(r);
    TestAdd(HeadRequest, [=](std::shared_ptr<class TestAddReply> HeadReply) { Callback(TestAddAt2ReplyFromHead(HeadReply)); }, OnFailure);
}
std::shared_ptr<class TestAddRequest> ServerImplementation::TestAddAt2RequestToHead(std::shared_ptr<class TestAddAt2Request> o)
{
    auto ho = std::make_shared<std::shared_ptr<class TestAddRequest>::element_type>();
    ho->Left = o->Left.value_or(0);
    ho->Right = o->Right;
    return ho;
}
std::shared_ptr<class TestAddAt2Reply> ServerImplementation::TestAddAt2ReplyFromHead(std::shared_ptr<class TestAddReply> ho)
{
    if (ho->OnResult())
    {
        return TestAddAt2Reply::CreateResult(ho->Result);
    }
    throw std::logic_error("InvalidOperation");
}

std::shared_ptr<class TestAverageAt1Reply> ServerImplementation::TestAverageAt1(std::shared_ptr<class TestAverageAt1Request> r)
{
    auto HeadRequest = TestAverageAt1RequestToHead(r);
    auto HeadReply = TestAverage(HeadRequest);
    auto Reply = TestAverageAt1ReplyFromHead(HeadReply);
    return Reply;
}
std::shared_ptr<class TestAverageRequest> ServerImplementation::TestAverageAt1RequestToHead(std::shared_ptr<class TestAverageAt1Request> o)
{
    auto ho = std::make_shared<std::shared_ptr<class TestAverageRequest>::element_type>();
    ho->Values = ListOfAverageInputAt1ToHead(o->Values);
    return ho;
}
std::shared_ptr<class TestAverageAt1Reply> ServerImplementation::TestAverageAt1ReplyFromHead(std::shared_ptr<class TestAverageReply> ho)
{
    if (ho->OnResult())
    {
        return TestAverageAt1Reply::CreateResult(OptionalOfAverageResultAt1FromHead(ho->Result));
    }
    throw std::logic_error("InvalidOperation");
}
std::shared_ptr<class AverageResultAt1> ServerImplementation::AverageResultAt1FromHead(std::shared_ptr<class AverageResult> ho)
{
    auto o = std::make_shared<std::shared_ptr<class AverageResultAt1>::element_type>();
    o->Value = static_cast<int>(ho->Value);
    return o;
}
std::shared_ptr<class AverageInput> ServerImplementation::AverageInputAt1ToHead(std::shared_ptr<class AverageInputAt1> o)
{
    auto ho = std::make_shared<std::shared_ptr<class AverageInput>::element_type>();
    ho->Value = o->Value;
    return ho;
}
std::optional<std::shared_ptr<class AverageResultAt1>> ServerImplementation::OptionalOfAverageResultAt1FromHead(std::optional<std::shared_ptr<class AverageResult>> ho)
{
    return ho.has_value() ? AverageResultAt1FromHead(ho.value()) : std::optional<std::shared_ptr<class AverageResultAt1>>{};
}
std::vector<std::shared_ptr<class AverageInput>> ServerImplementation::ListOfAverageInputAt1ToHead(std::vector<std::shared_ptr<class AverageInputAt1>> o)
{
    std::vector<std::shared_ptr<class AverageInput>> l;
    for (auto e : o)
    {
        l.push_back(AverageInputAt1ToHead(e));
    }
    return l;
}

std::shared_ptr<class TestSumAt2Reply> ServerImplementation::TestSumAt2(std::shared_ptr<class TestSumAt2Request> r)
{
    auto s = 0.0;
    for (auto v : r->Values)
    {
        s += v;
    }
    return TestSumAt2Reply::CreateResult(s);
}
