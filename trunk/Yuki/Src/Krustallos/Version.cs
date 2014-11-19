using System;
using System.Diagnostics;

namespace Krustallos
{
    [DebuggerDisplay("{ToString(),nq}")]
    public struct Version : IEquatable<Version>, IComparable<Version>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public const int IndexSpace = 1073741824;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public const int WindowSize = 268435456;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int v;

        public Version(int v)
        {
            this.v = mod(v, IndexSpace);
        }

        public static explicit operator Version(int v)
        {
            return new Version(v);
        }
        public static explicit operator int(Version v)
        {
            return v.v;
        }

        private static int mod(int v, int m)
        {
            var r = v % m;
            if ((r < 0 && m > 0) || (r > 0 && m < 0)) { r += m; }
            return r;
        }

        public static Version operator +(Version Left, int Right)
        {
            return new Version(Left.v + Right);
        }
        public static Version operator -(Version Left, int Right)
        {
            return new Version(Left.v - Right);
        }
        public static int operator -(Version Left, Version Right)
        {
            var d = Left.v - Right.v;
            if ((d > -WindowSize) && (d < WindowSize)) { return d; }
            if (d < 0)
            {
                d += IndexSpace;
            }
            else
            {
                d -= IndexSpace;
            }
            if ((d > -WindowSize) && (d < WindowSize)) { return d; }
            throw new InvalidOperationException();
        }

        public static bool operator ==(Version Left, Version Right)
        {
            return Left.v == Right.v;
        }
        public static bool operator !=(Version Left, Version Right)
        {
            return Left.v != Right.v;
        }

        public bool Equals(Version Other)
        {
            return this.v == Other.v;
        }
        public override bool Equals(Object Obj)
        {
            if (Obj == null) { return false; }
            if (Obj.GetType() != typeof(Version)) { return false; }
            var Other = (Version)(Obj);
            return this.Equals(Other);
        }
        public override int GetHashCode()
        {
            return v;
        }

        public static bool operator <(Version Left, Version Right)
        {
            return Left.CompareTo(Right) < 0;
        }
        public static bool operator >(Version Left, Version Right)
        {
            return Left.CompareTo(Right) > 0;
        }
        public static bool operator <=(Version Left, Version Right)
        {
            return Left.CompareTo(Right) <= 0;
        }
        public static bool operator >=(Version Left, Version Right)
        {
            return Left.CompareTo(Right) >= 0;
        }
        public int CompareTo(Version Other)
        {
            return this - Other;
        }

        public override string ToString()
        {
            return v.ToString();
        }
    }
}
