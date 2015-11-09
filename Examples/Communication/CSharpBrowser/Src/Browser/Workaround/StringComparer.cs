using System.Collections;
using System.Collections.Generic;

namespace System
{
    public abstract class StringComparer : IComparer<String>, IEqualityComparer<String>
    {
        protected StringComparer() { }

        private class ComparerOrdinal : StringComparer
        {
            public override int Compare(String x, String y)
            {
                return String.Compare(x, y, StringComparison.Ordinal);
            }

            public override bool Equals(String x, String y)
            {
                return String.Equals(x, y, StringComparison.Ordinal);
            }

            public override int GetHashCode(String obj)
            {
                return obj.GetHashCode();
            }
        }

        private class ComparerOrdinalIgnoreCase : StringComparer
        {
            public override int Compare(String x, String y)
            {
                return String.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(String x, String y)
            {
                return String.Equals(x, y, StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode(String obj)
            {
                return obj.ToLower().GetHashCode();
            }
        }

        public static StringComparer Ordinal
        {
            get
            {
                return new ComparerOrdinal();
            }
        }
        public static StringComparer OrdinalIgnoreCase
        {
            get
            {
                return new ComparerOrdinalIgnoreCase();
            }
        }

        public abstract int Compare(String x, String y);
        public abstract bool Equals(String x, String y);
        public abstract int GetHashCode(String obj);
    }
}
