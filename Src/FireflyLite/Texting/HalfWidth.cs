using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Firefly.TextEncoding;

namespace Firefly.Texting
{
    public static class HalfWidth
    {
        private static readonly Indexer Ranges;

        static HalfWidth()
        {
            var l = new List<Range>();

            l.Add(new Range(0x20A9, 0x20A9));
            l.Add(new Range(0xFF61, 0xFF64));
            l.Add(new Range(0x20, 0x7E));
            l.Add(new Range(0xA2, 0xA3));
            l.Add(new Range(0xA5, 0xA6));
            l.Add(new Range(0xAC, 0xAC));
            l.Add(new Range(0xAF, 0xAF));
            l.Add(new Range(0x0, 0x1F));
            l.Add(new Range(0x7F, 0xA0));
            l.Add(new Range(0xA9, 0xA9));
            l.Add(new Range(0xAB, 0xAB));
            l.Add(new Range(0xAE, 0xAE));
            l.Add(new Range(0xB5, 0xB5));
            l.Add(new Range(0xBB, 0xBB));
            l.Add(new Range(0xC0, 0xC5));
            l.Add(new Range(0xC7, 0xCF));
            l.Add(new Range(0xD1, 0xD6));
            l.Add(new Range(0xD9, 0xDD));
            l.Add(new Range(0xE2, 0xE5));
            l.Add(new Range(0xE7, 0xE7));
            l.Add(new Range(0xEB, 0xEB));
            l.Add(new Range(0xEE, 0xEF));
            l.Add(new Range(0xF1, 0xF1));
            l.Add(new Range(0xF4, 0xF6));
            l.Add(new Range(0xFB, 0xFB));
            l.Add(new Range(0xFD, 0xFD));
            l.Add(new Range(0xFF, 0x100));
            l.Add(new Range(0x102, 0x110));
            l.Add(new Range(0x112, 0x112));
            l.Add(new Range(0x114, 0x11A));
            l.Add(new Range(0x11C, 0x125));
            l.Add(new Range(0x128, 0x12A));
            l.Add(new Range(0x12C, 0x130));
            l.Add(new Range(0x134, 0x137));
            l.Add(new Range(0x139, 0x13E));
            l.Add(new Range(0x143, 0x143));
            l.Add(new Range(0x145, 0x147));
            l.Add(new Range(0x14C, 0x14C));
            l.Add(new Range(0x14E, 0x151));
            l.Add(new Range(0x154, 0x165));
            l.Add(new Range(0x168, 0x16A));
            l.Add(new Range(0x16C, 0x1CD));
            l.Add(new Range(0x1CF, 0x1CF));
            l.Add(new Range(0x1D1, 0x1D1));
            l.Add(new Range(0x1D3, 0x1D3));
            l.Add(new Range(0x1D5, 0x1D5));
            l.Add(new Range(0x1D7, 0x1D7));
            l.Add(new Range(0x1D9, 0x1D9));
            l.Add(new Range(0x1DB, 0x1DB));
            l.Add(new Range(0x1DD, 0x250));
            l.Add(new Range(0x252, 0x260));
            l.Add(new Range(0x262, 0x2A8));
            l.Add(new Range(0x2B0, 0x2C6));
            l.Add(new Range(0x2C8, 0x2C8));
            l.Add(new Range(0x2CC, 0x2CC));
            l.Add(new Range(0x2CE, 0x2CF));
            l.Add(new Range(0x2D1, 0x2D7));
            l.Add(new Range(0x2DC, 0x2DC));
            l.Add(new Range(0x2DE, 0x2DE));
            l.Add(new Range(0x2E0, 0x2E9));
            l.Add(new Range(0x374, 0x390));
            l.Add(new Range(0x3AA, 0x3B0));
            l.Add(new Range(0x3C2, 0x3C2));
            l.Add(new Range(0x3CA, 0x3EF));
            l.Add(new Range(0x400, 0x400));
            l.Add(new Range(0x402, 0x40F));
            l.Add(new Range(0x450, 0x450));
            l.Add(new Range(0x452, 0x486));
            l.Add(new Range(0x490, 0x4F9));
            l.Add(new Range(0x531, 0x556));
            l.Add(new Range(0x559, 0x55F));
            l.Add(new Range(0x561, 0x587));
            l.Add(new Range(0x589, 0x589));
            l.Add(new Range(0x591, 0x5F4));
            l.Add(new Range(0x60C, 0x6F9));
            l.Add(new Range(0x901, 0x970));
            l.Add(new Range(0x981, 0x9FA));
            l.Add(new Range(0xA02, 0xA74));
            l.Add(new Range(0xA81, 0xAEF));
            l.Add(new Range(0xB01, 0xB70));
            l.Add(new Range(0xB82, 0xBF2));
            l.Add(new Range(0xC01, 0xC6F));
            l.Add(new Range(0xC82, 0xCEF));
            l.Add(new Range(0xD02, 0xD6F));
            l.Add(new Range(0xE01, 0xE5B));
            l.Add(new Range(0xE81, 0xEDD));
            l.Add(new Range(0xF00, 0xFB9));
            l.Add(new Range(0x10A0, 0x10F6));
            l.Add(new Range(0x10FB, 0x10FB));
            l.Add(new Range(0x1E00, 0x1EF9));
            l.Add(new Range(0x1F00, 0x1FFE));
            l.Add(new Range(0x2000, 0x200F));
            l.Add(new Range(0x2011, 0x2012));
            l.Add(new Range(0x2017, 0x2017));
            l.Add(new Range(0x201A, 0x201B));
            l.Add(new Range(0x201E, 0x201F));
            l.Add(new Range(0x2022, 0x2024));
            l.Add(new Range(0x2028, 0x202E));

            Ranges = new Indexer(l);
        }

        public static bool IsHalfWidth(this Char32 c)
        {
            return Ranges.Contain(c.Value);
        }
    }
}
