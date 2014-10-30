using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Algorithms
{
    public static class Cryptography
    {
        private static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        public static Byte[] CreateRandom(int Count)
        {
            var r = new Byte[Count];
            rng.GetBytes(r);
            return r;
        }
        public static Byte[] UTF8(String s)
        {
            return Encoding.UTF8.GetBytes(s);
        }
        public static Int32 CRC32(Byte[] Bytes)
        {
            var c = new CRC32();
            foreach (var b in Bytes)
            {
                c.PushData(b);
            }
            return c.GetCRC32();
        }
        public static Int32 CRC32(IEnumerable<Byte> Bytes)
        {
            var c = new CRC32();
            foreach (var b in Bytes)
            {
                c.PushData(b);
            }
            return c.GetCRC32();
        }

        public static Byte[] SHA1(Byte[] Bytes)
        {
            SHA1 sha = new SHA1Managed();
            return sha.ComputeHash(Bytes);
        }
        public static Byte[] SHA1(IEnumerable<Byte> Bytes)
        {
            SHA1 sha = new SHA1Managed();
            return sha.ComputeHash(Bytes.ToArray());
        }
        /// <summary>
        /// HMAC = H((K XOR opad) :: H((K XOR ipad) :: Inner))
        /// H = SHA1
        /// opad = 0x5C
        /// ipad = 0x36
        /// </summary>
        public static Byte[] HMACSHA1(IEnumerable<Byte> Key, IEnumerable<Byte> Bytes)
        {
            var InnerHash = SHA1(Key.Select(k => unchecked((Byte)(k ^ 0x36))).Concat(Bytes));
            var OuterHash = SHA1(Key.Select(k => unchecked((Byte)(k ^ 0x5C))).Concat(InnerHash));
            return OuterHash;
        }

        public static String BytesToHexString(IEnumerable<Byte> Bytes)
        {
            return String.Join("", Bytes.Select(b => b.ToString("X2")));
        }

        private static Regex rWhitespace = new Regex(@"\s", RegexOptions.ExplicitCapture);
        public static Byte[] HexStringToBytes(String Hex)
        {
            var h = rWhitespace.Replace(Hex, "");
            if (h.Length % 2 != 0) { throw new ArgumentException("HexStringLengthNotEven"); }
            Func<String, Byte> ParseByte = s => Byte.Parse(s, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            return Enumerable.Range(0, h.Length / 2).Select(i => ParseByte(h.Substring(i * 2, 2))).ToArray();
        }
    }

    public class CRC32
    {
        private static Int32[] Table;
        static CRC32()
        {
            //g(x) = x^32 + x^26 + x^23 + x^22 + x^16 + x^12 + x^11 + x^10 + x^8 + x^7 + x^5 + x^4 + x^2 + x + 1
            //多项式系数的位数组表示104C11DB7
            Int32 Coefficients = unchecked((Int32)(0xEDB88320));
            //反向表示

            Table = new Int32[256];

            for (int i = 0; i < 256; i++)
            {
                Int32 CRC = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((CRC & 1) != 0)
                    {
                        CRC = (CRC >> 1) & 0x7FFFFFFF;
                        CRC = CRC ^ Coefficients;
                    }
                    else
                    {
                        CRC = (CRC >> 1) & 0x7FFFFFFF;
                    }
                }
                Table[i] = CRC;
            }
        }

        private Int32 Result;
        public CRC32()
        {
            Reset();
        }
        public void Reset()
        {
            Result = unchecked((Int32)(0xFFFFFFFF));
        }
        public void PushData(byte b)
        {
            int iLookup = (Result & 0xFF) ^ b;
            Result = (Result >> 8) & 0xFFFFFF;
            Result = Result ^ Table[iLookup];
        }
        public Int32 GetCRC32()
        {
            return ~Result;
        }
    }

    public class RC4
    {
        private Byte[] S;
        private int i;
        private int j;

        public RC4(Byte[] Key)
        {
            S = Enumerable.Range(0, 256).Select(k => (Byte)(k)).ToArray();
            int b = 0;
            for (int a = 0; a < 256; a += 1)
            {
                b = (b + S[a] + Key[a % Key.Length]) % 256;
                Swap(ref S[a], ref S[b]);
            }

            i = 0;
            j = 0;
        }

        public Byte NextByte()
        {
            i = (i + 1) % 256;
            j = (j + S[i]) % 256;
            Swap(ref S[i], ref S[j]);
            var K = S[(S[i] + S[j]) % 256];
            return K;
        }

        public void Skip(int n)
        {
            for (int k = 0; k < n; k += 1)
            {
                i = (i + 1) % 256;
                j = (j + S[i]) % 256;
                Swap(ref S[i], ref S[j]);
            }
        }

        private void Swap(ref Byte a, ref Byte b)
        {
            var t = a;
            a = b;
            b = t;
        }
    }
}
