#pragma once

#include <cstdint>
#include <vector>

namespace Algorithms
{
    namespace Cryptography
    {
        class RC4
        {
        private:
            std::uint8_t S[256];
            int i;
            int j;

        public:
            RC4(const std::vector<std::uint8_t> &Key);
            std::uint8_t NextByte();
            void Skip(int n);
        private:
            void Swap(std::uint8_t &a, std::uint8_t &b);
        };
    }
}
