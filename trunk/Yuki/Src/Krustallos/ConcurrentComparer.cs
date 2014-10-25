﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Krustallos
{
    public static class ConcurrentComparer
    {
        public static Func<T, T, int> CreateDefault<T>(bool IsReversed = false)
        {
            if (IsReversed)
            {
                return (new ReversedComparer<T>(new DefaultComparer<T>())).Compare;
            }
            else
            {
                return (new DefaultComparer<T>()).Compare;
            }
        }
        public static Func<Object, Object, int> AsObjectComparer<T>(Func<T, T, int> Compare)
        {
            return (x, y) => Compare((T)(x), (T)(y));
        }

        private class DefaultComparer<T> : IComparer<T>
        {
            private Func<T, T, int> Inner;
            public DefaultComparer()
            {
                if (typeof(T) == typeof(String))
                {
                    Inner = (Func<T, T, int>)(Object)(Func<String, String, int>)(String.CompareOrdinal);
                }
                else if (typeof(T).IsGenericType && (typeof(T).GetGenericTypeDefinition() == typeof(Optional<>)))
                {
                    var Type = typeof(T).GetGenericArguments().Single();
                    var ElementComparer = Activator.CreateInstance(typeof(DefaultComparer<>).MakeGenericType(Type));
                    Inner = ((IComparer<T>)(Activator.CreateInstance(typeof(OptionalComparer<>).MakeGenericType(Type), ElementComparer))).Compare;
                }
                else if (typeof(T).GetInterfaces().Where(it => it.IsGenericType && (it.GetGenericTypeDefinition() == typeof(IEnumerable<>))).Count() == 1)
                {
                    var Type = typeof(T).GetInterfaces().Where(it => it.IsGenericType && (it.GetGenericTypeDefinition() == typeof(IEnumerable<>))).Single().GetGenericArguments().Single();
                    var ElementComparer = Activator.CreateInstance(typeof(DefaultComparer<>).MakeGenericType(Type));
                    Inner = ((IComparer<T>)(Activator.CreateInstance(typeof(EnumerableComparer<>).MakeGenericType(Type), ElementComparer))).Compare;
                }
                else
                {
                    Inner = (x, y) => ((IComparable<T>)(x)).CompareTo(y);
                }
            }
            public int Compare(T x, T y)
            {
                return Inner(x, y);
            }
        }
        private class ReversedComparer<T> : IComparer<T>
        {
            private IComparer<T> Inner;
            public ReversedComparer(IComparer<T> Inner)
            {
                this.Inner = Inner;
            }
            public int Compare(T x, T y)
            {
                return -Inner.Compare(x, y);
            }
        }
        private class OptionalComparer<T> : IComparer<Optional<T>>
        {
            private IComparer<T> Inner;
            public OptionalComparer(IComparer<T> Inner)
            {
                this.Inner = Inner;
            }
            public int Compare(Optional<T> x, Optional<T> y)
            {
                if ((x == null) && (y == null)) { return 0; }
                if (x == null) { return -1; }
                if (y == null) { return 1; }
                return Inner.Compare(x.HasValue, y.HasValue);
            }
        }
        private class EnumerableComparer<T> : IComparer<IEnumerable<T>>
        {
            private IComparer<T> Inner;
            public EnumerableComparer(IComparer<T> Inner)
            {
                this.Inner = Inner;
            }
            public int Compare(IEnumerable<T> x, IEnumerable<T> y)
            {
                if ((x == null) && (y == null)) { return 0; }
                if (x == null) { return -1; }
                if (y == null) { return 1; }
                var xi = x.GetEnumerator();
                var yi = y.GetEnumerator();
                while (true)
                {
                    var xr = xi.MoveNext();
                    var yr = yi.MoveNext();
                    if (!xr && !yr) { return 0; }
                    if (!xr) { return -1; }
                    if (!yr) { return 1; }
                    var r = Inner.Compare(xi.Current, yi.Current);
                    if (r != 0) { return r; }
                }
                throw new InvalidOperationException();
            }
        }
    }
}