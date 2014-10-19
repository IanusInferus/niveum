using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Krustallos
{
    public class ImmutableSortedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private Func<TKey, TKey, int> Compare;
        private Node Root;

        private class DefaultComparer
        {
            public int Compare(TKey x, TKey y)
            {
                return ((IComparable<TKey>)(x)).CompareTo(y);
            }
        }
        private class ReversedDefaultComparer
        {
            public int Compare(TKey x, TKey y)
            {
                return -((IComparable<TKey>)(x)).CompareTo(y);
            }
        }

        public ImmutableSortedDictionary()
        {
            this.Compare = (new DefaultComparer()).Compare;
            this.Root = null;
        }
        public ImmutableSortedDictionary(bool IsReversed)
        {
            if (IsReversed)
            {
                this.Compare = (new ReversedDefaultComparer()).Compare;
            }
            else
            {
                this.Compare = (new DefaultComparer()).Compare;
            }
            this.Root = null;
        }
        public ImmutableSortedDictionary(Func<TKey, TKey, int> ConcurrentCompare)
        {
            this.Compare = ConcurrentCompare;
            this.Root = null;
        }
        public ImmutableSortedDictionary(IComparer<TKey> ConcurrentComparer)
        {
            this.Compare = ConcurrentComparer.Compare;
            this.Root = null;
        }

        private class Node
        {
            public readonly TKey Key;
            public readonly TValue Value;
            public readonly Node Left;
            public readonly Node Right;
            public int Count;
            public int Height;
            public Node(TKey Key, TValue Value, Node Left, Node Right)
            {
                this.Key = Key;
                this.Value = Value;
                this.Left = Left;
                this.Right = Right;
                this.Count = GetCount(Left) + GetCount(Right) + 1;
                this.Height = Math.Max(GetHeight(Left), GetHeight(Right)) + 1;
            }
        }

        public int Count
        {
            get
            {
                return GetCount(Root);
            }
        }
        public bool ContainsKey(TKey Key)
        {
            foreach (var v in Range(Key, Key))
            {
                return true;
            }
            return false;
        }
        public Optional<TValue> TryGetValue(TKey Key)
        {
            foreach (var v in Range(Key, Key))
            {
                return v.Value;
            }
            return Optional<TValue>.Empty;
        }
        public Optional<TValue> TryGetValueByIndex(int Index)
        {
            foreach (var v in RangeByIndex(Index, Index))
            {
                return v.Value;
            }
            return Optional<TValue>.Empty;
        }
        public Optional<KeyValuePair<TKey, TValue>> TryGetPairByIndex(int Index)
        {
            foreach (var v in RangeByIndex(Index, Index))
            {
                return v;
            }
            return Optional<KeyValuePair<TKey, TValue>>.Empty;
        }
        public Optional<KeyValuePair<TKey, TValue>> TryGetMinPair()
        {
            foreach (var v in RangeByIndex(0, 0))
            {
                return v;
            }
            return Optional<KeyValuePair<TKey, TValue>>.Empty;
        }
        public Optional<KeyValuePair<TKey, TValue>> TryGetMaxPair()
        {
            var UpperIndex = GetCount(Root) - 1;
            foreach (var v in RangeByIndex(UpperIndex, UpperIndex))
            {
                return v;
            }
            return Optional<KeyValuePair<TKey, TValue>>.Empty;
        }
        public Optional<TValue> TryGetMin()
        {
            foreach (var v in RangeByIndex(0, 0))
            {
                return v.Value;
            }
            return Optional<TValue>.Empty;
        }
        public Optional<TValue> TryGetMax()
        {
            var UpperIndex = GetCount(Root) - 1;
            foreach (var v in RangeByIndex(UpperIndex, UpperIndex))
            {
                return v.Value;
            }
            return Optional<TValue>.Empty;
        }
        public IEnumerable<KeyValuePair<TKey, TValue>> Range(Optional<TKey> Lower, Optional<TKey> Upper)
        {
            if (Lower.OnHasValue && Upper.OnHasValue)
            {
                if (Compare(Lower.Value, Upper.Value) > 0)
                {
                    return Enumerable.Empty<KeyValuePair<TKey, TValue>>();
                }
            }
            return Range(Root, Lower, Upper);
        }
        public IEnumerable<KeyValuePair<TKey, TValue>> RangeByIndex(Optional<int> LowerIndex, Optional<int> UpperIndex)
        {
            if (LowerIndex.OnHasValue && UpperIndex.OnHasValue)
            {
                if (LowerIndex.Value > UpperIndex.Value)
                {
                    return Enumerable.Empty<KeyValuePair<TKey, TValue>>();
                }
            }
            return RangeByIndex(Root, LowerIndex, UpperIndex);
        }
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Range(Optional<TKey>.Empty, Optional<TKey>.Empty).GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)(Range(Optional<TKey>.Empty, Optional<TKey>.Empty))).GetEnumerator();
        }

        private static int GetCount(Node n)
        {
            if (n == null) { return 0; }
            return n.Count;
        }
        private static int GetHeight(Node n)
        {
            if (n == null) { return 0; }
            return n.Height;
        }
        private IEnumerable<KeyValuePair<TKey, TValue>> Range(Node n, Optional<TKey> Lower, Optional<TKey> Upper)
        {
            if (n == null) { yield break; }

            var nAgainstLower = Lower.OnNotHasValue ? 1 : Compare(n.Key, Lower.Value);
            var nAgainstUpper = Upper.OnNotHasValue ? -1 : Compare(n.Key, Upper.Value);

            if (nAgainstLower > 0)
            {
                foreach (var v in Range(n.Left, Lower, Upper))
                {
                    yield return v;
                }
            }

            if ((nAgainstLower >= 0) && (nAgainstUpper <= 0))
            {
                yield return new KeyValuePair<TKey, TValue>(n.Key, n.Value);
            }

            if (nAgainstUpper < 0)
            {
                foreach (var v in Range(n.Right, Lower, Upper))
                {
                    yield return v;
                }
            }
        }
        private IEnumerable<KeyValuePair<TKey, TValue>> RangeByIndex(Node n, Optional<int> LowerIndex, Optional<int> UpperIndex)
        {
            if (n == null) { yield break; }

            var Index = GetCount(n.Left);
            var nAgainstLower = LowerIndex.OnNotHasValue ? 1 : Index.CompareTo(LowerIndex.Value);
            var nAgainstUpper = UpperIndex.OnNotHasValue ? -1 : Index.CompareTo(UpperIndex.Value);

            if (nAgainstLower > 0)
            {
                foreach (var v in RangeByIndex(n.Left, LowerIndex, UpperIndex))
                {
                    yield return v;
                }
            }

            if ((nAgainstLower >= 0) && (nAgainstUpper <= 0))
            {
                yield return new KeyValuePair<TKey, TValue>(n.Key, n.Value);
            }

            if (nAgainstUpper < 0)
            {
                foreach (var v in RangeByIndex(n.Right, LowerIndex.OnNotHasValue ? LowerIndex : (LowerIndex.Value - Index - 1), UpperIndex.OnNotHasValue ? UpperIndex : (UpperIndex.Value - Index - 1)))
                {
                    yield return v;
                }
            }
        }

        public ImmutableSortedDictionary<TKey, TValue> Add(TKey Key, TValue Value)
        {
            return new ImmutableSortedDictionary<TKey, TValue>(this.Compare) { Root = Add(Root, Key, Value) };
        }
        public ImmutableSortedDictionary<TKey, TValue> Remove(TKey Key)
        {
            return new ImmutableSortedDictionary<TKey, TValue>(this.Compare) { Root = Remove(Root, Key) };
        }
        public ImmutableSortedDictionary<TKey, TValue> SetItem(TKey Key, TValue Value)
        {
            return new ImmutableSortedDictionary<TKey, TValue>(this.Compare) { Root = SetItem(Root, Key, Value) };
        }
        public ImmutableSortedDictionary<TKey, TValue> AddOrSetItem(TKey Key, TValue Value)
        {
            if (ContainsKey(Key))
            {
                return SetItem(Key, Value);
            }
            else
            {
                return Add(Key, Value);
            }
        }
        public ImmutableSortedDictionary<TKey, TValue> RemoveIfExist(TKey Key)
        {
            if (ContainsKey(Key))
            {
                return Remove(Key);
            }
            else
            {
                return this;
            }
        }
        public TValue GetOrCreate(TKey Key, Func<TValue> ValueFactory)
        {
            if (ContainsKey(Key))
            {
                return TryGetValue(Key).Value;
            }
            else
            {
                return ValueFactory();
            }
        }

        private Node Add(Node n, TKey Key, TValue Value)
        {
            if (n == null)
            {
                return new Node(Key, Value, null, null);
            }
            var Result = Compare(n.Key, Key);
            if (Result == 0) { throw new InvalidOperationException("KeyExist"); }
            if (Result > 0)
            {
                return Rebalance(new Node(n.Key, n.Value, Add(n.Left, Key, Value), n.Right));
            }
            else
            {
                return Rebalance(new Node(n.Key, n.Value, n.Left, Add(n.Right, Key, Value)));
            }
        }
        private Node Remove(Node n, TKey Key)
        {
            if (n == null) { throw new InvalidOperationException("KeyNotExist"); }
            var Result = Compare(n.Key, Key);
            if (Result == 0)
            {
                if (n.Left == null)
                {
                    return n.Right;
                }
                else if (n.Right == null)
                {
                    return n.Left;
                }
                TKey SuccessorKey;
                TValue SuccessorValue;
                var r = RemoveMin(n.Right, out SuccessorKey, out SuccessorValue);
                return Rebalance(new Node(SuccessorKey, SuccessorValue, n.Left, r));
            }
            else if (Result > 0)
            {
                return Rebalance(new Node(n.Key, n.Value, Remove(n.Left, Key), n.Right));
            }
            else
            {
                return Rebalance(new Node(n.Key, n.Value, n.Left, Remove(n.Right, Key)));
            }
        }
        private Node RemoveMin(Node n, out TKey Key, out TValue Value)
        {
            if (n.Left == null)
            {
                Key = n.Key;
                Value = n.Value;
                return n.Right;
            }
            return Rebalance(new Node(n.Key, n.Value, RemoveMin(n.Left, out Key, out Value), n.Right));
        }
        private Node SetItem(Node n, TKey Key, TValue Value)
        {
            if (n == null) { throw new InvalidOperationException("KeyNotExist"); }
            var Result = Compare(n.Key, Key);
            if (Result == 0)
            {
                return new Node(Key, Value, n.Left, n.Right);
            }
            if (Result > 0)
            {
                return new Node(n.Key, n.Value, SetItem(n.Left, Key, Value), n.Right);
            }
            else
            {
                return new Node(n.Key, n.Value, n.Left, SetItem(n.Right, Key, Value));
            }
        }
        private Node Rebalance(Node n)
        {
            var BalanceFactor = GetBalanceFactor(n);
            if (BalanceFactor == 2)
            {
                var l = n.Left;
                if (GetBalanceFactor(n.Left) == 1)
                {
                    // LL Rotate
                    return new Node(l.Key, l.Value, l.Left, new Node(n.Key, n.Value, l.Right, n.Right));
                }
                else
                {
                    // LR Rotate
                    var lr = l.Right;
                    return new Node(lr.Key, lr.Value, new Node(l.Key, l.Value, l.Left, lr.Left), new Node(n.Key, n.Value, lr.Right, n.Right));
                }
            }
            else if (BalanceFactor == -2)
            {
                var r = n.Right;
                if (GetBalanceFactor(n.Right) == 1)
                {
                    // RL Rotate
                    var rl = r.Left;
                    return new Node(rl.Key, rl.Value, new Node(n.Key, n.Value, n.Left, rl.Left), new Node(r.Key, r.Value, rl.Right, r.Right));
                }
                else
                {
                    // RR Rotate
                    return new Node(r.Key, r.Value, new Node(n.Key, n.Value, n.Left, r.Left), r.Right);
                }
            }
            else
            {
                return n;
            }
        }
        private int GetBalanceFactor(Node n)
        {
            if (n == null) { return 0; }
            return GetHeight(n.Left) - GetHeight(n.Right);
        }
    }
}
