using System;

namespace Firefly
{
    public static class DirectIntConvert
    {
        public static short CID(int i)
        {
            if ((i & 0x8000) != 0)
            {
                return unchecked((short)((i & 0xFFFF) | unchecked((int)0xFFFF0000)));
            }
            return (short)i;
        }
        public static int CID(long i)
        {
            if ((i & 0x80000000L) != 0)
            {
                return unchecked((int)((i & 0xFFFFFFFFL) | unchecked((long)0xFFFFFFFF00000000L)));
            }
            return (int)i;
        }
        public static int EID(short i)
        {
            if ((i & unchecked((short)0x8000)) != 0)
            {
                return ((int)(i & 0x7FFF)) | 0x8000;
            }
            return i;
        }
        public static long EID(int i)
        {
            if ((i & unchecked((int)0x80000000)) != 0)
            {
                return ((long)(i & 0x7FFFFFFF)) | 0x80000000L;
            }
            return i;
        }
        public static byte CSU(sbyte i)
        {
            if ((i & 0x80) != 0)
            {
                return (byte)((byte)(i & 0x7F) | (byte)0x80);
            }
            return (byte)i;
        }
        public static ushort CSU(short i)
        {
            if ((i & unchecked((short)0x8000)) != 0)
            {
                return (ushort)((ushort)(i & 0x7FFF) | (ushort)0x8000);
            }
            return (ushort)i;
        }
        public static uint CSU(int i)
        {
            if ((i & unchecked((int)0x80000000)) != 0)
            {
                return ((uint)(i & 0x7FFFFFFF)) | 0x80000000U;
            }
            return (uint)i;
        }
        public static ulong CSU(long i)
        {
            if ((i & unchecked((long)0x8000000000000000L)) != 0)
            {
                return ((ulong)(i & 0x7FFFFFFFFFFFFFFFL)) | 0x8000000000000000UL;
            }
            return (ulong)i;
        }
        public static sbyte CUS(byte i)
        {
            if ((i & 0x80) != 0)
            {
                return unchecked((sbyte)((sbyte)(i & 0x7F) | unchecked((sbyte)0x80)));
            }
            return (sbyte)i;
        }
        public static short CUS(ushort i)
        {
            if ((i & 0x8000) != 0)
            {
                return unchecked((short)((short)(i & 0x7FFF) | unchecked((short)0x8000)));
            }
            return (short)i;
        }
        public static int CUS(uint i)
        {
            if ((i & 0x80000000U) != 0)
            {
                return unchecked((int)((int)(i & 0x7FFFFFFFU) | unchecked((int)0x80000000)));
            }
            return (int)i;
        }
        public static long CUS(ulong i)
        {
            if ((i & 0x8000000000000000UL) != 0)
            {
                return unchecked((long)((long)(i & 0x7FFFFFFFFFFFFFFFUL) | unchecked((long)0x8000000000000000L)));
            }
            return (long)i;
        }
    }
}
