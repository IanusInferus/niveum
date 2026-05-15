using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Firefly.Streaming
{
    public static class ReadableStreamFloats
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct SingleInt32
        {
            [FieldOffset(0)]
            public float Float32Value;
            [FieldOffset(0)]
            public int Int32Value;
        }
        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleInt64
        {
            [FieldOffset(0)]
            public double Float64Value;
            [FieldOffset(0)]
            public long Int64Value;
        }

        public static float ReadFloat32(this IReadableStream This)
        {
            SingleInt32 a = default;
            a.Int32Value = This.ReadInt32();
            return a.Float32Value;
        }
        public static double ReadFloat64(this IReadableStream This)
        {
            DoubleInt64 a = default;
            a.Int64Value = This.ReadInt64();
            return a.Float64Value;
        }
        public static float ReadFloat32B(this IReadableStream This)
        {
            SingleInt32 a = default;
            a.Int32Value = This.ReadInt32B();
            return a.Float32Value;
        }
        public static double ReadFloat64B(this IReadableStream This)
        {
            DoubleInt64 a = default;
            a.Int64Value = This.ReadInt64B();
            return a.Float64Value;
        }
    }

    public static class WritableStreamFloats
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct SingleInt32
        {
            [FieldOffset(0)]
            public float Float32Value;
            [FieldOffset(0)]
            public int Int32Value;
        }
        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleInt64
        {
            [FieldOffset(0)]
            public double Float64Value;
            [FieldOffset(0)]
            public long Int64Value;
        }

        public static void WriteFloat32(this IWritableStream This, float f)
        {
            SingleInt32 a = default;
            a.Float32Value = f;
            This.WriteInt32(a.Int32Value);
        }
        public static void WriteFloat64(this IWritableStream This, double f)
        {
            DoubleInt64 a = default;
            a.Float64Value = f;
            This.WriteInt64(a.Int64Value);
        }
        public static void WriteFloat32B(this IWritableStream This, float f)
        {
            SingleInt32 a = default;
            a.Float32Value = f;
            This.WriteInt32B(a.Int32Value);
        }
        public static void WriteFloat64B(this IWritableStream This, double f)
        {
            DoubleInt64 a = default;
            a.Float64Value = f;
            This.WriteInt64B(a.Int64Value);
        }
    }
}
