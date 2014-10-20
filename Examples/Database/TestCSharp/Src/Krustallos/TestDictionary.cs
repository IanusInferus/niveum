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
            TestParallelRandomAdd1();
            TestParallelRandomAdd2();
        }
    }
}
