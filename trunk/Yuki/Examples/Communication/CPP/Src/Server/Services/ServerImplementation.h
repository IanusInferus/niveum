#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
#include "Context/ServerContext.h"
#include "Context/SessionContext.h"

#include <memory>
#include <string>

#undef SendMessage

namespace Server
{
    namespace _Impl
    {
        using namespace Communication;

        /// <summary>
        /// ��������й�����Ա�����̰߳�ȫ�ġ�
        /// </summary>
        class ServerImplementation : public IServerImplementation<SessionContext>
        {
        private:
            std::shared_ptr<ServerContext> sc;
        public:
            ServerImplementation(std::shared_ptr<ServerContext> sc)
            {
                this->sc = sc;
            }

            void RaiseError(SessionContext &c, std::wstring CommandName, std::wstring Message)
            {
                if (Error != nullptr)
                {
                    auto e = std::make_shared<ErrorEvent>();
                    e->CommandName = CommandName;
                    e->Message = Message;
                    Error(c, e);
                }
            }

            /// <summary>������ʱ��</summary>
            std::shared_ptr<ServerTimeReply> ServerTime(SessionContext &c, std::shared_ptr<ServerTimeRequest> r);
            /// <summary>�˳�</summary>
            std::shared_ptr<QuitReply> Quit(SessionContext &c, std::shared_ptr<QuitRequest> r);
            /// <summary>������Ϣ</summary>
            std::shared_ptr<SendMessageReply> SendMessage(SessionContext &c, std::shared_ptr<SendMessageRequest> r);
            /// <summary>������Ϣ</summary>
            std::shared_ptr<SendMessageAt1Reply> SendMessageAt1(SessionContext &c, std::shared_ptr<SendMessageAt1Request> r);
        };
    }
    typedef _Impl::ServerImplementation ServerImplementation;
}
