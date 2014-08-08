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
        virtual void Transform(std::uint8_t *prgBuffer, int Start, int Count) = 0;
        virtual void Inverse(std::uint8_t *prgBuffer, int Start, int Count) = 0;
    };
}
