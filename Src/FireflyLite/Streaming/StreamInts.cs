using System;
using System.Runtime.CompilerServices;

namespace Firefly.Streaming
{
    public static class ReadableStreamInts
    {
        public static sbyte ReadInt8(this IReadableStream This)
        {
            return DirectIntConvert.CUS(This.ReadByte());
        }
        public static short ReadInt16(this IReadableStream This)
        {
            short o;
            o = (short)This.ReadByte();
            o = (short)(o | (short)(This.ReadByte() << 8));
            return o;
        }
        public static int ReadInt32(this IReadableStream This)
        {
            int o;
            o = This.ReadByte();
            o = o | (This.ReadByte() << 8);
            o = o | (This.ReadByte() << 16);
            o = o | (This.ReadByte() << 24);
            return o;
        }
        public static long ReadInt64(this IReadableStream This)
        {
            long o;
            o = This.ReadByte();
            o = o | ((long)This.ReadByte() << 8);
            o = o | ((long)This.ReadByte() << 16);
            o = o | ((long)This.ReadByte() << 24);
            o = o | ((long)This.ReadByte() << 32);
            o = o | ((long)This.ReadByte() << 40);
            o = o | ((long)This.ReadByte() << 48);
            o = o | ((long)This.ReadByte() << 56);
            return o;
        }
        public static short ReadInt16B(this IReadableStream This)
        {
            short o;
            o = (short)((short)This.ReadByte() << 8);
            o = (short)(o | (short)This.ReadByte());
            return o;
        }
        public static int ReadInt32B(this IReadableStream This)
        {
            int o;
            o = This.ReadByte() << 24;
            o = o | (This.ReadByte() << 16);
            o = o | (This.ReadByte() << 8);
            o = o | This.ReadByte();
            return o;
        }
        public static long ReadInt64B(this IReadableStream This)
        {
            long o;
            o = (long)This.ReadByte() << 56;
            o = o | ((long)This.ReadByte() << 48);
            o = o | ((long)This.ReadByte() << 40);
            o = o | ((long)This.ReadByte() << 32);
            o = o | ((long)This.ReadByte() << 24);
            o = o | ((long)This.ReadByte() << 16);
            o = o | ((long)This.ReadByte() << 8);
            o = o | This.ReadByte();
            return o;
        }

        public static byte ReadUInt8(this IReadableStream This)
        {
            return This.ReadByte();
        }
        public static ushort ReadUInt16(this IReadableStream This) { return DirectIntConvert.CSU(This.ReadInt16()); }
        public static uint ReadUInt32(this IReadableStream This) { return DirectIntConvert.CSU(This.ReadInt32()); }
        public static ulong ReadUInt64(this IReadableStream This) { return DirectIntConvert.CSU(This.ReadInt64()); }
        public static ushort ReadUInt16B(this IReadableStream This) { return DirectIntConvert.CSU(This.ReadInt16B()); }
        public static uint ReadUInt32B(this IReadableStream This) { return DirectIntConvert.CSU(This.ReadInt32B()); }
        public static ulong ReadUInt64B(this IReadableStream This) { return DirectIntConvert.CSU(This.ReadInt64B()); }
    }

    public static class WritableStreamInts
    {
        public static void WriteInt8(this IWritableStream This, sbyte i)
        {
            This.WriteByte(DirectIntConvert.CSU(i));
        }
        public static void WriteInt16(this IWritableStream This, short i)
        {
            This.WriteByte((byte)(i & 0xFF));
            i = (short)(i >> 8);
            This.WriteByte((byte)(i & 0xFF));
        }
        public static void WriteInt32(this IWritableStream This, int i)
        {
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
        }
        public static void WriteInt64(this IWritableStream This, long i)
        {
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
            i = i >> 8;
            This.WriteByte((byte)(i & 0xFF));
        }
        public static void WriteInt16B(this IWritableStream This, short i)
        {
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 8) & 0xFF));
            This.WriteByte((byte)(i & 0xFF));
        }
        public static void WriteInt32B(this IWritableStream This, int i)
        {
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 24) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 16) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 8) & 0xFF));
            This.WriteByte((byte)(i & 0xFF));
        }
        public static void WriteInt64B(this IWritableStream This, long i)
        {
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 56) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 48) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 40) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 32) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 24) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 16) & 0xFF));
            This.WriteByte((byte)((DirectIntConvert.CSU(i) >> 8) & 0xFF));
            This.WriteByte((byte)(i & 0xFF));
        }

        public static void WriteUInt8(this IWritableStream This, byte b) { This.WriteByte(b); }
        public static void WriteUInt16(this IWritableStream This, ushort i) { This.WriteInt16(DirectIntConvert.CUS(i)); }
        public static void WriteUInt32(this IWritableStream This, uint i) { This.WriteInt32(DirectIntConvert.CUS(i)); }
        public static void WriteUInt64(this IWritableStream This, ulong i) { This.WriteInt64(DirectIntConvert.CUS(i)); }
        public static void WriteUInt16B(this IWritableStream This, ushort i) { This.WriteInt16B(DirectIntConvert.CUS(i)); }
        public static void WriteUInt32B(this IWritableStream This, uint i) { This.WriteInt32B(DirectIntConvert.CUS(i)); }
        public static void WriteUInt64B(this IWritableStream This, ulong i) { This.WriteInt64B(DirectIntConvert.CUS(i)); }
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
