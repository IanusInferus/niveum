using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Firefly
{
    public sealed class Indexer : IEnumerator<int>, IEnumerable<int>
    {
        private SortedList<int, Range> Descriptor = new SortedList<int, Range>();
        private int Value;
        private int Position;

        public Indexer(ICollection<Range> Descriptors)
        {
            foreach (Range d in Descriptors)
            {
                if (d.Lower == int.MinValue) throw new InvalidDataException();
                Descriptor.Add(d.Lower, d);
            }
            Value = int.MinValue;
            Position = 0;
        }
        public void AddDescriptor(Range d)
        {
            if (d.Lower == int.MinValue) throw new InvalidDataException();
            Descriptor.Add(d.Lower, d);
            Position = 0;
        }
        public void RemoveDescriptor(Range d)
        {
            Descriptor.Remove(d.Lower);
            Position = 0;
        }

        public bool Contain(int i)
        {
            if (Descriptor.Count == 0) return false;
            int U = Descriptor.Count - 1;
            int M = U / 2;
            while (U > 0)
            {
                if (Descriptor.Keys[M] > i)
                {
                    U = M;
                    M = U / 2;
                }
                else
                {
                    break;
                }
            }
            U = M;
            for (int n = U; n >= 0; n--)
            {
                if (Descriptor[Descriptor.Keys[n]].Contain(i)) return true;
            }
            return false;
        }

        public int Current
        {
            get { return Value; }
        }

        object IEnumerator.Current
        {
            get { return Value; }
        }

        public bool MoveNext()
        {
            if (Descriptor.Count == 0) return false;
            var v = Value + 1;
            while (v >= Descriptor.Values[Position].Lower + Descriptor.Values[Position].Count)
            {
                Position += 1;
                if (Position >= Descriptor.Count) return false;
            }
            if (v < Descriptor.Values[Position].Lower) v = Descriptor.Values[Position].Lower;
            Value = v;
            return true;
        }

        public void SetBefore(int Index)
        {
            Value = Index - 1;
        }

        public void Reset()
        {
            if (Descriptor.Count < 0) throw new InvalidOperationException();
            Value = int.MinValue;
            Position = 0;
        }

        public void Dispose()
        {
            Descriptor = null;
        }

        public IEnumerator<int> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }
    }

    public class Range
    {
        public int Lower;
        public int Upper;
        public int Count
        {
            get { return Upper - Lower + 1; }
            set { Upper = Lower + value - 1; }
        }
        public Range(int Lower, int Upper)
        {
            if (Upper < Lower) throw new ArgumentOutOfRangeException();
            this.Lower = Lower;
            this.Upper = Upper;
        }
        public bool Contain(int i)
        {
            return (i >= Lower) && (i <= Upper);
        }
    }

    public class RangeInt64
    {
        public long Lower;
        public long Upper;
        public long Count
        {
            get { return Upper - Lower + 1; }
            set { Upper = Lower + value - 1; }
        }
        public RangeInt64(long Lower, long Upper)
        {
            if (Upper < Lower) throw new ArgumentOutOfRangeException();
            this.Lower = Lower;
            this.Upper = Upper;
        }
        public bool Contain(long i)
        {
            return (i >= Lower) && (i <= Upper);
        }
    }

    public class RangeUInt64
    {
        public ulong Lower;
        public ulong Upper;
        public ulong Count
        {
            get { return Upper - Lower + 1UL; }
            set { Upper = Lower + value - 1UL; }
        }
        public RangeUInt64(ulong Lower, ulong Upper)
        {
            if (Upper < Lower) throw new ArgumentOutOfRangeException();
            this.Lower = Lower;
            this.Upper = Upper;
        }
        public bool Contain(ulong i)
        {
            return (i >= Lower) && (i <= Upper);
        }
    }
}
