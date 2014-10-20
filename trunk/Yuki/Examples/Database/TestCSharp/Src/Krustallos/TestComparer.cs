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

        public static void Run()
        {
            TestComparerString();
            TestComparerOptionalString();
            TestComparerByteArray();
            TestComparerStringArray();
            TestComparerOptionalByteArray();
        }
    }
}
