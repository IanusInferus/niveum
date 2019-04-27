#pragma once

#include <memory>
#include <cstdint>
#include <vector>
#include <string>
#include <functional>

namespace Client
{
    class IBinarySerializationClientAdapter
    {
    public:
        virtual ~IBinarySerializationClientAdapter() {}

        virtual std::uint64_t Hash() = 0;
        virtual void HandleResult(std::u16string CommandName, std::uint32_t CommandHash, std::vector<std::uint8_t> Parameters) = 0;
        std::function<void(std::u16string CommandName, std::uint32_t CommandHash, std::vector<std::uint8_t> Parameters)> ClientEvent;
    };
}
