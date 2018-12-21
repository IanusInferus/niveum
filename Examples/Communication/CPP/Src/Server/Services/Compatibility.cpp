#include "Services/ServerImplementation.h"

#include "BaseSystem/StringUtilities.h"

using namespace Communication;
using namespace Server::Services;

std::shared_ptr<IEventPump> ServerImplementation::CreateEventPump(std::function<std::wstring()> GetVersion)
{
    auto ep = std::make_shared<EventPump>();
    ep->Error = [=](std::shared_ptr<class ErrorEvent> e) { if (Error != nullptr) { Error(e); } };
    ep->ErrorCommand = [=](std::shared_ptr<class ErrorCommandEvent> e) { if (ErrorCommand != nullptr) { ErrorCommand(e); } };
    ep->ServerShutdown = [=](std::shared_ptr<class ServerShutdownEvent> e) { if (ServerShutdown != nullptr) { ServerShutdown(e); } };
    ep->MessageReceived = [=](std::shared_ptr<class MessageReceivedEvent> eHead)
    {
        auto Version = GetVersion();
        if (Version == L"")
        {
            if (MessageReceived != nullptr) { MessageReceived(eHead); }
            return;
        }
        if (Version == L"1")
        {
            auto e = MessageReceivedAt1EventFromHead(eHead);
            if (MessageReceivedAt1 != nullptr) { MessageReceivedAt1(e); }
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
    ho->Content = (o->Title != L"" ? o->Title + L"\r\n" : L"") + JoinStrings(o->Lines, L"\r\n");
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

std::shared_ptr<class MessageReceivedAt1Event> ServerImplementation::MessageReceivedAt1EventFromHead(std::shared_ptr<class MessageReceivedEvent> ho)
{
    auto o = std::make_shared<std::shared_ptr<class MessageReceivedAt1Event>::element_type>();
    o->Title = L"";
    o->Lines = SplitString(ReplaceAllCopy(ho->Content, L"\r\n", L"\n"), L"\n");
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
    ho->Right = o->Operand2;
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
