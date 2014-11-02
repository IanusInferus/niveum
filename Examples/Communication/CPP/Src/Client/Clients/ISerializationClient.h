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
        virtual void HandleResult(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters) = 0;
        std::function<void(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)> ClientEvent;
    };
}
