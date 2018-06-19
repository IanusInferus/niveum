using System;

namespace Client
{
    public class SecureContext
    {
        public Byte[] ServerToken; //服务器到客户端数据的Token
        public Byte[] ClientToken; //客户端到服务器数据的Token
    }

    public interface IBinaryTransformer
    {
        void Transform(Byte[] Buffer, int Start, int Count);
        void Inverse(Byte[] Buffer, int Start, int Count);
    }
}
