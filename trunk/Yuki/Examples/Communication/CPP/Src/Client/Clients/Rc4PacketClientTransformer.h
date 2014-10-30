#pragma once

#include "IContext.h"

#include "BaseSystem/Cryptography.h"

#include <cstdint>
#include <memory>
#include <vector>

namespace Client
{
    class Rc4PacketClientTransformer : public IBinaryTransformer
    {
    private:
        bool UseEncryption;
        std::shared_ptr<Algorithms::RC4> ServerStream;
        std::shared_ptr<Algorithms::RC4> ClientStream;

    public:
        Rc4PacketClientTransformer()
            : UseEncryption(false)
        {
        }

        void SetSecureContext(std::shared_ptr<SecureContext> SecureContext)
        {
            UseEncryption = true;
            ServerStream = std::make_shared<Algorithms::RC4>(SecureContext->ServerToken);
            ClientStream = std::make_shared<Algorithms::RC4>(SecureContext->ClientToken);
            ServerStream->Skip(1536);
            ClientStream->Skip(1536);
        }

        void Transform(std::uint8_t *prgBuffer, int Start, int Count)
        {
            if (!UseEncryption) { return; }

            auto s = ClientStream;
            for (int k = 0; k < Count; k += 1)
            {
                auto Index = Start + k;
                prgBuffer[Index] = prgBuffer[Index] ^ s->NextByte();
            }
        }

        void Inverse(std::uint8_t *prgBuffer, int Start, int Count)
        {
            if (!UseEncryption) { return; }

            auto s = ServerStream;
            for (int k = 0; k < Count; k += 1)
            {
                auto Index = Start + k;
                prgBuffer[Index] = prgBuffer[Index] ^ s->NextByte();
            }
        }
    };
}
