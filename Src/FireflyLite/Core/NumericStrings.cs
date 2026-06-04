using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Firefly
{
    public static class NumericStrings
    {
        public static byte InvariantParseUInt8(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return byte.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return byte.Parse(s, CultureInfo.InvariantCulture);
        }
        public static ushort InvariantParseUInt16(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return ushort.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ushort.Parse(s, CultureInfo.InvariantCulture);
        }
        public static uint InvariantParseUInt32(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return uint.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return uint.Parse(s, CultureInfo.InvariantCulture);
        }
        public static ulong InvariantParseUInt64(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return ulong.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ulong.Parse(s, CultureInfo.InvariantCulture);
        }
        public static sbyte InvariantParseInt8(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return sbyte.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return sbyte.Parse(s, CultureInfo.InvariantCulture);
        }
        public static short InvariantParseInt16(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return short.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return short.Parse(s, CultureInfo.InvariantCulture);
        }
        public static int InvariantParseInt32(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return int.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return int.Parse(s, CultureInfo.InvariantCulture);
        }
        public static long InvariantParseInt64(string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return long.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return long.Parse(s, CultureInfo.InvariantCulture);
        }
        public static float InvariantParseFloat32(string s)
        {
            return float.Parse(s, CultureInfo.InvariantCulture);
        }
        public static double InvariantParseFloat64(string s)
        {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }
        public static bool InvariantParseBoolean(string s)
        {
            return bool.Parse(s);
        }
        public static decimal InvariantParseDecimal(string s)
        {
            return decimal.Parse(s, CultureInfo.InvariantCulture);
        }

        public static string ToInvariantString(this byte i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this ushort i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this uint i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this ulong i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this sbyte i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this short i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this int i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this long i) { return i.ToString(CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this float f) { return f.ToString("r", CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this double f) { return f.ToString("r", CultureInfo.InvariantCulture); }
        public static string ToInvariantString(this bool b) { return b.ToString(); }
        public static string ToInvariantString(this decimal i) { return i.ToString(CultureInfo.InvariantCulture); }
    }
}
