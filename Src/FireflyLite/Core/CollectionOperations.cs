using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Firefly
{
    public static class CollectionOperations
    {
        public static KeyValuePair<TKey, TValue> CreatePair<TKey, TValue>(TKey Key, TValue Value)
        {
            return new KeyValuePair<TKey, TValue>(Key, Value);
        }

        public static T[] Extend<T>(this T[] This, int Length, T Value)
        {
            if (This.Length > Length) throw new ArgumentOutOfRangeException();
            T[] newBytes = new T[Length];
            Array.Copy(This, newBytes, NumericOperations.Min(This.Length, Length));
            for (int n = NumericOperations.Min(This.Length, Length); n <= Length - 1; n++)
            {
                newBytes[n] = Value;
            }
            return newBytes;
        }

        public static void ForEach<T>(this T[] This, Action<T> Action)
        {
            Array.ForEach(This, Action);
        }

        public static TValue ItemOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> This, TKey Key, TValue DefaultValue)
        {
            TValue ReturnValue;
            if (This.TryGetValue(Key, out ReturnValue))
            {
                return ReturnValue;
            }
            else
            {
                return DefaultValue;
            }
        }

        public static IEnumerable<TResult> ZipStrict<T1, T2, TResult>(this IEnumerable<T1> This, IEnumerable<T2> Right, Func<T1, T2, TResult> Selector)
        {
            return new EnumeratorEnumerable<TResult>(new ZippedEnumerator<T1, T2, TResult>(This.GetEnumerator(), Right.GetEnumerator(), Selector));
        }
    }
}
