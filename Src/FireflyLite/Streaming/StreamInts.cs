using System;
using System.Runtime.CompilerServices;

namespace Firefly.Streaming
{
    public static class ReadableStreamInts
    {
        public static sbyte ReadInt8(this IReadableStream This)
        {
            return unchecked((sbyte)(This.ReadByte()));
        }
        public static short ReadInt16(this IReadableStream This)
        {
            unchecked
            {
                short o;
                o = (short)((short)(This.ReadByte()) & (short)(0xFF));
                o = (short)(o | (short)(((short)(This.ReadByte()) & 0xFF) << 8));
                return o;
            }
        }
        public static int ReadInt32(this IReadableStream This)
        {
            unchecked
            {
                int o;
                o = (int)(This.ReadByte()) & 0xFF;
                o = o | (((int)(This.ReadByte()) & 0xFF) << 8);
                o = o | (((int)(This.ReadByte()) & 0xFF) << 16);
                o = o | (((int)(This.ReadByte()) & 0xFF) << 24);
                return o;
            }
        }
        public static long ReadInt64(this IReadableStream This)
        {
            unchecked
            {
                long o;
                o = (long)(This.ReadByte()) & 0xFF;
                o = o | (((long)(This.ReadByte()) & 0xFF) << 8);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 16);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 24);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 32);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 40);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 48);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 56);
                return o;
            }
        }
        public static short ReadInt16B(this IReadableStream This)
        {
            unchecked
            {
                short o;
                o = (short)(((short)(This.ReadByte()) & 0xFF) << 8);
                o = (short)(o | (short)((short)(This.ReadByte()) & (short)(0xFF)));
                return o;
            }
        }
        public static int ReadInt32B(this IReadableStream This)
        {
            unchecked
            {
                int o;
                o = ((int)(This.ReadByte()) & 0xFF) << 24;
                o = o | (((int)(This.ReadByte()) & 0xFF) << 16);
                o = o | (((int)(This.ReadByte()) & 0xFF) << 8);
                o = o | ((int)(This.ReadByte()) & 0xFF);
                return o;
            }
        }
        public static long ReadInt64B(this IReadableStream This)
        {
            unchecked
            {
                long o;
                o = ((long)(This.ReadByte()) & 0xFF) << 56;
                o = o | (((long)(This.ReadByte()) & 0xFF) << 48);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 40);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 32);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 24);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 16);
                o = o | (((long)(This.ReadByte()) & 0xFF) << 8);
                o = o | ((long)(This.ReadByte()) & 0xFF);
                return o;
            }
        }

        public static byte ReadUInt8(this IReadableStream This)
        {
            return This.ReadByte();
        }
        public static ushort ReadUInt16(this IReadableStream This)
        {
            unchecked
            {
                ushort o;
                o = (ushort)((ushort)(This.ReadByte()) & (ushort)(0xFF));
                o = (ushort)(o | (ushort)(((ushort)(This.ReadByte()) & 0xFF) << 8));
                return o;
            }
        }
        public static uint ReadUInt32(this IReadableStream This)
        {
            unchecked
            {
                uint o;
                o = (uint)(This.ReadByte()) & 0xFFU;
                o = o | (((uint)(This.ReadByte()) & 0xFFU) << 8);
                o = o | (((uint)(This.ReadByte()) & 0xFFU) << 16);
                o = o | (((uint)(This.ReadByte()) & 0xFFU) << 24);
                return o;
            }
        }
        public static ulong ReadUInt64(this IReadableStream This)
        {
            unchecked
            {
                ulong o;
                o = (ulong)(This.ReadByte()) & 0xFFUL;
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 8);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 16);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 24);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 32);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 40);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 48);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 56);
                return o;
            }
        }
        public static ushort ReadUInt16B(this IReadableStream This)
        {
            unchecked
            {
                ushort o;
                o = (ushort)(((ushort)(This.ReadByte()) & 0xFF) << 8);
                o = (ushort)(o | (ushort)((ushort)(This.ReadByte()) & (ushort)(0xFF)));
                return o;
            }
        }
        public static uint ReadUInt32B(this IReadableStream This)
        {
            unchecked
            {
                uint o;
                o = ((uint)(This.ReadByte()) & 0xFFU) << 24;
                o = o | (((uint)(This.ReadByte()) & 0xFFU) << 16);
                o = o | (((uint)(This.ReadByte()) & 0xFFU) << 8);
                o = o | ((uint)(This.ReadByte()) & 0xFFU);
                return o;
            }
        }
        public static ulong ReadUInt64B(this IReadableStream This)
        {
            unchecked
            {
                ulong o;
                o = ((ulong)(This.ReadByte()) & 0xFFUL) << 56;
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 48);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 40);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 32);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 24);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 16);
                o = o | (((ulong)(This.ReadByte()) & 0xFFUL) << 8);
                o = o | ((ulong)(This.ReadByte()) & 0xFFUL);
                return o;
            }
        }
    }

    public static class WritableStreamInts
    {
        public static void WriteInt8(this IWritableStream This, sbyte v)
        {
            This.WriteByte(unchecked((byte)(v)));
        }
        public static void WriteInt16(this IWritableStream This, short v)
        {
            unchecked
            {
                This.WriteByte((byte)(v & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
            }
        }
        public static void WriteInt32(this IWritableStream This, int v)
        {
            unchecked
            {
                This.WriteByte((byte)(v & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 24) & 0xFF));
            }
        }
        public static void WriteInt64(this IWritableStream This, long v)
        {
            unchecked
            {
                This.WriteByte((byte)(v & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 24) & 0xFF));
                This.WriteByte((byte)((v >> 32) & 0xFF));
                This.WriteByte((byte)((v >> 40) & 0xFF));
                This.WriteByte((byte)((v >> 48) & 0xFF));
                This.WriteByte((byte)((v >> 56) & 0xFF));
            }
        }
        public static void WriteInt16B(this IWritableStream This, short v)
        {
            unchecked
            {
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)(v & 0xFF));
            }
        }
        public static void WriteInt32B(this IWritableStream This, int v)
        {
            unchecked
            {
                This.WriteByte((byte)((v >> 24) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)(v & 0xFF));
            }
        }
        public static void WriteInt64B(this IWritableStream This, long v)
        {
            unchecked
            {
                This.WriteByte((byte)((v >> 56) & 0xFF));
                This.WriteByte((byte)((v >> 48) & 0xFF));
                This.WriteByte((byte)((v >> 40) & 0xFF));
                This.WriteByte((byte)((v >> 32) & 0xFF));
                This.WriteByte((byte)((v >> 24) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)(v & 0xFF));
            }
        }

        public static void WriteUInt8(this IWritableStream This, byte v) { This.WriteByte(v); }
        public static void WriteUInt16(this IWritableStream This, ushort v)
        {
            unchecked
            {
                This.WriteByte((byte)(v & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
            }
        }
        public static void WriteUInt32(this IWritableStream This, uint v)
        {
            unchecked
            {
                This.WriteByte((byte)(v & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 24) & 0xFF));
            }
        }
        public static void WriteUInt64(this IWritableStream This, ulong v)
        {
            unchecked
            {
                This.WriteByte((byte)(v & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 24) & 0xFF));
                This.WriteByte((byte)((v >> 32) & 0xFF));
                This.WriteByte((byte)((v >> 40) & 0xFF));
                This.WriteByte((byte)((v >> 48) & 0xFF));
                This.WriteByte((byte)((v >> 56) & 0xFF));
            }
        }
        public static void WriteUInt16B(this IWritableStream This, ushort v)
        {
            unchecked
            {
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)(v & 0xFF));
            }
        }
        public static void WriteUInt32B(this IWritableStream This, uint v)
        {
            unchecked
            {
                This.WriteByte((byte)((v >> 24) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)(v & 0xFF));
            }
        }
        public static void WriteUInt64B(this IWritableStream This, ulong v)
        {
            unchecked
            {
                This.WriteByte((byte)((v >> 56) & 0xFF));
                This.WriteByte((byte)((v >> 48) & 0xFF));
                This.WriteByte((byte)((v >> 40) & 0xFF));
                This.WriteByte((byte)((v >> 32) & 0xFF));
                This.WriteByte((byte)((v >> 24) & 0xFF));
                This.WriteByte((byte)((v >> 16) & 0xFF));
                This.WriteByte((byte)((v >> 8) & 0xFF));
                This.WriteByte((byte)(v & 0xFF));
            }
        }
    }

    public static class ReadableSeekableStreamInts
    {
        public static byte PeekByte(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadByte(); }
            finally { This.Position = HoldPosition; }
        }

        public static sbyte PeekInt8(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt8(); }
            finally { This.Position = HoldPosition; }
        }
        public static short PeekInt16(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt16(); }
            finally { This.Position = HoldPosition; }
        }
        public static int PeekInt32(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt32(); }
            finally { This.Position = HoldPosition; }
        }
        public static long PeekInt64(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt64(); }
            finally { This.Position = HoldPosition; }
        }
        public static short PeekInt16B(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt16B(); }
            finally { This.Position = HoldPosition; }
        }
        public static int PeekInt32B(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt32B(); }
            finally { This.Position = HoldPosition; }
        }
        public static long PeekInt64B(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadInt64B(); }
            finally { This.Position = HoldPosition; }
        }

        public static byte PeekUInt8(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt8(); }
            finally { This.Position = HoldPosition; }
        }
        public static ushort PeekUInt16(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt16(); }
            finally { This.Position = HoldPosition; }
        }
        public static uint PeekUInt32(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt32(); }
            finally { This.Position = HoldPosition; }
        }
        public static ulong PeekUInt64(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt64(); }
            finally { This.Position = HoldPosition; }
        }
        public static ushort PeekUInt16B(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt16B(); }
            finally { This.Position = HoldPosition; }
        }
        public static uint PeekUInt32B(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt32B(); }
            finally { This.Position = HoldPosition; }
        }
        public static ulong PeekUInt64B(this IReadableSeekableStream This)
        {
            var HoldPosition = This.Position;
            try { return This.ReadUInt64B(); }
            finally { This.Position = HoldPosition; }
        }
    }
}
