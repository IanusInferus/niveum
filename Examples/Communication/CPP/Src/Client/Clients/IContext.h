#pragma once

#include <cstdint>
#include <vector>

namespace Client
{
    class SecureContext
    {
    public:
        std::vector<std::uint8_t> ServerToken;
        std::vector<std::uint8_t> ClientToken;
    };

    class IBinaryTransformer
    {
    public:
        virtual void Transform(std::vector<std::uint8_t> &Buffer, int Start, int Count) = 0;
        virtual void Inverse(std::vector<std::uint8_t> &Buffer, int Start, int Count) = 0;
    };
}
