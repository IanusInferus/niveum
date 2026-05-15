using System;

namespace Firefly.Streaming
{
    public sealed class ArrayStream<T> : IDisposable
    {
        private T[] BaseArray;
        private int BasePositionValue;
        private int PositionValue;
        private int LengthValue;

        public ArrayStream(int Length)
        {
            if (Length < 0) throw new ArgumentOutOfRangeException();
            BaseArray = new T[Length];
            BasePositionValue = 0;
            PositionValue = 0;
            LengthValue = Length;
        }
        public ArrayStream(T[] BaseArray, int BasePosition = 0)
        {
            if (BaseArray == null) throw new ArgumentNullException();
            if (BasePosition < 0 || BasePosition > BaseArray.Length) throw new ArgumentOutOfRangeException();
            this.BaseArray = BaseArray;
            this.BasePositionValue = BasePosition;
            this.PositionValue = BasePosition;
            this.LengthValue = BaseArray.Length - BasePosition;
        }

        public ArrayStream(T[] BaseArray, int BasePosition, int Length)
        {
            if (BaseArray == null) throw new ArgumentNullException();
            if (Length < 0) throw new ArgumentOutOfRangeException();
            if (BasePosition < 0 || BasePosition + Length > BaseArray.Length) throw new ArgumentOutOfRangeException();
            this.BaseArray = BaseArray;
            this.BasePositionValue = BasePosition;
            this.PositionValue = BasePosition;
            this.LengthValue = Length;
        }

        public T ReadElement()
        {
            T t = BaseArray[PositionValue];
            PositionValue += 1;
            return t;
        }
        public void WriteElement(T b)
        {
            BaseArray[PositionValue] = b;
            PositionValue += 1;
        }
        public T PeekElement()
        {
            var HoldPosition = PositionValue;
            try { return ReadElement(); }
            finally { PositionValue = HoldPosition; }
        }

        public void Flush() { }
        public void Close()
        {
            Dispose();
        }
        public long Length
        {
            get { return LengthValue; }
        }
        public int Position
        {
            get { return PositionValue - BasePositionValue; }
            set { PositionValue = BasePositionValue + value; }
        }
        public void Read(T[] Buffer, int Offset, int Count)
        {
            if (Count < 0 || PositionValue + Count > BasePositionValue + LengthValue) throw new ArgumentOutOfRangeException();
            Array.Copy(BaseArray, PositionValue, Buffer, Offset, Count);
            PositionValue += Count;
        }
        public void Read(T[] Buffer)
        {
            Read(Buffer, 0, Buffer.Length);
        }
        public T[] Read(int Count)
        {
            T[] d = new T[Count];
            Read(d, 0, Count);
            return d;
        }
        public void Write(T[] Buffer, int Offset, int Count)
        {
            if (Count < 0 || PositionValue + Count > BasePositionValue + LengthValue) throw new ArgumentOutOfRangeException();
            Array.Copy(Buffer, Offset, BaseArray, PositionValue, Count);
            PositionValue += Count;
        }
        public void Write(T[] Buffer)
        {
            Write(Buffer, 0, Buffer.Length);
        }

        public void ReadToStream(ArrayStream<T> s, int Count)
        {
            if (Count <= 0) return;
            T[] Buffer = new T[(int)NumericOperations.Min(Count, 4 * (1 << 10))];
            for (int n = 0; n <= Count - Buffer.Length; n += Buffer.Length)
            {
                Read(Buffer);
                s.Write(Buffer);
            }
            int LeftLength = (int)(Count % Buffer.Length);
            Read(Buffer, 0, LeftLength);
            s.Write(Buffer, 0, LeftLength);
        }
        public void WriteFromStream(ArrayStream<T> s, int Count)
        {
            if (Count <= 0) return;
            T[] Buffer = new T[(int)NumericOperations.Min(Count, 4 * (1 << 10))];
            for (int n = 0; n <= Count - Buffer.Length; n += Buffer.Length)
            {
                s.Read(Buffer);
                Write(Buffer);
            }
            int LeftLength = (int)(Count % Buffer.Length);
            s.Read(Buffer, 0, LeftLength);
            Write(Buffer, 0, LeftLength);
        }

        public void Dispose()
        {
            BaseArray = null;
        }
    }
}
