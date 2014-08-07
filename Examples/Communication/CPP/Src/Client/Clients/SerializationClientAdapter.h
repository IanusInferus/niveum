#pragma once

#include "ISerializationClient.h"

#include "Communication.h"
#include "CommunicationBinary.h"

namespace Client
{
    class BinarySerializationClientAdapter : public IBinarySerializationClientAdapter
    {
    private:
        class BinarySender : public Communication::Binary::IBinarySender
        {
        private:
            const BinarySerializationClientAdapter &a;
        public:
            BinarySender(const BinarySerializationClientAdapter &a)
                : a(a)
            {
            }

            void Send(std::wstring CommandName, uint32_t CommandHash, std::shared_ptr<std::vector<uint8_t>> Parameters)
            {
                if (a.ClientEvent != nullptr)
                {
                    a.ClientEvent(CommandName, CommandHash, Parameters);
                }
            }
        };

        std::shared_ptr<Communication::Binary::BinarySerializationClient> bc;

    public:
        BinarySerializationClientAdapter()
        {
            this->bc = std::make_shared<Communication::Binary::BinarySerializationClient>(std::make_shared<BinarySender>(*this));
            auto ac = this->bc->GetApplicationClient();
            ac->ErrorCommand = [=](std::shared_ptr<Communication::ErrorCommandEvent> e)
            {
                this->bc->GetApplicationClient()->DequeueCallback(e->CommandName);
                if (DequeuedCallbackEvent != nullptr)
                {
                    DequeuedCallbackEvent(e->CommandName);
                }
            };
        }

        std::shared_ptr<Communication::IApplicationClient> GetApplicationClient()
        {
            return bc->GetApplicationClient();
        }

        virtual std::uint64_t Hash()
        {
            return bc->GetApplicationClient()->Hash();
        }
        virtual void DequeueCallback(std::wstring CommandName)
        {
            bc->GetApplicationClient()->DequeueCallback(CommandName);
        }
        virtual void HandleResult(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)
        {
            bc->HandleResult(CommandName, CommandHash, Parameters);
        }
    };
}
