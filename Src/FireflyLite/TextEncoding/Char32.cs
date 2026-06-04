using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Firefly.TextEncoding
{
    [DebuggerDisplay("{ToDisplayString()}")]
    public struct Char32 : IEquatable<Char32>, IComparable<Char32>
    {
        private int Unicode;
        public Char32(int Unicode)
        {
            this.Unicode = Unicode;
        }

        public int Value
        {
            get { return Unicode; }
        }

        public string ToDisplayString()
        {
            var List = new List<string>();
            List.Add(string.Format("U+{0:X4}", Unicode));
            if (!IsControlChar) List.Add(string.Format("\"{0}\"", ToString()));
            return "Char32{" + string.Join(", ", List.ToArray()) + "}";
        }

        public override string ToString()
        {
            return ToString(this);
        }

        private bool IsControlChar
        {
            get { return Unicode >= 0 && Unicode <= 0x1F; }
        }

        public static string ToString(Char32 c)
        {
            if (c.Unicode >= 0 && c.Unicode < 0x10000)
            {
                return String16.ChrW(c.Unicode).ToString();
            }
            else if (c.Unicode >= 0x10000 && c.Unicode < 0x10FFFF)
            {
                int S0;
                int S1;
                BitOperations.SplitBits(out S1, 10, out S0, 10, c.Unicode - 0x10000);
                int L = BitOperations.ConcatBits((byte)0x37, 6, (byte)S0, 10);
                int H = BitOperations.ConcatBits((byte)0x36, 6, (byte)S1, 10);
                return String16.ChrW(H).ToString() + String16.ChrW(L).ToString();
            }
            else
            {
                throw new InvalidDataException();
            }
        }

        public static Char32 FromString(string UTF16B)
        {
            if (UTF16B == "") return new Char32(0);
            int H = String16.AscW(UTF16B[0]);
            if (H >= 0xD800 && H <= 0xDBFF)
            {
                if (UTF16B.Length != 2) throw new InvalidDataException();
                int L = String16.AscW(UTF16B[1]);
                if (L < 0xDC00 || L > 0xDFFF) throw new InvalidDataException();
                return new Char32(BitOperations.ConcatBits(H.Bits(9, 0), 10, L.Bits(9, 0), 10) + 0x10000);
            }
            else
            {
                if (UTF16B.Length != 1) throw new InvalidDataException();
                return new Char32(H);
            }
        }

        public static implicit operator int(Char32 c)
        {
            return c.Unicode;
        }

        public static implicit operator Char32(int c)
        {
            return new Char32(c);
        }

        public static implicit operator Char32(char c)
        {
            return FromString(c.ToString());
        }

        public static explicit operator char(Char32 c)
        {
            var l = c.ToString();
            if (l.Length > 1) throw new ArgumentOutOfRangeException();
            return l[0];
        }

        public static explicit operator Char32(string c)
        {
            return FromString(c);
        }

        public static implicit operator string(Char32 c)
        {
            return c.ToString();
        }

        public bool Equals(Char32 other)
        {
            return Unicode == other.Unicode;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (!(obj is Char32)) return false;
            return Equals((Char32)obj);
        }

        public override int GetHashCode()
        {
            return Unicode;
        }

        public int CompareTo(Char32 other)
        {
            return Unicode.CompareTo(other.Unicode);
        }

        public static bool operator ==(Char32 l, Char32 r) { return l.Equals(r); }
        public static bool operator !=(Char32 l, Char32 r) { return !l.Equals(r); }
        public static bool operator <(Char32 l, Char32 r) { return l.CompareTo(r) < 0; }
        public static bool operator <=(Char32 l, Char32 r) { return l.CompareTo(r) <= 0; }
        public static bool operator >(Char32 l, Char32 r) { return l.CompareTo(r) > 0; }
        public static bool operator >=(Char32 l, Char32 r) { return l.CompareTo(r) >= 0; }
    }

    public static class String32
    {
        public static Char32 ChrQ(int u)
        {
            return (Char32)u;
        }

        public static int AscQ(Char32 c)
        {
            return (int)c;
        }

        public static Char32[] FromUTF16B(string s)
        {
            var cl = new List<Char32>();

            for (int n = 0; n < s.Length; n++)
            {
                char c = s[n];
                int H = String16.AscW(c);
                if (H >= 0xD800 && H <= 0xDBFF)
                {
                    cl.Add((Char32)(c.ToString() + s[n + 1].ToString()));
                    n += 1;
                }
                else
                {
                    cl.Add((Char32)c);
                }
            }

            return cl.ToArray();
        }

        public static string ToUTF16B(this IEnumerable<Char32> s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                sb.Append(c.ToString());
            }
            return sb.ToString();
        }
    }

    public static class String16
    {
        public static char ChrW(short u)
        {
            return Convert.ToChar(unchecked((ushort)u));
        }

        public static char ChrW(ushort u)
        {
            return Convert.ToChar(u);
        }

        public static char ChrW(int u)
        {
            return Convert.ToChar(u);
        }

        public static ushort AscW(char c)
        {
            return Convert.ToUInt16(c);
        }

        public static Char32[] ToUTF32(this string s)
        {
            return String32.FromUTF16B(s);
        }

        public static string FromUTF32(IEnumerable<Char32> s)
        {
            return String32.ToUTF16B(s);
        }

        public static string UnifyNewLineToCrLf(this string s)
        {
            return s.Replace(ControlChars.CrLf, ControlChars.Lf.ToString()).Replace(ControlChars.Cr.ToString(), ControlChars.Lf.ToString()).Replace(ControlChars.Lf.ToString(), ControlChars.CrLf);
        }

        public static string UnifyNewLineToLf(this string s)
        {
            return s.Replace(ControlChars.CrLf, ControlChars.Lf.ToString()).Replace(ControlChars.Cr.ToString(), ControlChars.Lf.ToString());
        }

        public static string TrimStart(this string s, Char32 c)
        {
            var s32 = s.ToUTF32();
            for (int n = 0; n < s32.Length; n++)
            {
                if (s32[n] != c)
                {
                    return s32.Skip(n).ToUTF16B();
                }
            }
            return "";
        }

        public static string TrimEnd(this string s, Char32 c)
        {
            var s32 = s.ToUTF32();
            for (int n = s32.Length - 1; n >= 0; n--)
            {
                if (s32[n] != c)
                {
                    return s32.Take(n + 1).ToUTF16B();
                }
            }
            return "";
        }

        public static string Trim(this string s, Char32 c)
        {
            return s.TrimStart(c).TrimEnd(c);
        }
    }
}
