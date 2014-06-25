﻿using System;
using System.Collections.Generic;
using System.Linq;
using Algorithms;

namespace Server
{
    public class Rc4PacketServerTransformer : IBinaryTransformer
    {
        private Boolean WillUseEncryption = false;
        private Boolean UseEncryption = false;
        private Cryptography.RC4 ServerStream;
        private Cryptography.RC4 ClientStream;

        public void SetSecureContext(SecureContext SecureContext)
        {
            WillUseEncryption = true;
            ServerStream = new Cryptography.RC4(SecureContext.ServerToken);
            ClientStream = new Cryptography.RC4(SecureContext.ClientToken);
            ServerStream.Skip(1536);
            ClientStream.Skip(1536);
        }

        public void Transform(Byte[] Buffer, int Start, int Count)
        {
            if (!UseEncryption) { return; }

            var s = ServerStream;
            for (int k = 0; k < Count; k += 1)
            {
                var Index = Start + k;
                Buffer[Index] = (Byte)(Buffer[Index] ^ s.NextByte());
            }
        }

        public void Inverse(Byte[] Buffer, int Start, int Count)
        {
            if (!WillUseEncryption) { return; }

            var s = ClientStream;
            for (int k = 0; k < Count; k += 1)
            {
                var Index = Start + k;
                Buffer[Index] = (Byte)(Buffer[Index] ^ s.NextByte());
            }
            if (!UseEncryption) { UseEncryption = true; }
        }
    }
}
