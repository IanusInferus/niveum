using System;
using System.Runtime.CompilerServices;

namespace Firefly
{
    public static class NumericOperations
    {
        public static T Max<T>(T a, T b) where T : IComparable
        {
            if (a != null)
            {
                if (a.CompareTo(b) >= 0) return a;
            }
            return b;
        }
        public static T Min<T>(T a, T b) where T : IComparable
        {
            if (a != null)
            {
                if (a.CompareTo(b) >= 0) return b;
            }
            return a;
        }
        public static T Max<T>(T a, params T[] b) where T : IComparable
        {
            T ret = a;
            foreach (var x in b)
            {
                if (x != null)
                {
                    if (x.CompareTo(ret) >= 0) ret = x;
                }
            }
            return ret;
        }
        public static T Min<T>(T a, params T[] b) where T : IComparable
        {
            T ret = a;
            foreach (var x in b)
            {
                if (ret != null)
                {
                    if (ret.CompareTo(x) >= 0) ret = x;
                }
            }
            return ret;
        }
        public static void Exchange<T>(ref T a, ref T b)
        {
            T Temp = a;
            a = b;
            b = Temp;
        }
        public static int Mod(this int This, int m)
        {
            int r = This % m;
            if ((r < 0 && m > 0) || (r > 0 && m < 0)) r += m;
            return r;
        }
        public static long Mod(this long This, long m)
        {
            long r = This % m;
            if ((r < 0 && m > 0) || (r > 0 && m < 0)) r += m;
            return r;
        }
        public static int Div(this int This, int b)
        {
            if (b == 0) throw new DivideByZeroException();
            int r = This.Mod(b);
            if (This > 0 && r < 0)
            {
                if ((This - int.MaxValue > r)) return (This - Math.Abs(b) - r) / b + Math.Sign(b);
            }
            else if (This < 0 && r > 0)
            {
                if ((This - int.MinValue < r)) return (This + Math.Abs(b) - r) / b - Math.Sign(b);
            }
            return (This - r) / b;
        }
        public static long Div(this long This, long b)
        {
            if (b == 0) throw new DivideByZeroException();
            long r = This.Mod(b);
            if (This > 0 && r < 0)
            {
                if ((This - long.MaxValue > r)) return (This - Math.Abs(b) - r) / b + Math.Sign(b);
            }
            else if (This < 0 && r > 0)
            {
                if ((This - long.MinValue < r)) return (This + Math.Abs(b) - r) / b - Math.Sign(b);
            }
            return (This - r) / b;
        }
        public static int CeilToMultipleOf(this int This, int n)
        {
            return (This + n - 1).Div(n) * n;
        }
        public static long CeilToMultipleOf(this long This, long n)
        {
            return (This + n - 1).Div(n) * n;
        }
        public static uint CeilToMultipleOf(this uint This, uint n)
        {
            return ((This + n - 1U) / n) * n;
        }
        public static ulong CeilToMultipleOf(this ulong This, ulong n)
        {
            return ((This + n - 1UL) / n) * n;
        }
    }
}
