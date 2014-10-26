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
        public static Int32 CRC32(IEnumerable<Byte> Bytes)
        {
            var c = new Firefly.CRC32();
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
        /// <summary>
        /// HMAC = H((K XOR opad) :: H((K XOR ipad) :: Inner))
        /// H = SHA1
        /// opad = 0x5C
        /// ipad = 0x36
        /// </summary>
        public static Byte[] HMACSHA1(IEnumerable<Byte> Key, IEnumerable<Byte> Bytes)
        {
            var InnerHash = SHA1(Key.Select(k => (Byte)(k ^ 0x36)).Concat(Bytes).ToArray());
            var OuterHash = SHA1(Key.Select(k => (Byte)(k ^ 0x5C)).Concat(InnerHash).ToArray());
            return OuterHash;
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

        public static String BytesToHexString(Byte[] Bytes)
        {
            return String.Join("", Bytes.Select(b => b.ToString("X2")).ToArray());
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
}
