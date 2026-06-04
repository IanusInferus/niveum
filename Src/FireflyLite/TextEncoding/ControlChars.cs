using System;

namespace Firefly.TextEncoding
{
    public static class ControlChars
    {
        public static readonly Char32 Cr = 0xD;
        public static readonly Char32 Lf = 0xA;
        public static readonly string CrLf = (new Char32[] { 0xD, 0xA }).ToUTF16B();
        public static readonly Char32 Nul = 0x0;
        public static readonly Char32 Quote = 0x22;
    }
}
