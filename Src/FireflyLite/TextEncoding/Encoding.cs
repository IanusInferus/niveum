using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Firefly.TextEncoding
{
    public class EncodingNoPreambleWrapper : Encoding
    {
        private Encoding BaseEncoding;
        public EncodingNoPreambleWrapper(Encoding BaseEncoding)
        {
            this.BaseEncoding = BaseEncoding;
        }

        public override Decoder GetDecoder() { return BaseEncoding.GetDecoder(); }
        public override Encoder GetEncoder() { return BaseEncoding.GetEncoder(); }
        public override byte[] GetPreamble() { return new byte[] { }; }

        public override int GetByteCount(char[] chars, int index, int count) { return BaseEncoding.GetByteCount(chars, index, count); }
        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) { return BaseEncoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex); }
        public override int GetCharCount(byte[] bytes, int index, int count) { return BaseEncoding.GetCharCount(bytes, index, count); }
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) { return BaseEncoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex); }
        public override int GetMaxByteCount(int charCount) { return BaseEncoding.GetMaxByteCount(charCount); }
        public override int GetMaxCharCount(int byteCount) { return BaseEncoding.GetMaxCharCount(byteCount); }

        public override object Clone() { return base.Clone(); }
        public override bool Equals(object value)
        {
            var e = value as EncodingNoPreambleWrapper;
            if (e != null) return BaseEncoding.Equals(e.BaseEncoding);
            return BaseEncoding.Equals(value);
        }
        public override int GetHashCode() { return BaseEncoding.GetHashCode(); }
        public override string ToString() { return BaseEncoding.ToString(); }

        public override string BodyName { get { return BaseEncoding.BodyName; } }
        public override int CodePage { get { return BaseEncoding.CodePage; } }
        public override string EncodingName { get { return BaseEncoding.EncodingName; } }

        public override int GetByteCount(char[] chars) { return BaseEncoding.GetByteCount(chars); }
        public override int GetByteCount(string s) { return BaseEncoding.GetByteCount(s); }
        public override byte[] GetBytes(char[] chars) { return BaseEncoding.GetBytes(chars); }
        public override byte[] GetBytes(char[] chars, int index, int count) { return BaseEncoding.GetBytes(chars, index, count); }
        public override byte[] GetBytes(string s) { return BaseEncoding.GetBytes(s); }
        public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex) { return BaseEncoding.GetBytes(s, charIndex, charCount, bytes, byteIndex); }
        public override int GetCharCount(byte[] bytes) { return BaseEncoding.GetCharCount(bytes); }
        public override char[] GetChars(byte[] bytes) { return BaseEncoding.GetChars(bytes); }
        public override char[] GetChars(byte[] bytes, int index, int count) { return BaseEncoding.GetChars(bytes, index, count); }
        public override string GetString(byte[] bytes) { return BaseEncoding.GetString(bytes); }
        public override string GetString(byte[] bytes, int index, int count) { return BaseEncoding.GetString(bytes, index, count); }

        public override string HeaderName { get { return BaseEncoding.HeaderName; } }
        public override bool IsAlwaysNormalized(NormalizationForm form) { return BaseEncoding.IsAlwaysNormalized(form); }
        public override bool IsBrowserDisplay { get { return BaseEncoding.IsBrowserDisplay; } }
        public override bool IsBrowserSave { get { return BaseEncoding.IsBrowserSave; } }
        public override bool IsMailNewsDisplay { get { return BaseEncoding.IsMailNewsDisplay; } }
        public override bool IsMailNewsSave { get { return BaseEncoding.IsMailNewsSave; } }
        public override bool IsSingleByte { get { return BaseEncoding.IsSingleByte; } }
        public override string WebName { get { return BaseEncoding.WebName; } }
        public override int WindowsCodePage { get { return BaseEncoding.WindowsCodePage; } }
    }

    public static class TextEncoding
    {
        private static Encoding DefaultValue;
        public static Encoding Default
        {
            get
            {
                if (DefaultValue == null)
                {
                    DefaultValue = Encoding.Default;
                    try
                    {
                        if (DefaultValue.WindowsCodePage == 936)
                        {
                            DefaultValue = GB18030;
                        }
                    }
                    catch { }
                }
                return DefaultValue;
            }
            set { DefaultValue = value; }
        }

        private static Encoding WritingDefaultValue;
        public static Encoding WritingDefault
        {
            get
            {
                if (WritingDefaultValue == null)
                {
                    WritingDefaultValue = UTF16;
                }
                return WritingDefaultValue;
            }
            set { WritingDefaultValue = value; }
        }

        public static bool IsSameIntrinsic(Encoding Left, Encoding Right)
        {
            if (object.ReferenceEquals(Left, Right)) return true;
            if (Left == null || Right == null) return false;
            if (Left.WebName == Right.WebName) return true;
            if (Left.CodePage == Right.CodePage) return true;
            return false;
        }

        private static Encoding _ASCII;
        public static Encoding ASCII { get { if (_ASCII == null) _ASCII = Encoding.GetEncoding("ASCII", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _ASCII; } }

        private static Encoding _UTF8;
        public static Encoding UTF8 { get { if (_UTF8 == null) _UTF8 = Encoding.GetEncoding("UTF-8", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _UTF8; } }

        private static Encoding _UTF16;
        public static Encoding UTF16 { get { if (_UTF16 == null) _UTF16 = Encoding.GetEncoding("UTF-16", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _UTF16; } }

        private static Encoding _UTF16B;
        public static Encoding UTF16B { get { if (_UTF16B == null) _UTF16B = Encoding.GetEncoding("UTF-16BE", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _UTF16B; } }

        private static Encoding _UTF32;
        public static Encoding UTF32 { get { if (_UTF32 == null) _UTF32 = Encoding.GetEncoding("UTF-32", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _UTF32; } }

        private static Encoding _UTF32B;
        public static Encoding UTF32B { get { if (_UTF32B == null) _UTF32B = Encoding.GetEncoding("UTF-32BE", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _UTF32B; } }

        private static Encoding _GB18030;
        public static Encoding GB18030 { get { if (_GB18030 == null) _GB18030 = Encoding.GetEncoding("GB18030", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _GB18030; } }

        private static bool? _GB18030Available;
        public static bool GB18030Available
        {
            get
            {
                if (_GB18030Available == null)
                {
                    try
                    {
                        Encoding.GetEncoding("GB18030", new EncoderExceptionFallback(), new DecoderExceptionFallback());
                        _GB18030Available = true;
                    }
                    catch
                    {
                        _GB18030Available = false;
                    }
                }
                return _GB18030Available.Value;
            }
        }

        private static Encoding _GB2312;
        public static Encoding GB2312 { get { if (_GB2312 == null) _GB2312 = Encoding.GetEncoding("GB2312", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _GB2312; } }

        private static Encoding _Big5;
        public static Encoding Big5 { get { if (_Big5 == null) _Big5 = Encoding.GetEncoding("Big5", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _Big5; } }

        private static Encoding _ShiftJIS;
        public static Encoding ShiftJIS { get { if (_ShiftJIS == null) _ShiftJIS = Encoding.GetEncoding("Shift-JIS", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _ShiftJIS; } }

        private static Encoding _ISO8859_1;
        public static Encoding ISO8859_1 { get { if (_ISO8859_1 == null) _ISO8859_1 = Encoding.GetEncoding("ISO-8859-1", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _ISO8859_1; } }

        private static Encoding _Windows1252;
        public static Encoding Windows1252 { get { if (_Windows1252 == null) _Windows1252 = Encoding.GetEncoding("Windows-1252", new EncoderExceptionFallback(), new DecoderExceptionFallback()); return _Windows1252; } }

        public static Char32[] GetString32(this Encoding This, byte[] Bytes)
        {
            return String32.FromUTF16B(new string(This.GetChars(Bytes)));
        }

        public delegate R Mapping<D, R>(D d);
    }
}
