#pragma once

#include <cstdint>
#include <vector>

namespace Client
{
    class SecureContext
    {
    public:
        std::vector<std::uint8_t> ServerToken; //服务器到客户端数据的Token
        std::vector<std::uint8_t> ClientToken; //客户端到服务器数据的Token
    };

    class IBinaryTransformer
    {
    public:
        virtual void Transform(std::vector<std::uint8_t> &Buffer, int Start, int Count) = 0;
        virtual void Inverse(std::vector<std::uint8_t> &Buffer, int Start, int Count) = 0;
    };
}
