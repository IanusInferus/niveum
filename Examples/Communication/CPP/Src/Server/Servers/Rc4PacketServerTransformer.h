#pragma once

#include "IContext.h"

#include "BaseSystem/Cryptography.h"

#include <cstdint>
#include <memory>
#include <vector>

namespace Server
{
    class Rc4PacketServerTransformer : public IBinaryTransformer
    {
    private:
        bool WillUseEncryption;
        bool UseEncryption;
        std::shared_ptr<Algorithms::RC4> ServerStream;
        std::shared_ptr<Algorithms::RC4> ClientStream;

    public:
        Rc4PacketServerTransformer()
            : WillUseEncryption(false), UseEncryption(false)
        {
        }

        void SetSecureContext(std::shared_ptr<SecureContext> SecureContext)
        {
            WillUseEncryption = true;
            ServerStream = std::make_shared<Algorithms::RC4>(SecureContext->ServerToken);
            ClientStream = std::make_shared<Algorithms::RC4>(SecureContext->ClientToken);
            ServerStream->Skip(1536);
            ClientStream->Skip(1536);
        }

        void Transform(std::vector<std::uint8_t> &Buffer, int Start, int Count)
        {
            if (!UseEncryption) { return; }

            auto s = ServerStream;
            for (int k = 0; k < Count; k += 1)
            {
                auto Index = Start + k;
                Buffer[Index] = Buffer[Index] ^ s->NextByte();
            }
        }

        void Inverse(std::vector<std::uint8_t> &Buffer, int Start, int Count)
        {
            if (!WillUseEncryption) { return; }

            auto s = ClientStream;
            for (int k = 0; k < Count; k += 1)
            {
                auto Index = Start + k;
                Buffer[Index] = Buffer[Index] ^ s->NextByte();
            }
            if (!UseEncryption) { UseEncryption = true; }
        }
    };
}
