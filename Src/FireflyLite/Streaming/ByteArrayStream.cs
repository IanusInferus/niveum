using System;

namespace Firefly.Streaming
{
    public sealed class ByteArrayStream : IReadableWritableSeekableStream
    {
        private byte[] BaseArray;
        private int BasePositionValue;
        private int PositionValue;
        private int LengthValue;

        public ByteArrayStream(int Length)
        {
            if (Length < 0) throw new ArgumentOutOfRangeException();
            BaseArray = new byte[Length];
            BasePositionValue = 0;
            PositionValue = 0;
            LengthValue = Length;
        }
        public ByteArrayStream(byte[] BaseArray, int BasePosition = 0)
        {
            if (BaseArray == null) throw new ArgumentNullException();
            if (BasePosition < 0 || BasePosition > BaseArray.Length) throw new ArgumentOutOfRangeException();
            this.BaseArray = BaseArray;
            this.BasePositionValue = BasePosition;
            this.PositionValue = BasePosition;
            this.LengthValue = BaseArray.Length - BasePosition;
        }

        public ByteArrayStream(byte[] BaseArray, int BasePosition, int Length)
        {
            if (BaseArray == null) throw new ArgumentNullException();
            if (Length < 0) throw new ArgumentOutOfRangeException();
            if (BasePosition < 0 || BasePosition + Length > BaseArray.LongLength) throw new ArgumentOutOfRangeException();
            this.BaseArray = BaseArray;
            this.BasePositionValue = BasePosition;
            this.PositionValue = BasePosition;
            this.LengthValue = Length;
        }

        public byte ReadByte()
        {
            byte t = BaseArray[PositionValue];
            PositionValue += 1;
            return t;
        }
        public void WriteByte(byte b)
        {
            BaseArray[PositionValue] = b;
            PositionValue += 1;
        }

        public void Flush() { }

        public long Length
        {
            get { return LengthValue; }
        }
        public long Position
        {
            get { return PositionValue - BasePositionValue; }
            set { PositionValue = (int)(BasePositionValue + value); }
        }
        public void Read(byte[] Buffer, int Offset, int Count)
        {
            if (Count < 0 || PositionValue + Count > BasePositionValue + LengthValue) throw new ArgumentOutOfRangeException();
            Array.Copy(BaseArray, PositionValue, Buffer, Offset, Count);
            PositionValue += Count;
        }
        public void Write(byte[] Buffer, int Offset, int Count)
        {
            if (Count < 0 || PositionValue + Count > BasePositionValue + LengthValue) throw new ArgumentOutOfRangeException();
            Array.Copy(Buffer, Offset, BaseArray, PositionValue, Count);
            PositionValue += Count;
        }

        public void Dispose()
        {
            BaseArray = null;
        }
    }
}
