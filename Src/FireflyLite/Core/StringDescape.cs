using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Firefly.TextEncoding;

namespace Firefly
{
    public static class StringDescape
    {
        public static string Descape(this string This)
        {
            var m = r.Match(This);
            if (!m.Success) throw new InvalidCastException();

            var ss = new SortedList<int, string>();
            foreach (Capture c in m.Groups["SingleEscape"].Captures)
            {
                ss.Add(c.Index, SingleEscapeDict[c.Value]);
            }
            foreach (Capture c in m.Groups["UnicodeEscape"].Captures)
            {
                ss.Add(c.Index, String32.ChrQ(int.Parse(c.Value, NumberStyles.HexNumber)).ToString());
            }
            foreach (Capture c in m.Groups["ErrorEscape"].Captures)
            {
                throw new ArgumentException("ErrorEscape: Ch " + (c.Index + 1) + " " + c.Value);
            }
            foreach (Capture c in m.Groups["Normal"].Captures)
            {
                ss.Add(c.Index, c.Value);
            }

            var sb = new StringBuilder();
            foreach (var s in ss.Values)
            {
                sb.Append(s);
            }

            return sb.ToString();
        }

        public static string Escape(this string This)
        {
            var l = new List<Char32>();
            foreach (var c in This.ToUTF32())
            {
                int v = c.Value;
                switch (v)
                {
                    case 0x5C:
                        l.AddRange("\\\\".ToUTF32());
                        break;
                    case 0x0:
                        l.AddRange("\\0".ToUTF32());
                        break;
                    case 0x7:
                        l.AddRange("\\a".ToUTF32());
                        break;
                    case 0x8:
                        l.AddRange("\\b".ToUTF32());
                        break;
                    case 0xC:
                        l.AddRange("\\f".ToUTF32());
                        break;
                    case 0xA:
                        l.AddRange("\\n".ToUTF32());
                        break;
                    case 0xD:
                        l.AddRange("\\r".ToUTF32());
                        break;
                    case 0x9:
                        l.AddRange("\\t".ToUTF32());
                        break;
                    case 0xB:
                        l.AddRange("\\v".ToUTF32());
                        break;
                    default:
                        if ((v >= 0x0 && v <= 0x1F) || v == 0x7F)
                        {
                            l.AddRange(Formats("\\u{0:X4}", v).ToUTF32());
                        }
                        else
                        {
                            l.Add(c);
                        }
                        break;
                }
            }
            return l.ToUTF16B();
        }

        public static string Formats(this string This, object arg0)
        {
            return string.Format(This, arg0);
        }
        public static string Formats(this string This, object arg0, object arg1)
        {
            return string.Format(This, arg0, arg1);
        }
        public static string Formats(this string This, object arg0, object arg1, object arg2)
        {
            return string.Format(This, arg0, arg1, arg2);
        }
        public static string Formats(this string This, params object[] args)
        {
            return string.Format(This, args);
        }
        public static string Formats(this string This, IFormatProvider provider, params object[] args)
        {
            return string.Format(provider, This, args);
        }


        private static Dictionary<string, string> _SingleEscapeDict;
        private static Dictionary<string, string> SingleEscapeDict
        {
            get
            {
                if (_SingleEscapeDict != null) return _SingleEscapeDict;
                var d = new Dictionary<string, string>();
                d.Add("\\", "\\");
                d.Add("0", String32.ChrQ(0).ToString());
                d.Add("a", String32.ChrQ(7).ToString());
                d.Add("b", String32.ChrQ(8).ToString());
                d.Add("f", String32.ChrQ(0xC).ToString());
                d.Add("n", String32.ChrQ(0xA).ToString());
                d.Add("r", String32.ChrQ(0xD).ToString());
                d.Add("t", String32.ChrQ(9).ToString());
                d.Add("v", String32.ChrQ(0xB).ToString());
                _SingleEscapeDict = d;
                return _SingleEscapeDict;
            }
        }

        private static string _SingleEscapes;
        private static string SingleEscapes
        {
            get
            {
                if (_SingleEscapes != null) return _SingleEscapes;
                var Chars = new List<string>();
                foreach (var c in "\\0abfnrtv")
                {
                    Chars.Add(Regex.Escape(c.ToString()));
                }
                _SingleEscapes = "\\\\(?<SingleEscape>" + string.Join("|", Chars.ToArray()) + ")";
                return _SingleEscapes;
            }
        }
        private static readonly string UnicodeEscapes = "\\\\U(?<UnicodeEscape>[0-9A-Fa-f]{5})|\\\\u(?<UnicodeEscape>[0-9A-Fa-f]{4})|\\\\x(?<UnicodeEscape>[0-9A-Fa-f]{2})";
        private static readonly string ErrorEscapes = "(?<ErrorEscape>\\\\)";
        private static readonly string Normal = "(?<Normal>.|\\r|\\n)";

        private static readonly Regex r = new Regex("^" + "(" + SingleEscapes + "|" + UnicodeEscapes + "|" + ErrorEscapes + "|" + Normal + ")*" + "$", RegexOptions.ExplicitCapture);
    }
}
