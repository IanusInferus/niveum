using System;
using System.Collections;
using System.Collections.Generic;

namespace Firefly
{
    public sealed class MappedEnumerator<TKey, TValue> : IEnumerator<TValue>
    {
        private IEnumerator<TKey> BaseEnumerator;
        private Func<TKey, TValue> Mapping;

        public MappedEnumerator(IEnumerator<TKey> BaseEnumerator, Func<TKey, TValue> Mapping)
        {
            this.BaseEnumerator = BaseEnumerator;
            this.Mapping = Mapping;
        }

        public TValue Current
        {
            get { return Mapping(BaseEnumerator.Current); }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public bool MoveNext()
        {
            return BaseEnumerator.MoveNext();
        }

        public void Reset()
        {
            BaseEnumerator.Reset();
        }

        public void Dispose()
        {
            if (BaseEnumerator != null)
            {
                BaseEnumerator.Dispose();
                BaseEnumerator = null;
            }
        }
    }

    public sealed class ZippedEnumerator<TKeyA, TKeyB, TValue> : IEnumerator<TValue>
    {
        private IEnumerator<TKeyA> BaseEnumeratorA;
        private IEnumerator<TKeyB> BaseEnumeratorB;
        private Func<TKeyA, TKeyB, TValue> Zipping;

        public ZippedEnumerator(IEnumerator<TKeyA> BaseEnumeratorA, IEnumerator<TKeyB> BaseEnumeratorB, Func<TKeyA, TKeyB, TValue> Zipping)
        {
            this.BaseEnumeratorA = BaseEnumeratorA;
            this.BaseEnumeratorB = BaseEnumeratorB;
            this.Zipping = Zipping;
        }

        public TValue Current
        {
            get { return Zipping(BaseEnumeratorA.Current, BaseEnumeratorB.Current); }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public bool MoveNext()
        {
            var ResultA = BaseEnumeratorA.MoveNext();
            var ResultB = BaseEnumeratorB.MoveNext();
            if (ResultA != ResultB) throw new InvalidOperationException();
            return ResultA;
        }

        public void Reset()
        {
            BaseEnumeratorA.Reset();
            BaseEnumeratorB.Reset();
        }

        public void Dispose()
        {
            if (BaseEnumeratorA != null)
            {
                BaseEnumeratorA.Dispose();
                BaseEnumeratorA = null;
            }
            if (BaseEnumeratorB != null)
            {
                BaseEnumeratorB.Dispose();
                BaseEnumeratorB = null;
            }
        }
    }

    public class EnumeratorEnumerable<T> : IEnumerable<T>
    {
        private IEnumerator<T> BaseEnumerator;

        public EnumeratorEnumerable(IEnumerator<T> BaseEnumerator)
        {
            this.BaseEnumerator = BaseEnumerator;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return BaseEnumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
