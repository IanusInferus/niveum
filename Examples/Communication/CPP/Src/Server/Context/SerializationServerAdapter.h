#pragma once

#include "Generated/Communication.h"
#include "Generated/CommunicationBinary.h"
#include "Servers/ISerializationServer.h"

#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <memory>

namespace Server
{
    class BinarySerializationServerAdapter : public IBinarySerializationServerAdapter
    {
    private:
        std::shared_ptr<Communication::IApplicationServer> s;
        std::shared_ptr<Communication::Binary::BinarySerializationServer> ss;
        std::shared_ptr<Communication::Binary::BinarySerializationServerEventDispatcher> ssed;

    public:
        BinarySerializationServerAdapter(std::shared_ptr<Communication::IApplicationServer> ApplicationServer);

        std::uint64_t Hash();
        bool HasCommand(std::u16string CommandName, std::uint32_t CommandHash);
        void ExecuteCommand(std::u16string CommandName, std::uint32_t CommandHash, std::vector<std::uint8_t> Parameters, std::function<void(std::vector<std::uint8_t>)> OnSuccess, std::function<void(const std::exception &)> OnFailure);
    };
}
