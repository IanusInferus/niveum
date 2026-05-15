using System;
using System.IO;
using System.Text;
using Firefly.Streaming;
using Firefly.TextEncoding;

namespace Firefly.Texting
{
    public sealed class Txt
    {
        private Txt() { }

        public static Encoding GetEncodingByBOM(NewReadingStreamPasser sp)
        {
            var s = sp.GetStream();
            if (s.Length >= 4)
            {
                s.Position = 0;
                int BOM = s.ReadInt32B();
                if (BOM == unchecked((int)0xFFFE0000)) return Firefly.TextEncoding.TextEncoding.UTF32;
                if (BOM == 0xFEFF) return Firefly.TextEncoding.TextEncoding.UTF32B;
                if (BOM == unchecked((int)0x84319533)) return Firefly.TextEncoding.TextEncoding.GB18030;
            }
            if (s.Length >= 3)
            {
                s.Position = 0;
                int BOM = s.ReadUInt16B();
                BOM = (BOM << 8) | s.ReadByte();
                if (BOM == 0xEFBBBF) return Firefly.TextEncoding.TextEncoding.UTF8;
            }
            if (s.Length >= 2)
            {
                s.Position = 0;
                ushort BOM = s.ReadUInt16B();
                if (BOM == 0xFFFE) return Firefly.TextEncoding.TextEncoding.UTF16;
                if (BOM == 0xFEFF) return Firefly.TextEncoding.TextEncoding.UTF16B;
            }
            return null;
        }
        public static Encoding GetEncodingByBOM(string Path)
        {
            using (var s = Streams.OpenResizable(Path))
            {
                return GetEncodingByBOM(s.AsNewReading());
            }
        }
        public static Encoding GetEncoding(NewReadingStreamPasser sp, Encoding DefaultEncoding)
        {
            var Encoding = GetEncodingByBOM(sp);
            if (Encoding != null) return Encoding;
            return DefaultEncoding;
        }
        public static Encoding GetEncoding(string Path, Encoding DefaultEncoding)
        {
            using (var s = Streams.OpenReadable(Path))
            {
                return GetEncoding(s.AsNewReading(), DefaultEncoding);
            }
        }
        public static Encoding GetEncoding(string Path)
        {
            return GetEncoding(Path, Firefly.TextEncoding.TextEncoding.Default);
        }

        public static StreamReader CreateTextReader(NewReadingStreamPasser sp, Encoding Encoding, bool DetectEncodingFromByteOrderMarks = true)
        {
            var s = sp.GetStream();
            if (DetectEncodingFromByteOrderMarks)
            {
                if (s.Length >= 4)
                {
                    s.Position = 0;
                    int BOM = s.ReadInt32B();
                    if (BOM == unchecked((int)0xFFFE0000)) return new StreamReader((s.Partialize(4, s.Length - 4, true)).ToUnsafeStream(), Firefly.TextEncoding.TextEncoding.UTF32, false);
                    if (BOM == 0xFEFF) return new StreamReader((s.Partialize(4, s.Length - 4, true)).ToUnsafeStream(), Firefly.TextEncoding.TextEncoding.UTF32B, false);
                    if (BOM == unchecked((int)0x84319533)) return new StreamReader((s.Partialize(4, s.Length - 4, true)).ToUnsafeStream(), Firefly.TextEncoding.TextEncoding.GB18030, false);
                }
                if (s.Length >= 3)
                {
                    s.Position = 0;
                    int BOM = s.ReadUInt16B();
                    BOM = (BOM << 8) | s.ReadByte();
                    if (BOM == 0xEFBBBF) return new StreamReader((s.Partialize(3, s.Length - 3, true)).ToUnsafeStream(), Firefly.TextEncoding.TextEncoding.UTF8, false);
                }
                if (s.Length >= 2)
                {
                    s.Position = 0;
                    ushort BOM = s.ReadUInt16B();
                    if (BOM == 0xFFFE) return new StreamReader((s.Partialize(2, s.Length - 2, true)).ToUnsafeStream(), Firefly.TextEncoding.TextEncoding.UTF16, false);
                    if (BOM == 0xFEFF) return new StreamReader((s.Partialize(2, s.Length - 2, true)).ToUnsafeStream(), Firefly.TextEncoding.TextEncoding.UTF16B, false);
                }
                s.Position = 0;
                return new StreamReader(s.ToUnsafeStream(), Encoding, true);
            }
            else
            {
                return new StreamReader(s.ToUnsafeStream(), Encoding, false);
            }
        }
        public static StreamReader CreateTextReader(string Path, Encoding Encoding, bool DetectEncodingFromByteOrderMarks = true)
        {
            return CreateTextReader(Streams.OpenReadable(Path).AsNewReading(), Encoding, DetectEncodingFromByteOrderMarks);
        }
        public static StreamReader CreateTextReader(string Path)
        {
            return CreateTextReader(Path, Firefly.TextEncoding.TextEncoding.Default, true);
        }

        public static string ReadFile(StreamReader Reader)
        {
            var s = Reader;
            if (!s.EndOfStream) return s.ReadToEnd();
            return "";
        }
        public static string ReadFile(string Path, Encoding Encoding, bool DetectEncodingFromByteOrderMarks = true)
        {
            using (var s = CreateTextReader(Path, Encoding, DetectEncodingFromByteOrderMarks))
            {
                if (!s.EndOfStream) return s.ReadToEnd();
            }
            return "";
        }
        public static string ReadFile(string Path)
        {
            return ReadFile(Path, Firefly.TextEncoding.TextEncoding.Default);
        }

        public static StreamWriter CreateTextWriter(NewWritingStreamPasser sp, Encoding Encoding, bool WithByteOrderMarks = true)
        {
            var s = sp.GetStream();
            if (WithByteOrderMarks)
            {
                if (Firefly.TextEncoding.TextEncoding.IsSameIntrinsic(Encoding, Firefly.TextEncoding.TextEncoding.UTF16))
                {
                    s.WriteByte(0xFF);
                    s.WriteByte(0xFE);
                }
                else if (Firefly.TextEncoding.TextEncoding.GB18030Available && Firefly.TextEncoding.TextEncoding.IsSameIntrinsic(Encoding, Firefly.TextEncoding.TextEncoding.GB18030))
                {
                    s.WriteInt32B(unchecked((int)0x84319533));
                }
                else if (Firefly.TextEncoding.TextEncoding.IsSameIntrinsic(Encoding, Firefly.TextEncoding.TextEncoding.UTF8))
                {
                    s.WriteByte(0xEF);
                    s.WriteByte(0xBB);
                    s.WriteByte(0xBF);
                }
                else if (Firefly.TextEncoding.TextEncoding.IsSameIntrinsic(Encoding, Firefly.TextEncoding.TextEncoding.UTF32))
                {
                    s.WriteByte(0xFF);
                    s.WriteByte(0xFE);
                    s.WriteByte(0);
                    s.WriteByte(0);
                }
                else if (Firefly.TextEncoding.TextEncoding.IsSameIntrinsic(Encoding, Firefly.TextEncoding.TextEncoding.UTF16B))
                {
                    s.WriteByte(0xFE);
                    s.WriteByte(0xFF);
                }
                else if (Firefly.TextEncoding.TextEncoding.IsSameIntrinsic(Encoding, Firefly.TextEncoding.TextEncoding.UTF32B))
                {
                    s.WriteByte(0);
                    s.WriteByte(0);
                    s.WriteByte(0xFE);
                    s.WriteByte(0xFF);
                }
            }
            return new StreamWriter(s.ToStream(), new EncodingNoPreambleWrapper(Encoding));
        }
        public static StreamWriter CreateTextWriter(string Path, Encoding Encoding, bool WithByteOrderMarks = true)
        {
            return CreateTextWriter(Streams.CreateResizable(Path).AsNewWriting(), Encoding, WithByteOrderMarks);
        }
        public static StreamWriter CreateTextWriter(string Path)
        {
            return CreateTextWriter(Path, Firefly.TextEncoding.TextEncoding.WritingDefault, true);
        }

        public static void WriteFile(StreamWriter Writer, string Value)
        {
            var s = Writer;
            s.Write(Value);
        }
        public static void WriteFile(string Path, Encoding Encoding, string Value, bool WithByteOrderMarks = true)
        {
            using (var s = CreateTextWriter(Path, Encoding, WithByteOrderMarks))
            {
                s.Write(Value);
            }
        }
        public static void WriteFile(string Path, string Value)
        {
            WriteFile(Path, Firefly.TextEncoding.TextEncoding.WritingDefault, Value);
        }
    }
}
