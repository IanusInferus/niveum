using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Firefly.TextEncoding;

namespace Firefly.Streaming
{
    public static class ReadableStreamComplex
    {
        public static void Read(this IReadableStream This, byte[] Buffer)
        {
            This.Read(Buffer, 0, Buffer.Length);
        }
        public static byte[] Read(this IReadableStream This, int Count)
        {
            byte[] d = new byte[Count];
            This.Read(d, 0, Count);
            return d;
        }
        public static void Skip(this IReadableStream This, int Count)
        {
            for (int n = 0; n < Count; n++)
            {
                This.ReadByte();
            }
        }

        public static void ReadToStream(this IReadableStream This, IWritableStream s, long Count)
        {
            if (Count <= 0) return;
            byte[] Buffer = new byte[(int)NumericOperations.Min(Count, 4 * (1 << 20))];
            for (long n = 0; n <= Count - Buffer.Length; n += Buffer.Length)
            {
                This.Read(Buffer);
                s.Write(Buffer);
            }
            int LeftLength = (int)(Count % Buffer.Length);
            This.Read(Buffer, 0, LeftLength);
            s.Write(Buffer, 0, LeftLength);
        }

        public static string ReadString(this IReadableStream This, int Count, Encoding Encoding)
        {
            var Bytes = new List<byte>();
            for (int n = 0; n < Count; n++)
            {
                var b = This.ReadByte();
                if (b == ControlChars.Nul.Value)
                {
                    for (int k = 0; k < (Count - 1 - n); k++)
                    {
                        This.ReadByte();
                    }
                    break;
                }
                else
                {
                    Bytes.Add(b);
                }
            }
            return new string(Encoding.GetChars(Bytes.ToArray()));
        }
        public static string ReadStringWithNull(this IReadableStream This, int Count, Encoding Encoding)
        {
            return new string(Encoding.GetChars(This.Read(Count)));
        }
        public static string ReadSimpleString(this IReadableStream This, int Count)
        {
            return This.ReadString(Count, Firefly.TextEncoding.TextEncoding.ASCII);
        }
        public static string ReadSimpleStringWithNull(this IReadableStream This, int Count)
        {
            return This.ReadStringWithNull(Count, Firefly.TextEncoding.TextEncoding.ASCII);
        }
    }

    public static class WritableStreamComplex
    {
        public static void Write(this IWritableStream This, byte[] Buffer)
        {
            This.Write(Buffer, 0, Buffer.Length);
        }

        public static void WriteFromStream(this IWritableStream This, IReadableStream s, long Count)
        {
            if (Count <= 0) return;
            byte[] Buffer = new byte[(int)NumericOperations.Min(Count, 4 * (1 << 20))];
            for (long n = 0; n <= Count - Buffer.Length; n += Buffer.Length)
            {
                s.Read(Buffer);
                This.Write(Buffer);
            }
            int LeftLength = (int)(Count % Buffer.Length);
            s.Read(Buffer, 0, LeftLength);
            This.Write(Buffer, 0, LeftLength);
        }

        public static void WriteString(this IWritableStream This, string s, int Count, Encoding Encoding)
        {
            if (s == "")
            {
                for (int n = 0; n < Count; n++)
                {
                    This.WriteByte(0);
                }
            }
            else
            {
                var Bytes = Encoding.GetBytes(s);
                if (Bytes.Length > Count) throw new InvalidDataException();
                This.Write(Bytes);
                for (int n = Bytes.Length; n < Count; n++)
                {
                    This.WriteByte(0);
                }
            }
        }
        public static void WriteSimpleString(this IWritableStream This, string s, int Count)
        {
            This.WriteString(s, Count, Firefly.TextEncoding.TextEncoding.ASCII);
        }
        public static void WriteSimpleString(this IWritableStream This, string s)
        {
            This.WriteSimpleString(s, s.Length);
        }
    }

    public static class ReadableSeekableStreamComplex
    {
        public static string PeekSimpleString(this IReadableSeekableStream This, int Count)
        {
            var HoldPosition = This.Position;
            try { return This.ReadSimpleString(Count); }
            finally { This.Position = HoldPosition; }
        }
    }
}
