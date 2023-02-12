#pragma once

#include "Generated/Communication.h"
#include "Generated/CommunicationBinary.h"
#include "Context/ServerContext.h"
#include "Context/SessionContext.h"
#include "BaseSystem/StringUtilities.h"

#include <memory>
#include <string>

#undef SendMessage

namespace Server
{
    namespace Services
    {
        /// <summary>
        /// 本类的所有公共成员均是线程安全的。
        /// </summary>
        class ServerImplementation : public Communication::IApplicationServer, public IServerImplementation
        {
        private:
            std::shared_ptr<class ServerContext> ServerContext;
            std::shared_ptr<class SessionContext> SessionContext;
        public:
            ServerImplementation(std::shared_ptr<class ServerContext> sc, std::shared_ptr<class SessionContext> c)
            {
                this->ServerContext = sc;
                this->SessionContext = c;
                RegisterCrossSessionEvents();
            }
            ~ServerImplementation()
            {
                Stop();
            }
            void Stop()
            {
                UnregisterCrossSessionEvents();
            }

            class EventPump : public Communication::IEventPump
            {
            };
            std::shared_ptr<Communication::IEventPump> CreateEventPump(std::function<std::function<std::u16string()>(std::vector<std::u16string>)> GetVersionResolver);
            void RegisterCrossSessionEvents()
            {
                auto Lock = SessionContext->WriterLock();
                auto GetVersionResolver = [this](std::vector<std::u16string> Versions)
                {
                    std::vector<int> Sorted;
                    for (auto v : Versions)
                    {
                        Sorted.push_back(Parse<int>(v));
                    }
                    std::sort(Sorted.begin(), Sorted.end());
                    return [this, Sorted]() -> std::u16string
                    {
                        auto Version = SessionContext->Version;
                        if (Version == u"") { return u""; }
                        if (Sorted.size() == 0) { return u""; }
                        auto cv = Parse<int>(Version);
                        auto vPrev = 0;
                        for (auto v : Sorted)
                        {
                            if (cv <= v)
                            {
                                vPrev = v;
                            }
                            else
                            {
                                break;
                            }
                        }
                        return ToU16String(vPrev);
                    };
                };
                SessionContext->EventPump = CreateEventPump(GetVersionResolver);
            }

            void UnregisterCrossSessionEvents()
            {
                auto Lock = SessionContext->WriterLock();
                SessionContext->EventPump = nullptr;
            }

            void RaiseError(std::u16string CommandName, std::u16string Message)
            {
                if (CommandName != u"")
                {
                    if (ErrorCommand != nullptr)
                    {
                        auto e = std::make_shared<Communication::ErrorCommandEvent>();
                        e->CommandName = CommandName;
                        ErrorCommand(e);
                    }
                    if (Error != nullptr)
                    {
                        auto e = std::make_shared<Communication::ErrorEvent>();
                        e->Message = CommandName + u": " + Message;
                        Error(e);
                    }
                }
                else
                {
                    if (Error != nullptr)
                    {
                        auto e = std::make_shared<Communication::ErrorEvent>();
                        e->Message = Message;
                        Error(e);
                    }
                }
            }

            /// <summary>关闭服务器</summary>
            std::shared_ptr<Communication::ShutdownReply> Shutdown(std::shared_ptr<Communication::ShutdownRequest> r) override;
            /// <summary>服务器时间</summary>
            std::shared_ptr<Communication::ServerTimeReply> ServerTime(std::shared_ptr<Communication::ServerTimeRequest> r) override;
            /// <summary>退出</summary>
            std::shared_ptr<Communication::QuitReply> Quit(std::shared_ptr<Communication::QuitRequest> r) override;
            /// <summary>检测类型结构版本</summary>
            std::shared_ptr<Communication::CheckSchemaVersionReply> CheckSchemaVersion(std::shared_ptr<Communication::CheckSchemaVersionRequest> r) override;
            /// <summary>获取用户信息</summary>
            std::shared_ptr<Communication::GetUserProfileReply> GetUserProfile(std::shared_ptr<Communication::GetUserProfileRequest> r) override;
            /// <summary>发送消息</summary>
            std::shared_ptr<Communication::SendMessageReply> SendMessage(std::shared_ptr<Communication::SendMessageRequest> r) override;
            /// <summary>加法</summary>
            void TestAdd(std::shared_ptr<Communication::TestAddRequest> r, std::function<void(std::shared_ptr<Communication::TestAddReply>)> Callback, std::function<void(const std::exception &)> OnFailure) override;
            /// <summary>两百万次浮点乘法</summary>
            std::shared_ptr<Communication::TestMultiplyReply> TestMultiply(std::shared_ptr<Communication::TestMultiplyRequest> r) override;
            /// <summary>测试平均数</summary>
            std::shared_ptr<Communication::TestAverageReply> TestAverage(std::shared_ptr<Communication::TestAverageRequest> r) override;
            /// <summary>文本原样返回</summary>
            std::shared_ptr<Communication::TestTextReply> TestText(std::shared_ptr<Communication::TestTextRequest> r) override;
            /// <summary>群发消息</summary>
            std::shared_ptr<Communication::TestMessageReply> TestMessage(std::shared_ptr<Communication::TestMessageRequest> r) override;
            /// <summary>服务器时间</summary>
            std::shared_ptr<CommunicationDuplication::ServerTimeReply> CommunicationDuplicationDotServerTime(std::shared_ptr<CommunicationDuplication::ServerTimeRequest> r) override;

            /// <summary>发送消息</summary>
            std::shared_ptr<Communication::SendMessageAt1Reply> SendMessageAt1(std::shared_ptr<Communication::SendMessageAt1Request> r) override;
            std::shared_ptr<Communication::SendMessageRequest> SendMessageAt1RequestToHead(std::shared_ptr<Communication::SendMessageAt1Request> o);
            std::shared_ptr<Communication::SendMessageAt1Reply> SendMessageAt1ReplyFromHead(std::shared_ptr<Communication::SendMessageReply> ho);
            std::shared_ptr<Communication::SendMessageAt2Reply> SendMessageAt2(std::shared_ptr<Communication::SendMessageAt2Request> r) override;
            std::shared_ptr<Communication::SendMessageRequest> SendMessageAt2RequestToHead(std::shared_ptr<Communication::SendMessageAt2Request> o);
            std::shared_ptr<Communication::SendMessageAt2Reply> SendMessageAt2ReplyFromHead(std::shared_ptr<Communication::SendMessageReply> ho);
            std::shared_ptr<Communication::MessageReceivedAt2Event> MessageReceivedAt2EventFromHead(std::shared_ptr<Communication::MessageReceivedEvent> ho);
            /// <summary>加法</summary>
            void TestAddAt1(std::shared_ptr<Communication::TestAddAt1Request> r, std::function<void(std::shared_ptr<Communication::TestAddAt1Reply>)> Callback, std::function<void(const std::exception &)> OnFailure) override;
            std::shared_ptr<Communication::TestAddRequest> TestAddAt1RequestToHead(std::shared_ptr<Communication::TestAddAt1Request> o);
            std::shared_ptr<Communication::TestAddAt1Reply> TestAddAt1ReplyFromHead(std::shared_ptr<Communication::TestAddReply> ho);
            /// <summary>加法</summary>
            void TestAddAt2(std::shared_ptr<Communication::TestAddAt2Request> r, std::function<void(std::shared_ptr<Communication::TestAddAt2Reply>)> Callback, std::function<void(const std::exception&)> OnFailure) override;
            std::shared_ptr<Communication::TestAddRequest> TestAddAt2RequestToHead(std::shared_ptr<Communication::TestAddAt2Request> o);
            std::shared_ptr<Communication::TestAddAt2Reply> TestAddAt2ReplyFromHead(std::shared_ptr<Communication::TestAddReply> ho);
            /// <summary>测试平均数</summary>
            std::shared_ptr<Communication::TestAverageAt1Reply> TestAverageAt1(std::shared_ptr<Communication::TestAverageAt1Request> r) override;
            std::shared_ptr<Communication::TestAverageRequest> TestAverageAt1RequestToHead(std::shared_ptr<Communication::TestAverageAt1Request> o);
            std::shared_ptr<Communication::TestAverageAt1Reply> TestAverageAt1ReplyFromHead(std::shared_ptr<Communication::TestAverageReply> ho);
            std::shared_ptr<Communication::AverageResultAt1> AverageResultAt1FromHead(std::shared_ptr<Communication::AverageResult> ho);
            std::shared_ptr<Communication::AverageInput> AverageInputAt1ToHead(std::shared_ptr<Communication::AverageInputAt1> o);
            std::optional<std::shared_ptr<Communication::AverageResultAt1>> OptionalOfAverageResultAt1FromHead(std::optional<std::shared_ptr<Communication::AverageResult>> ho);
            std::vector<std::shared_ptr<Communication::AverageInput>> ListOfAverageInputAt1ToHead(std::vector<std::shared_ptr<Communication::AverageInputAt1>> o);
            /// <summary>测试和</summary>
            std::shared_ptr<Communication::TestSumAt2Reply> TestSumAt2(std::shared_ptr<Communication::TestSumAt2Request> r) override;
        };
    }
}
