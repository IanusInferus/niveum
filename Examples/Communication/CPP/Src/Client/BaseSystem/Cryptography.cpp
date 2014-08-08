#include "Cryptography.h"

namespace Algorithms
{
    namespace Cryptography
    {
        RC4::RC4(const std::vector<std::uint8_t> &Key)
        {
            for (int k = 0; k < 256; k += 1)
            {
                S[k] = static_cast<std::uint8_t>(k);
            }
            int b = 0;
            for (int a = 0; a < 256; a += 1)
            {
                b = (b + S[a] + Key[a % Key.size()]) % 256;
                Swap(S[a], S[b]);
            }

            i = 0;
            j = 0;
        }

        std::uint8_t RC4::NextByte()
        {
            i = (i + 1) % 256;
            j = (j + S[i]) % 256;
            Swap(S[i], S[j]);
            auto K = S[(S[i] + S[j]) % 256];
            return K;
        }

        void RC4::Skip(int n)
        {
            for (int k = 0; k < n; k += 1)
            {
                i = (i + 1) % 256;
                j = (j + S[i]) % 256;
                Swap(S[i], S[j]);
            }
        }

        void RC4::Swap(std::uint8_t &a, std::uint8_t &b)
        {
            auto t = a;
            a = b;
            b = t;
        }
    }
}
