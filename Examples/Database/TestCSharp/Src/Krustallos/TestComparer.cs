using System;
using System.Linq;
using System.Diagnostics;

namespace Krustallos
{
    public static class TestComparer
    {
        public static void TestComparerString()
        {
            var d = new ImmutableSortedDictionary<String, int>();
            d = d.Add("AC", 3);
            d = d.Add("A", 1);
            d = d.Add("", 0);
            d = d.Add("AB", 2);
            d = d.Add("BA", 4);
            d = d.Add("a", 5);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
        }
        public static void TestComparerOptionalString()
        {
            var d = new ImmutableSortedDictionary<Optional<String>, int>();
            d = d.Add("AC", 3);
            d = d.Add("A", 1);
            d = d.Add(null, 0);
            d = d.Add("AB", 2);
            d = d.Add("BA", 4);
            d = d.Add("a", 5);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
        }
        public enum TestEnum
        {
            A,
            B,
            C
        }
        public static void TestComparerEnum()
        {
            var d = new ImmutableSortedDictionary<TestEnum, int>();
            d = d.Add(TestEnum.B, 1);
            d = d.Add(TestEnum.A, 0);
            d = d.Add(TestEnum.C, 2);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
        }
        public static void TestComparerByteArray()
        {
            var d = new ImmutableSortedDictionary<Byte[], int>();
            d = d.Add(new Byte[] { 1, 3 }, 3);
            d = d.Add(new Byte[] { 1 }, 1);
            d = d.Add(new Byte[] { }, 0);
            d = d.Add(new Byte[] { 1, 2 }, 2);
            d = d.Add(new Byte[] { 2, 1 }, 4);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
        }
        public static void TestComparerStringArray()
        {
            var d = new ImmutableSortedDictionary<String[], int>();
            d = d.Add(new String[] { "1", "3" }, 3);
            d = d.Add(new String[] { "1" }, 1);
            d = d.Add(new String[] { }, 0);
            d = d.Add(new String[] { "1", "2" }, 2);
            d = d.Add(new String[] { "2", "1" }, 4);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
        }
        public static void TestComparerOptionalByteArray()
        {
            var d = new ImmutableSortedDictionary<Optional<Byte[]>, int>();
            d = d.Add(new Byte[] { 1, 3 }, 4);
            d = d.Add(new Byte[] { 1 }, 2);
            d = d.Add(null, 0);
            d = d.Add(new Byte[] { }, 1);
            d = d.Add(new Byte[] { 1, 2 }, 3);
            d = d.Add(new Byte[] { 2, 1 }, 5);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
        }
        public static void TestKey()
        {
            var d = new ImmutableSortedDictionary<Key, int>(new KeyComparer(ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<Byte[]>()), ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<Optional<int>>()), ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<String>())));
            d = d.Add(new Key(null, Optional<int>.Empty, "A"), 0);
            d = d.Add(new Key(null, Optional<int>.Empty, "B"), 1);
            d = d.Add(new Key(null, Optional<int>.Empty, "aA"), 2);
            d = d.Add(new Key(null, Optional<int>.Empty, "aB"), 3);
            d = d.Add(new Key(null, (Optional<int>)(1), "aA"), 4);
            d = d.Add(new Key(null, (Optional<int>)(1), "aB"), 5);
            d = d.Add(new Key(null, (Optional<int>)(2), "A"), 6);
            d = d.Add(new Key(null, (Optional<int>)(2), "B"), 7);
            d = d.Add(new Key(new Byte[] { }, (Optional<int>)(1), "A"), 8);
            d = d.Add(new Key(new Byte[] { 1 }, (Optional<int>)(1), "A"), 9);
            d = d.Add(new Key(new Byte[] { 1, 2 }, (Optional<int>)(1), "A"), 10);
            d = d.Add(new Key(new Byte[] { 1, 3 }, (Optional<int>)(1), "A"), 11);
            d = d.Add(new Key(new Byte[] { 2, 1 }, (Optional<int>)(1), "A"), 12);
            foreach (var v in d.Zip(Enumerable.Range(0, d.Count), (a, b) => new { Key = a.Key, Left = a.Value, Right = b }))
            {
                Debug.Assert(v.Left == v.Right);
            }
            Debug.Assert(d.RangeCount(new Key(null, KeyCondition.Min, KeyCondition.Min), new Key(null, KeyCondition.Max, KeyCondition.Max)) == 8);
            Debug.Assert(d.RangeCount(new Key(new Byte[] { }, KeyCondition.Min, KeyCondition.Min), new Key(new Byte[] { }, KeyCondition.Max, KeyCondition.Max)) == 1);
            Debug.Assert(d.RangeCount(new Key(new Byte[] { }, KeyCondition.Min, KeyCondition.Min), new Key(new Byte[] { 2, 1 }, KeyCondition.Max, KeyCondition.Max)) == 5);
        }

        public static void Run()
        {
            TestComparerString();
            TestComparerOptionalString();
            TestComparerEnum();
            TestComparerByteArray();
            TestComparerStringArray();
            TestComparerOptionalByteArray();
            TestKey();
        }
    }
}
