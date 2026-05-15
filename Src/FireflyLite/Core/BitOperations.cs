using System;
using System.Runtime.CompilerServices;

namespace Firefly
{
    public static class BitOperations
    {
        public static byte SHL(this byte This, int n)
        {
            if (n >= 8) return 0;
            if (n < 0) return SHR(This, -n);
            return unchecked((byte)(This << n));
        }
        public static byte SHR(this byte This, int n)
        {
            if (n >= 8) return 0;
            if (n < 0) return SHL(This, -n);
            return (byte)(This >> n);
        }

        public static ushort SHL(this ushort This, int n)
        {
            if (n >= 16) return 0;
            if (n < 0) return SHR(This, -n);
            return unchecked((ushort)(This << n));
        }
        public static ushort SHR(this ushort This, int n)
        {
            if (n >= 16) return 0;
            if (n < 0) return SHL(This, -n);
            return (ushort)(This >> n);
        }

        public static uint SHL(this uint This, int n)
        {
            if (n >= 32) return 0;
            if (n < 0) return SHR(This, -n);
            return unchecked(This << n);
        }
        public static uint SHR(this uint This, int n)
        {
            if (n >= 32) return 0;
            if (n < 0) return SHL(This, -n);
            return This >> n;
        }

        public static ulong SHL(this ulong This, int n)
        {
            if (n >= 64) return 0;
            if (n < 0) return SHR(This, -n);
            return unchecked(This << n);
        }
        public static ulong SHR(this ulong This, int n)
        {
            if (n >= 64) return 0;
            if (n < 0) return SHL(This, -n);
            return This >> n;
        }

        public static sbyte SAL(this sbyte This, int n)
        {
            if (n >= 8) return 0;
            if (n < 0) return SAR(This, -n);
            return unchecked((sbyte)(This << n));
        }
        public static sbyte SAR(this sbyte This, int n)
        {
            if (n >= 8)
            {
                if ((This & 0x80) != 0) return -1;
                return 0;
            }
            if (n < 0) return SAL(This, -n);
            return (sbyte)(This >> n);
        }

        public static short SAL(this short This, int n)
        {
            if (n >= 16) return 0;
            if (n < 0) return SAR(This, -n);
            return unchecked((short)(This << n));
        }
        public static short SAR(this short This, int n)
        {
            if (n >= 16)
            {
                if ((This & unchecked((short)0x8000)) != 0) return -1;
                return 0;
            }
            if (n < 0) return SAL(This, -n);
            return (short)(This >> n);
        }

        public static int SAL(this int This, int n)
        {
            if (n >= 32) return 0;
            if (n < 0) return SAR(This, -n);
            return unchecked(This << n);
        }
        public static int SAR(this int This, int n)
        {
            if (n >= 32)
            {
                if ((This & unchecked((int)0x80000000)) != 0) return -1;
                return 0;
            }
            if (n < 0) return SAL(This, -n);
            return This >> n;
        }

        public static long SAL(this long This, int n)
        {
            if (n >= 64) return 0;
            if (n < 0) return SAR(This, -n);
            return unchecked(This << n);
        }
        public static long SAR(this long This, int n)
        {
            if (n >= 64)
            {
                if ((This & unchecked((long)0x8000000000000000L)) != 0) return -1;
                return 0;
            }
            if (n < 0) return SAL(This, -n);
            return This >> n;
        }


        public static byte Bits(this byte This, int U, int L)
        {
            int NumBits = U - L + 1;
            byte Mask;
            if (NumBits <= 0)
            {
                Mask = 0;
            }
            else if (NumBits >= 8)
            {
                Mask = byte.MaxValue;
            }
            else
            {
                Mask = (byte)(((byte)1).SHL(NumBits) - (byte)1);
            }
            return (byte)(This.SHR(L) & Mask);
        }

        public static ushort Bits(this ushort This, int U, int L)
        {
            int NumBits = U - L + 1;
            ushort Mask;
            if (NumBits <= 0)
            {
                Mask = 0;
            }
            else if (NumBits >= 16)
            {
                Mask = ushort.MaxValue;
            }
            else
            {
                Mask = (ushort)(((ushort)1).SHL(NumBits) - (ushort)1);
            }
            return (ushort)(This.SHR(L) & Mask);
        }

        public static uint Bits(this uint This, int U, int L)
        {
            int NumBits = U - L + 1;
            uint Mask;
            if (NumBits <= 0)
            {
                Mask = 0;
            }
            else if (NumBits >= 32)
            {
                Mask = uint.MaxValue;
            }
            else
            {
                Mask = 1U.SHL(NumBits) - 1U;
            }
            return This.SHR(L) & Mask;
        }

        public static ulong Bits(this ulong This, int U, int L)
        {
            int NumBits = U - L + 1;
            ulong Mask;
            if (NumBits <= 0)
            {
                Mask = 0;
            }
            else if (NumBits >= 64)
            {
                Mask = ulong.MaxValue;
            }
            else
            {
                Mask = 1UL.SHL(NumBits) - 1UL;
            }
            return This.SHR(L) & Mask;
        }

        public static sbyte Bits(this sbyte This, int U, int L)
        {
            return DirectIntConvert.CUS(DirectIntConvert.CSU(This).Bits(U, L));
        }
        public static short Bits(this short This, int U, int L)
        {
            return DirectIntConvert.CUS(DirectIntConvert.CSU(This).Bits(U, L));
        }
        public static int Bits(this int This, int U, int L)
        {
            return DirectIntConvert.CUS(DirectIntConvert.CSU(This).Bits(U, L));
        }
        public static long Bits(this long This, int U, int L)
        {
            return DirectIntConvert.CUS(DirectIntConvert.CSU(This).Bits(U, L));
        }


        public static bool Bit(this byte This, int B)
        {
            return (This.SHR(B) & (byte)1) != 0;
        }
        public static bool Bit(this ushort This, int B)
        {
            return (This.SHR(B) & 1) != 0;
        }
        public static bool Bit(this uint This, int B)
        {
            return (This.SHR(B) & 1U) != 0;
        }
        public static bool Bit(this ulong This, int B)
        {
            return (This.SHR(B) & 1UL) != 0;
        }
        public static bool Bit(this sbyte This, int B)
        {
            return (This.SAR(B) & (sbyte)1) != 0;
        }
        public static bool Bit(this short This, int B)
        {
            return (This.SAR(B) & (short)1) != 0;
        }
        public static bool Bit(this int This, int B)
        {
            return (This.SAR(B) & 1) != 0;
        }
        public static bool Bit(this long This, int B)
        {
            return (This.SAR(B) & 1L) != 0;
        }


        public static byte ConcatBits(this byte This, byte Value, int Width)
        {
            return (byte)(This.SHL(Width) | Value.Bits(Width - 1, 0));
        }
        public static ushort ConcatBits(this ushort This, ushort Value, int Width)
        {
            return (ushort)(This.SHL(Width) | Value.Bits(Width - 1, 0));
        }
        public static uint ConcatBits(this uint This, uint Value, int Width)
        {
            return This.SHL(Width) | Value.Bits(Width - 1, 0);
        }
        public static ulong ConcatBits(this ulong This, ulong Value, int Width)
        {
            return This.SHL(Width) | Value.Bits(Width - 1, 0);
        }
        public static sbyte ConcatBits(this sbyte This, sbyte Value, int Width)
        {
            return (sbyte)(This.SAL(Width) | Value.Bits(Width - 1, 0));
        }
        public static short ConcatBits(this short This, short Value, int Width)
        {
            return (short)(This.SAL(Width) | Value.Bits(Width - 1, 0));
        }
        public static int ConcatBits(this int This, int Value, int Width)
        {
            return This.SAL(Width) | Value.Bits(Width - 1, 0);
        }
        public static long ConcatBits(this long This, long Value, int Width)
        {
            return This.SAL(Width) | Value.Bits(Width - 1, 0);
        }


        public static int ConcatBits(byte H, int HW, byte S, int SW, byte T, int TW, byte Q, int QW)
        {
            int HPart = H.Bits(HW - 1, 0);
            int SPart = S.Bits(SW - 1, 0);
            int TPart = T.Bits(TW - 1, 0);
            int QPart = Q.Bits(QW - 1, 0);
            return HPart.SAL(SW + TW + QW) | SPart.SAL(TW + QW) | TPart.SAL(QW) | QPart;
        }
        public static int ConcatBits(byte H, int HW, byte S, int SW, byte T, int TW)
        {
            int HPart = H.Bits(HW - 1, 0);
            int SPart = S.Bits(SW - 1, 0);
            int TPart = T.Bits(TW - 1, 0);
            return HPart.SAL(SW + TW) | SPart.SAL(TW) | TPart;
        }
        public static int ConcatBits(byte H, int HW, byte S, int SW)
        {
            int HPart = H.Bits(HW - 1, 0);
            int SPart = S.Bits(SW - 1, 0);
            return HPart.SAL(SW) | SPart;
        }
        public static int ConcatBits(int H, int HW, int S, int SW)
        {
            int HPart = H.Bits(HW - 1, 0);
            int SPart = S.Bits(SW - 1, 0);
            return HPart.SAL(SW) | SPart;
        }

        public static void SplitBits(out byte H, int HW, out byte S, int SW, out byte T, int TW, out byte Q, int QW, int Value)
        {
            H = (byte)(Value.SAR(SW + TW + QW) & (1.SAL(HW) - 1));
            S = (byte)(Value.SAR(TW + QW) & (1.SAL(SW) - 1));
            T = (byte)(Value.SAR(QW) & (1.SAL(TW) - 1));
            Q = (byte)(Value & (1.SAL(QW) - 1));
        }
        public static void SplitBits(out byte H, int HW, out byte S, int SW, out byte T, int TW, int Value)
        {
            H = (byte)(Value.SAR(SW + TW) & (1.SAL(HW) - 1));
            S = (byte)(Value.SAR(TW) & (1.SAL(SW) - 1));
            T = (byte)(Value & (1.SAL(TW) - 1));
        }
        public static void SplitBits(out byte H, int HW, out byte S, int SW, int Value)
        {
            H = (byte)(Value.SAR(SW) & (1.SAL(HW) - 1));
            S = (byte)(Value & (1.SAL(SW) - 1));
        }
        public static void SplitBits(out int H, int HW, out int S, int SW, int Value)
        {
            H = Value.SAR(SW) & (1.SAL(HW) - 1);
            S = Value & (1.SAL(SW) - 1);
        }
    }
}
