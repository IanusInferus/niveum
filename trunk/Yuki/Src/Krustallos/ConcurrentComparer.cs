using System;
using System.Collections.Generic;
using System.Linq;

namespace Krustallos
{
    public static class ConcurrentComparer
    {
        public static Func<T, T, int> CreateDefault<T>(bool IsReversed = false)
        {
            if (typeof(T).IsGenericType && (typeof(T).GetGenericTypeDefinition() == typeof(Optional<>)))
            {
                var Type = typeof(T).GetGenericArguments().Single();
                var Inner = Activator.CreateInstance((IsReversed ? typeof(ReversedDefaultComparer<>) : typeof(DefaultComparer<>)).MakeGenericType(Type));
                return ((IComparer<T>)(Activator.CreateInstance(typeof(OptionalComparer<>).MakeGenericType(Type), Inner))).Compare;
            }
            else if (typeof(T).GetInterfaces().Where(it => it.IsGenericType && (it.GetGenericTypeDefinition() == typeof(IEnumerable<>))).Count() == 1)
            {
                var Type = typeof(T).GetInterfaces().Where(it => it.IsGenericType && (it.GetGenericTypeDefinition() == typeof(IEnumerable<>))).Single().GetGenericArguments().Single();
                var Inner = Activator.CreateInstance((IsReversed ? typeof(ReversedDefaultComparer<>) : typeof(DefaultComparer<>)).MakeGenericType(Type));
                return ((IComparer<T>)(Activator.CreateInstance(typeof(EnumerableComparer<>).MakeGenericType(Type), Inner))).Compare;
            }
            else
            {
                return (IsReversed ? (IComparer<T>)(new ReversedDefaultComparer<T>()) : (IComparer<T>)(new DefaultComparer<T>())).Compare;
            }
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
        private class ReversedDefaultComparer<T> : IComparer<T>
        {
            private Func<T, T, int> Inner;
            public ReversedDefaultComparer()
            {
                if (typeof(T) == typeof(String))
                {
                    Inner = (Func<T, T, int>)(Object)(Func<String, String, int>)(String.CompareOrdinal);
                }
                else
                {
                    Inner = (x, y) => ((IComparable<T>)(x)).CompareTo(y);
                }
            }
            public int Compare(T x, T y)
            {
                return -Inner(x, y);
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
