using System;
using Algorithms;

namespace Client
{
    public class Rc4PacketClientTransformer : IBinaryTransformer
    {
        private Boolean UseEncryption = false;
        private RC4 ServerStream;
        private RC4 ClientStream;

        public void SetSecureContext(SecureContext SecureContext)
        {
            UseEncryption = true;
            ServerStream = new RC4(SecureContext.ServerToken);
            ClientStream = new RC4(SecureContext.ClientToken);
            ServerStream.Skip(1536);
            ClientStream.Skip(1536);
        }

        public void Transform(Byte[] Buffer, int Start, int Count)
        {
            if (!UseEncryption) { return; }

            var s = ClientStream;
            for (int k = 0; k < Count; k += 1)
            {
                var Index = Start + k;
                Buffer[Index] = (Byte)(Buffer[Index] ^ s.NextByte());
            }
        }

        public void Inverse(Byte[] Buffer, int Start, int Count)
        {
            if (!UseEncryption) { return; }

            var s = ServerStream;
            for (int k = 0; k < Count; k += 1)
            {
                var Index = Start + k;
                Buffer[Index] = (Byte)(Buffer[Index] ^ s.NextByte());
            }
        }
    }
}
