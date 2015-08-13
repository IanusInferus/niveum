#pragma once

#include "Communication.h"
#include "CommunicationBinary.h"
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
        bool HasCommand(std::wstring CommandName, std::uint32_t CommandHash);
        void ExecuteCommand(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters, std::function<void(std::shared_ptr<std::vector<std::uint8_t>>)> OnSuccess, std::function<void(const std::exception &)> OnFailure);
    };
}
