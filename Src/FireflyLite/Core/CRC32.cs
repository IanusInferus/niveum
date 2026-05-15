using System;

namespace Firefly
{
    public class CRC32
    {
        private int[] Table;
        private int Result;

        public void Reset()
        {
            Result = unchecked((int)0xFFFFFFFF);
        }
        public void PushData(byte b)
        {
            int iLookup = (Result & 0xFF) ^ b;
            Result = (int)(((uint)Result) >> 8) & 0xFFFFFF;
            Result = Result ^ Table[iLookup];
        }
        public int GetCRC32()
        {
            return ~Result;
        }

        public CRC32()
        {
            int Coefficients = unchecked((int)0xEDB88320);

            Table = new int[256];

            for (int i = 0; i <= 255; i++)
            {
                int CRC = i;
                for (int j = 0; j <= 7; j++)
                {
                    if ((CRC & 1) != 0)
                    {
                        CRC = (int)(((uint)CRC) >> 1) & 0x7FFFFFFF;
                        CRC = CRC ^ Coefficients;
                    }
                    else
                    {
                        CRC = (int)(((uint)CRC) >> 1) & 0x7FFFFFFF;
                    }
                }
                Table[i] = CRC;
            }

            Reset();
        }
    }
}
