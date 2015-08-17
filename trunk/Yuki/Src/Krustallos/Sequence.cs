using System;
using System.Threading;

namespace Krustallos
{
    public class SequenceInt32
    {
        private Int32 Value;

        public SequenceInt32() : this(1)
        {
        }
        public SequenceInt32(Int32 FirstValue)
        {
            if (FirstValue == Int32.MinValue) { throw new OverflowException(); }
            Value = FirstValue - 1;
        }

        public Int32 NextValue()
        {
            var v = Interlocked.Increment(ref Value);
            if (v == Int32.MinValue) { throw new OverflowException(); }
            return v;
        }

        public void SkipPass(Int32 KnownValue)
        {
            while (true)
            {
                var CurrentValue = this.Value;
                if (CurrentValue >= KnownValue) { break; }
                if (Interlocked.CompareExchange(ref Value, KnownValue, CurrentValue) == CurrentValue) { break; }
            }
        }
    }

    public class SequenceInt64
    {
        private Int64 Value;

        public SequenceInt64() : this(1)
        {
        }
        public SequenceInt64(Int64 FirstValue)
        {
            if (FirstValue == Int64.MinValue) { throw new OverflowException(); }
            Value = FirstValue - 1;
        }

        public Int64 NextValue()
        {
            var v = Interlocked.Increment(ref Value);
            if (v == Int64.MinValue) { throw new OverflowException(); }
            return v;
        }

        public void SkipPass(Int64 KnownValue)
        {
            while (true)
            {
                var CurrentValue = Interlocked.Read(ref Value);
                if (CurrentValue >= KnownValue) { break; }
                if (Interlocked.CompareExchange(ref Value, KnownValue, CurrentValue) == CurrentValue) { break; }
            }
        }
    }

    public class SequenceInt : SequenceInt32
    {
    }
}
