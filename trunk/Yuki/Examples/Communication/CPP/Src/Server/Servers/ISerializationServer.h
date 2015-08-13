﻿#pragma once

#include <cstdint>
#include <vector>
#include <string>
#include <functional>
#include <memory>
#include <exception>

namespace Server
{
    typedef std::function<void(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters)> BinaryServerEventDelegate;
    class IBinarySerializationServerAdapter
    {
    public:
        virtual ~IBinarySerializationServerAdapter() {}

        virtual std::uint64_t Hash() = 0;
        virtual bool HasCommand(std::wstring CommandName, std::uint32_t CommandHash) = 0;
        virtual void ExecuteCommand(std::wstring CommandName, std::uint32_t CommandHash, std::shared_ptr<std::vector<std::uint8_t>> Parameters, std::function<void(std::shared_ptr<std::vector<std::uint8_t>>)> OnSuccess, std::function<void(const std::exception &)> OnFailure) = 0;
        BinaryServerEventDelegate ServerEvent;
    };
}
