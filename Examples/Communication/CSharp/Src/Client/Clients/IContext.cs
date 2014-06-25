using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class SecureContext
    {
        public Byte[] ServerToken;
        public Byte[] ClientToken;
    }

    public interface IBinaryTransformer
    {
        void Transform(Byte[] Buffer, int Start, int Count);
        void Inverse(Byte[] Buffer, int Start, int Count);
    }
}
