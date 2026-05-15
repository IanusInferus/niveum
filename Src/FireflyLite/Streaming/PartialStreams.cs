using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Firefly.Streaming
{
    public static class PartialStreams
    {
        public static IReadableSeekableStream Partialize(this IReadableSeekableStream This, long BasePosition, long BaseLength, bool BaseStreamClose = false)
        {
            return new PartialReadableSeekableStream(This, BasePosition, BaseLength, BaseStreamClose);
        }
        public static IWritableSeekableStream Partialize(this IWritableSeekableStream This, long BasePosition, long BaseLength, bool BaseStreamClose = false)
        {
            return new PartialWritableSeekableStream(This, BasePosition, BaseLength, BaseLength, BaseStreamClose);
        }
        public static IWritableSeekableStream Partialize(this IWritableSeekableStream This, long BasePosition, long BaseLength, long Length, bool BaseStreamClose = false)
        {
            return new PartialWritableSeekableStream(This, BasePosition, BaseLength, Length, BaseStreamClose);
        }
        public static IReadableWritableSeekableStream Partialize(this IReadableWritableSeekableStream This, long BasePosition, long BaseLength, bool BaseStreamClose = false)
        {
            return new PartialReadableWritableSeekableStream(This, BasePosition, BaseLength, BaseLength, BaseStreamClose);
        }
        public static IReadableWritableSeekableStream Partialize(this IReadableWritableSeekableStream This, long BasePosition, long BaseLength, long Length, bool BaseStreamClose = false)
        {
            return new PartialReadableWritableSeekableStream(This, BasePosition, BaseLength, Length, BaseStreamClose);
        }
        public static IStream Partialize(this IStream This, long BasePosition, long BaseLength, bool BaseStreamClose = false)
        {
            return new PartialStream(This, BasePosition, BaseLength, BaseLength, BaseStreamClose);
        }
        public static IStream Partialize(this IStream This, long BasePosition, long BaseLength, long Length, bool BaseStreamClose = false)
        {
            return new PartialStream(This, BasePosition, BaseLength, Length, BaseStreamClose);
        }
    }

    internal class PartialReadableSeekableStream : IReadableSeekableStream
    {
        private IReadableSeekableStream BaseStream;
        private long BasePosition;
        private long BaseLength;
        private bool BaseStreamClose;
        private long LengthValue;

        public PartialReadableSeekableStream(IReadableSeekableStream s, long BasePosition, long BaseLength, bool BaseStreamClose = false)
        {
            BaseStream = s;
            this.BasePosition = BasePosition;
            this.BaseLength = BaseLength;
            LengthValue = BaseLength;
            BaseStream.Position = BasePosition;
            this.BaseStreamClose = BaseStreamClose;
        }

        public byte ReadByte()
        {
            if (Position >= BaseLength) throw new EndOfStreamException();
            return BaseStream.ReadByte();
        }

        public void Flush() { BaseStream.Flush(); }
        public long Length { get { return LengthValue; } }
        public long Position
        {
            get { return BaseStream.Position - BasePosition; }
            set { BaseStream.Position = BasePosition + value; }
        }

        public void Read(byte[] Buffer, int Offset, int Count)
        {
            if (Position + Count > Length) throw new EndOfStreamException();
            BaseStream.Read(Buffer, Offset, Count);
        }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                if (BaseStreamClose) BaseStream.Dispose();
                else BaseStream.Flush();
                BaseStream = null;
            }
        }
    }

    internal class PartialWritableSeekableStream : IWritableSeekableStream
    {
        private IWritableSeekableStream BaseStream;
        private long BasePosition;
        private long BaseLength;
        private bool BaseStreamClose;
        private long LengthValue;

        public PartialWritableSeekableStream(IWritableSeekableStream s, long BasePosition, long BaseLength, long Length, bool BaseStreamClose = false)
        {
            BaseStream = s;
            this.BasePosition = BasePosition;
            if (BaseLength < Length) throw new ArgumentOutOfRangeException();
            this.BaseLength = BaseLength;
            LengthValue = Length;
            BaseStream.Position = BasePosition;
            this.BaseStreamClose = BaseStreamClose;
        }

        public void WriteByte(byte b)
        {
            if (Position >= BaseLength) throw new EndOfStreamException();
            BaseStream.WriteByte(b);
        }
        public void Flush() { BaseStream.Flush(); }
        public long Length { get { return LengthValue; } }
        public long Position
        {
            get { return BaseStream.Position - BasePosition; }
            set { BaseStream.Position = BasePosition + value; }
        }

        public void Write(byte[] Buffer, int Offset, int Count)
        {
            if (Position + Count > BaseLength) throw new EndOfStreamException();
            BaseStream.Write(Buffer, Offset, Count);
            if (Position > Length) LengthValue = Position;
        }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                if (BaseStreamClose) BaseStream.Dispose();
                else BaseStream.Flush();
                BaseStream = null;
            }
        }
    }

    internal class PartialReadableWritableSeekableStream : IReadableWritableSeekableStream
    {
        private IReadableWritableSeekableStream BaseStream;
        private long BasePosition;
        private long BaseLength;
        private bool BaseStreamClose;
        private long LengthValue;

        public PartialReadableWritableSeekableStream(IReadableWritableSeekableStream s, long BasePosition, long BaseLength, long Length, bool BaseStreamClose = false)
        {
            BaseStream = s;
            this.BasePosition = BasePosition;
            if (BaseLength < Length) throw new ArgumentOutOfRangeException();
            this.BaseLength = BaseLength;
            LengthValue = Length;
            BaseStream.Position = BasePosition;
            this.BaseStreamClose = BaseStreamClose;
        }

        public byte ReadByte()
        {
            if (Position >= BaseLength) throw new EndOfStreamException();
            return BaseStream.ReadByte();
        }
        public void WriteByte(byte b)
        {
            if (Position >= BaseLength) throw new EndOfStreamException();
            BaseStream.WriteByte(b);
        }
        public void Flush() { BaseStream.Flush(); }
        public long Length { get { return LengthValue; } }
        public long Position
        {
            get { return BaseStream.Position - BasePosition; }
            set { BaseStream.Position = BasePosition + value; }
        }

        public void Read(byte[] Buffer, int Offset, int Count)
        {
            if (Position + Count > Length) throw new EndOfStreamException();
            BaseStream.Read(Buffer, Offset, Count);
        }
        public void Write(byte[] Buffer, int Offset, int Count)
        {
            if (Position + Count > BaseLength) throw new EndOfStreamException();
            BaseStream.Write(Buffer, Offset, Count);
            if (Position > Length) LengthValue = Position;
        }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                if (BaseStreamClose) BaseStream.Dispose();
                else BaseStream.Flush();
                BaseStream = null;
            }
        }
    }

    internal class PartialStream : IStream
    {
        private IStream BaseStream;
        private long BasePosition;
        private long BaseLength;
        private bool BaseStreamClose;
        private long LengthValue;

        public PartialStream(IStream s, long BasePosition, long BaseLength, long Length, bool BaseStreamClose = false)
        {
            BaseStream = s;
            this.BasePosition = BasePosition;
            if (BaseLength < Length) throw new ArgumentOutOfRangeException();
            this.BaseLength = BaseLength;
            LengthValue = Length;
            BaseStream.Position = BasePosition;
            this.BaseStreamClose = BaseStreamClose;
        }

        public byte ReadByte()
        {
            if (Position >= BaseLength) throw new EndOfStreamException();
            return BaseStream.ReadByte();
        }
        public void WriteByte(byte b)
        {
            if (Position >= BaseLength) throw new EndOfStreamException();
            BaseStream.WriteByte(b);
        }
        public void Flush() { BaseStream.Flush(); }
        public long Length { get { return LengthValue; } }
        public long Position
        {
            get { return BaseStream.Position - BasePosition; }
            set { BaseStream.Position = BasePosition + value; }
        }
        public void SetLength(long Value)
        {
            if (Value < 0) throw new ArgumentOutOfRangeException();
            if (Value > BaseLength) throw new ArgumentOutOfRangeException();
            if (BasePosition + Value > BaseStream.Length) BaseStream.SetLength(BasePosition + Value);
            LengthValue = Value;
        }

        public void Read(byte[] Buffer, int Offset, int Count)
        {
            if (Position + Count > Length) throw new EndOfStreamException();
            BaseStream.Read(Buffer, Offset, Count);
        }
        public void Write(byte[] Buffer, int Offset, int Count)
        {
            if (Position + Count > BaseLength) throw new EndOfStreamException();
            BaseStream.Write(Buffer, Offset, Count);
            if (Position > Length) LengthValue = Position;
        }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                if (BaseStreamClose) BaseStream.Dispose();
                else BaseStream.Flush();
                BaseStream = null;
            }
        }
    }
}
