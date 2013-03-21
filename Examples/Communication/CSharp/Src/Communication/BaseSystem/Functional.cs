using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseSystem
{
    public static class Functional
    {
        public static TResult Branch<T, TResult>(this T Obj, Func<T, Boolean> Predicate, Func<T, TResult> TrueSelector, Func<T, TResult> FalseSelector)
        {
            return Predicate(Obj) ? TrueSelector(Obj) : FalseSelector(Obj);
        }
    }
}
