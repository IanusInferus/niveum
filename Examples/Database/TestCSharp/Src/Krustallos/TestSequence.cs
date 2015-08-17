using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Krustallos
{
    public static class TestSequence
    {
        public static void TestInt32()
        {
            var s = new SequenceInt32();
            Debug.Assert(s.NextValue() == 1);
            Debug.Assert(s.NextValue() == 2);
            s.SkipPass(2);
            Debug.Assert(s.NextValue() == 3);
            s.SkipPass(4);
            Debug.Assert(s.NextValue() == 5);
            s.SkipPass(1000);
            Debug.Assert(s.NextValue() == 1001);
            s.SkipPass(Int32.MaxValue);
            try
            {
                s.NextValue();
                Debug.Assert(false);
            }
            catch (OverflowException)
            {
            }
            try
            {
                new SequenceInt32(Int32.MinValue);
                Debug.Assert(false);
            }
            catch (OverflowException)
            {
            }
        }
        public static void TestInt64()
        {
            var s = new SequenceInt64();
            Debug.Assert(s.NextValue() == 1);
            Debug.Assert(s.NextValue() == 2);
            s.SkipPass(2);
            Debug.Assert(s.NextValue() == 3);
            s.SkipPass(4);
            Debug.Assert(s.NextValue() == 5);
            s.SkipPass(1000);
            Debug.Assert(s.NextValue() == 1001);
            s.SkipPass(Int64.MaxValue);
            try
            {
                s.NextValue();
                Debug.Assert(false);
            }
            catch (OverflowException)
            {
            }
            try
            {
                new SequenceInt64(Int64.MinValue);
                Debug.Assert(false);
            }
            catch (OverflowException)
            {
            }
        }
        public static void TestMultiThread()
        {
            var s32 = new SequenceInt32();
            var s64 = new SequenceInt64();
            Parallel.For(0, 128, Index =>
            {
                Thread.Sleep(10 * ((Index * 7 + 3) % 10));
                s32.SkipPass(Index);
                s64.SkipPass(Index);
            });
            Debug.Assert(s32.NextValue() == 128);
            Debug.Assert(s64.NextValue() == 128);
        }

        public static void Run()
        {
            TestInt32();
            TestInt64();
            TestMultiThread();
        }
    }
}
