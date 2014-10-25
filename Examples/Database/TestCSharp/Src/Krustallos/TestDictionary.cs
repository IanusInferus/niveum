using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace Krustallos
{
    public class TestDictionary
    {
        public static void TestSequentialAdd()
        {
            var d = new ImmutableSortedDictionary<int, int>();
            var n = 1000;
            Debug.Assert(!d.ContainsKey(0));
            for (int k = 0; k < n; k += 1)
            {
                d = d.Add(k, k * 2);
            }
            for (int k = 0; k < n; k += 1)
            {
                Debug.Assert(d.ContainsKey(k));
                Debug.Assert(d.TryGetValue(k) == k * 2);
            }
            for (int k = 0; k < n; k += 1)
            {
                var p = d.TryGetPairByIndex(k);
                Debug.Assert(p.Value.Key == k);
                Debug.Assert(p.Value.Value == k * 2);
            }
            Debug.Assert(d.TryGetMin().Value == 0);
            Debug.Assert(d.TryGetMax().Value == (n - 1) * 2);
            Debug.Assert(!d.ContainsKey(-1));
            Debug.Assert(d.Count == n);
            {
                var k = 0;
                foreach (var p in d.Range(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = n - 1;
                foreach (var p in d.RangeReversed(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            {
                var k = 0;
                foreach (var p in d.RangeByIndex(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = n - 1;
                foreach (var p in d.RangeByIndexReversed(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            {
                var k = 100;
                foreach (var p in d.Range(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = 200;
                foreach (var p in d.RangeReversed(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            {
                var k = 100;
                foreach (var p in d.RangeByIndex(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = 200;
                foreach (var p in d.RangeByIndexReversed(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            Debug.Assert(d.RangeCount(100, 200) == 101);
            Debug.Assert(d.RangeCount(0, n - 1) == n);
            Debug.Assert(d.RangeCount(0, Optional<int>.Empty) == n);
            Debug.Assert(d.RangeCount(Optional<int>.Empty, n - 1) == n);
            Debug.Assert(d.RangeCount(Optional<int>.Empty, Optional<int>.Empty) == n);
            var l = d.ToList();
            Debug.Assert(l.Count == n);
            for (int k = 0; k < n; k += 1)
            {
                var p = l[k];
                Debug.Assert(p.Key == k);
                Debug.Assert(p.Value == k * 2);
            }
            for (int k = 0; k < n; k += 1)
            {
                d = d.SetItem(k, k * 3);
            }
            for (int k = 0; k < n; k += 1)
            {
                Debug.Assert(d.ContainsKey(k));
                Debug.Assert(d.TryGetValue(k) == k * 3);
            }
            for (int k = 0; k < n; k += 1)
            {
                d = d.Remove(k);
            }
            Debug.Assert(d.Count == 0);
            var l2 = d.ToList();
            Debug.Assert(l2.Count == 0);
        }

        public static void TestRandomAdd()
        {
            var d = new ImmutableSortedDictionary<int, int>();
            var n = 1009; //Prime
            Debug.Assert(!d.ContainsKey(0));
            for (int k = 0; k < n; k += 1)
            {
                var v = (k * 7 + 11) % n;
                d = d.Add(v, v * 2);
            }
            for (int k = 0; k < n; k += 1)
            {
                Debug.Assert(d.ContainsKey(k));
                Debug.Assert(d.TryGetValue(k) == k * 2);
            }
            for (int k = 0; k < n; k += 1)
            {
                var p = d.TryGetPairByIndex(k);
                Debug.Assert(p.Value.Key == k);
                Debug.Assert(p.Value.Value == k * 2);
            }
            Debug.Assert(d.TryGetMin().Value == 0);
            Debug.Assert(d.TryGetMax().Value == (n - 1) * 2);
            Debug.Assert(!d.ContainsKey(-1));
            Debug.Assert(d.Count == n);
            {
                var k = 0;
                foreach (var p in d.Range(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = n - 1;
                foreach (var p in d.RangeReversed(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            {
                var k = 0;
                foreach (var p in d.RangeByIndex(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = n - 1;
                foreach (var p in d.RangeByIndexReversed(Optional<int>.Empty, Optional<int>.Empty))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            {
                var k = 100;
                foreach (var p in d.Range(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = 200;
                foreach (var p in d.RangeReversed(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            {
                var k = 100;
                foreach (var p in d.RangeByIndex(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k += 1;
                }
            }
            {
                var k = 200;
                foreach (var p in d.RangeByIndexReversed(100, 200))
                {
                    Debug.Assert(p.Key == k);
                    Debug.Assert(p.Value == k * 2);
                    k -= 1;
                }
            }
            Debug.Assert(d.RangeCount(100, 200) == 101);
            Debug.Assert(d.RangeCount(0, n - 1) == n);
            Debug.Assert(d.RangeCount(0, Optional<int>.Empty) == n);
            Debug.Assert(d.RangeCount(Optional<int>.Empty, n - 1) == n);
            Debug.Assert(d.RangeCount(Optional<int>.Empty, Optional<int>.Empty) == n);
            var l = d.ToList();
            Debug.Assert(l.Count == n);
            for (int k = 0; k < n; k += 1)
            {
                var p = l[k];
                Debug.Assert(p.Key == k);
                Debug.Assert(p.Value == k * 2);
            }
            for (int k = 0; k < n; k += 1)
            {
                d = d.SetItem(k, k * 3);
            }
            for (int k = 0; k < n; k += 1)
            {
                Debug.Assert(d.ContainsKey(k));
                Debug.Assert(d.TryGetValue(k) == k * 3);
            }
            for (int k = 0; k < n; k += 1)
            {
                var v = (k * 11 + 7) % n;
                d = d.Remove(v);
            }
            Debug.Assert(d.Count == 0);
            var l2 = d.ToList();
            Debug.Assert(l2.Count == 0);
        }

        public static void TestHeight()
        {
            Func<int, double> HeightLimit = ((Func<Func<int, double>>)(() =>
            {
                // ln(sqrt(5) * (n + 2)) / ln((1 + sqrt(5)) / 2) - 2
                var a = Math.Sqrt(5);
                var b = 1.0 / Math.Log((1 + Math.Sqrt(5)) * 0.5);
                return c =>
                {
                    return Math.Log(a * (c + 2)) * b - 2;
                };
            }))();

            var d = new ImmutableSortedDictionary<int, Unit>();
            var n = 10000;
            var r = new Random();
            for (int k = 0; k < n; k += 1)
            {
                d = d.AddOrSetItem(r.Next(0, n), default(Unit));
                Debug.Assert(d.Height < HeightLimit(d.Count));
            }
            var Count = d.Count;
            for (int k = 0; k < Count; k += 1)
            {
                d = d.Remove(d.TryGetPairByIndex(r.Next(0, d.Count)).Value.Key);
                Debug.Assert(d.Height < HeightLimit(d.Count));
            }
        }

        public static void TestIndex()
        {
            var d = new ImmutableSortedDictionary<int, int>();
            var n = 1000;
            Debug.Assert(!d.ContainsKey(0));
            for (int k = 0; k < n; k += 1)
            {
                d = d.Add(k, k * 2);
            }
            Debug.Assert(d.GetIndexStartWithKey(-1) == 0);
            Debug.Assert(d.GetIndexStartWithKey(0) == 0);
            Debug.Assert(d.GetIndexStartWithKey(100) == 100);
            Debug.Assert(d.GetIndexStartWithKey(1000) == 1000);
            Debug.Assert(d.GetIndexStartWithKey(1001) == 1000);
            Debug.Assert(d.GetIndexEndWithKey(-1) == -1);
            Debug.Assert(d.GetIndexEndWithKey(0) == 0);
            Debug.Assert(d.GetIndexEndWithKey(100) == 100);
            Debug.Assert(d.GetIndexEndWithKey(1000) == 999);
            Debug.Assert(d.GetIndexEndWithKey(1001) == 999);
            d = d.Remove(500);
            Debug.Assert(d.GetIndexStartWithKey(500) == 500);
            Debug.Assert(d.GetIndexEndWithKey(500) == 499);
            {
                int k = 100;
                foreach (var p in d.Range(50, 250, 50, 50))
                {
                    Debug.Assert(p.Key == k);
                    k += 1;
                }
            }
        }

        public static void TestParallelRandomAdd1()
        {
            var swSerial = new Stopwatch();
            long Ticks = 0;
            var Lockee = new Object();
            var d = new ImmutableSortedDictionary<int, int>();
            var n = 104729; //Prime
            Debug.Assert(!d.ContainsKey(0));
            swSerial.Start();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var v = (k * 7 + 11) % n;
                    var sw = new Stopwatch();
                    sw.Start();
                    lock (Lockee)
                    {
                        d = d.Add(v, v * 2);
                    }
                    sw.Stop();
                    Interlocked.Add(ref Ticks, sw.ElapsedTicks);
                }
            );
            swSerial.Stop();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    Debug.Assert(d.ContainsKey(k));
                    Debug.Assert(d.TryGetValue(k) == k * 2);
                }
            );
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var p = d.TryGetPairByIndex(k);
                    Debug.Assert(p.Value.Key == k);
                    Debug.Assert(p.Value.Value == k * 2);
                }
            );
            Debug.Assert(d.TryGetMin().Value == 0);
            Debug.Assert(d.TryGetMax().Value == (n - 1) * 2);
            Debug.Assert(!d.ContainsKey(-1));
            Debug.Assert(d.Count == n);
            var l = d.ToList();
            Debug.Assert(l.Count == n);
            for (int k = 0; k < n; k += 1)
            {
                var p = l[k];
                Debug.Assert(p.Key == k);
                Debug.Assert(p.Value == k * 2);
            }
            swSerial.Start();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var v = (k * 7 + 11) % n;
                    var sw = new Stopwatch();
                    sw.Start();
                    lock (Lockee)
                    {
                        d = d.SetItem(k, k * 3);
                    }
                    sw.Stop();
                    Interlocked.Add(ref Ticks, sw.ElapsedTicks);
                }
            );
            swSerial.Stop();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    Debug.Assert(d.ContainsKey(k));
                    Debug.Assert(d.TryGetValue(k) == k * 3);
                }
            );
            swSerial.Start();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    lock (Lockee)
                    {
                        d = d.Remove(k);
                    }
                    sw.Stop();
                    Interlocked.Add(ref Ticks, sw.ElapsedTicks);
                }
            );
            swSerial.Stop();
            Debug.Assert(d.Count == 0);
            var l2 = d.ToList();
            Debug.Assert(l2.Count == 0);
            var ParallelMilliseconds = (double)(Ticks) / (double)(Stopwatch.Frequency) * 1000;
            var SerialMilliseconds = (double)(swSerial.ElapsedTicks) / (double)(Stopwatch.Frequency) * 1000;
            Console.WriteLine("TestParallelRandomAdd1");
            Console.WriteLine("并行耗时: " + ParallelMilliseconds.ToString() + "ms");
            Console.WriteLine("线性耗时: " + SerialMilliseconds.ToString() + "ms");
            Console.WriteLine("加速比: " + (ParallelMilliseconds / SerialMilliseconds).ToString());
        }

        public static void TestParallelRandomAdd2()
        {
            var swSerial = new Stopwatch();
            long Ticks = 0;
            var d = new ImmutableSortedDictionary<int, int>();
            var n = 104729; //Prime
            Debug.Assert(!d.ContainsKey(0));
            swSerial.Start();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var v = (k * 7 + 11) % n;
                    var sw = new Stopwatch();
                    sw.Start();
                    var od = d;
                    while (true)
                    {
                        var nd = Interlocked.CompareExchange(ref d, od.Add(v, v * 2), od);
                        if (nd == od)
                        {
                            break;
                        }
                        od = nd;
                    }
                    sw.Stop();
                    Interlocked.Add(ref Ticks, sw.ElapsedTicks);
                }
            );
            swSerial.Stop();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    Debug.Assert(d.ContainsKey(k));
                    Debug.Assert(d.TryGetValue(k) == k * 2);
                }
            );
            Debug.Assert(d.TryGetMin().Value == 0);
            Debug.Assert(d.TryGetMax().Value == (n - 1) * 2);
            Debug.Assert(!d.ContainsKey(-1));
            Debug.Assert(d.Count == n);
            var l = d.ToList();
            Debug.Assert(l.Count == n);
            for (int k = 0; k < n; k += 1)
            {
                var p = l[k];
                Debug.Assert(p.Key == k);
                Debug.Assert(p.Value == k * 2);
            }
            swSerial.Start();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    var od = d;
                    while (true)
                    {
                        var nd = Interlocked.CompareExchange(ref d, od.SetItem(k, k * 3), od);
                        if (nd == od)
                        {
                            break;
                        }
                        od = nd;
                    }
                    sw.Stop();
                    Interlocked.Add(ref Ticks, sw.ElapsedTicks);
                }
            );
            swSerial.Stop();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    Debug.Assert(d.ContainsKey(k));
                    Debug.Assert(d.TryGetValue(k) == k * 3);
                }
            );
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var p = d.TryGetPairByIndex(k);
                    Debug.Assert(p.Value.Key == k);
                    Debug.Assert(p.Value.Value == k * 3);
                }
            );
            swSerial.Start();
            ParallelEnumerable.Range(0, n).ForAll
            (
                k =>
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    var od = d;
                    while (true)
                    {
                        var nd = Interlocked.CompareExchange(ref d, od.Remove(k), od);
                        if (nd == od)
                        {
                            break;
                        }
                        od = nd;
                    }
                    sw.Stop();
                    Interlocked.Add(ref Ticks, sw.ElapsedTicks);
                }
            );
            swSerial.Stop();
            Debug.Assert(d.Count == 0);
            var l2 = d.ToList();
            Debug.Assert(l2.Count == 0);
            var ParallelMilliseconds = (double)(Ticks) / (double)(Stopwatch.Frequency) * 1000;
            var SerialMilliseconds = (double)(swSerial.ElapsedTicks) / (double)(Stopwatch.Frequency) * 1000;
            Console.WriteLine("TestParallelRandomAdd2");
            Console.WriteLine("并行耗时: " + ParallelMilliseconds.ToString() + "ms");
            Console.WriteLine("线性耗时: " + SerialMilliseconds.ToString() + "ms");
            Console.WriteLine("加速比: " + (ParallelMilliseconds / SerialMilliseconds).ToString());
        }

        public static void Run()
        {
            TestSequentialAdd();
            TestRandomAdd();
            TestHeight();
            TestIndex();
            TestParallelRandomAdd1();
            TestParallelRandomAdd2();
        }
    }
}
