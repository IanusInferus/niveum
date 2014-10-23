using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Krustallos
{
    public class Key
    {
        public Object[] Columns { get; private set; }

        public Key(params Object[] Columns)
        {
            this.Columns = Columns;
        }
    }

    public enum KeyCondition
    {
        Min,
        Max
    }

    public class KeyComparer : IComparer<Key>
    {
        private Func<Object, Object, int>[] InnerCompares;
        public KeyComparer(params Func<Object, Object, int>[] InnerCompares)
        {
            this.InnerCompares = InnerCompares;
        }

        public int Compare(Key x, Key y)
        {
            var xColumns = x.Columns;
            var yColumns = y.Columns;
            if (xColumns.Length != InnerCompares.Length) { throw new InvalidOperationException(); }
            if (yColumns.Length != InnerCompares.Length) { throw new InvalidOperationException(); }
            foreach (var p in InnerCompares.Select((c, i) => new { Left = xColumns[i], Right = yColumns[i], Compare = c }))
            {
                var LeftIsCondition = (p.Left != null) && (p.Left.GetType() == typeof(KeyCondition));
                var RightIsCondition = (p.Right != null) && (p.Right.GetType() == typeof(KeyCondition));
                if (LeftIsCondition && RightIsCondition)
                {
                    var l = (KeyCondition)(p.Left);
                    var r = (KeyCondition)(p.Right);
                    return (int)(l) - (int)(r);
                }
                else if (LeftIsCondition)
                {
                    var l = (KeyCondition)(p.Left);
                    if (l == KeyCondition.Min)
                    {
                        return -1;
                    }
                    else if (l == KeyCondition.Max)
                    {
                        return 1;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (RightIsCondition)
                {
                    var r = (KeyCondition)(p.Right);
                    if (r == KeyCondition.Min)
                    {
                        return 1;
                    }
                    else if (r == KeyCondition.Max)
                    {
                        return -1;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    var Result = p.Compare(p.Left, p.Right);
                    if (Result != 0) { return Result; }
                }
            }
            return 0;
        }
    }
}
