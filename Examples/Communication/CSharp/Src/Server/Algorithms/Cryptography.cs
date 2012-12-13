using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Server.Algorithms
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
        public static Byte[] SHA1(Byte[] Bytes)
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            return sha.ComputeHash(Bytes);
        }
        public static Byte[] UTF8(String s)
        {
            return Encoding.UTF8.GetBytes(s);
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
