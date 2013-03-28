using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yuki
{
    [Record]
    public struct Unit
    {
    }

    public class RecordAttribute : Firefly.Mapping.MetaSchema.RecordAttribute
    {
    }

    public class AliasAttribute : Firefly.Mapping.MetaSchema.AliasAttribute
    {
    }

    public class TaggedUnionAttribute : Firefly.Mapping.MetaSchema.TaggedUnionAttribute
    {
    }

    public class TagAttribute : Firefly.Mapping.MetaSchema.TagAttribute
    {
    }

    public class TupleAttribute : Firefly.Mapping.MetaSchema.TupleAttribute
    {
    }

    public enum OptionalTag
    {
        NotHasValue = 0,
        HasValue = 1
    }
    [TaggedUnion]
    public struct Optional<T>
    {
        [Tag]
        public OptionalTag _Tag { get; set; }

        public Unit NotHasValue { get; set; }
        public T HasValue { get; set; }

        public static Optional<T> CreateNotHasValue() { return new Optional<T> { _Tag = OptionalTag.NotHasValue, NotHasValue = new Unit() }; }
        public static Optional<T> CreateHasValue(T Value) { return new Optional<T> { _Tag = OptionalTag.HasValue, HasValue = Value }; }

        public Boolean OnNotHasValue { get { return _Tag == OptionalTag.NotHasValue; } }
        public Boolean OnHasValue { get { return _Tag == OptionalTag.HasValue; } }

        public static Optional<T> Empty { get { return CreateNotHasValue(); } }
        public static implicit operator Optional<T>(T v)
        {
            if (v == null)
            {
                return CreateNotHasValue();
            }
            else
            {
                return CreateHasValue(v);
            }
        }
        public static explicit operator T(Optional<T> v)
        {
            if (v.OnNotHasValue)
            {
                throw new InvalidOperationException();
            }
            return v.HasValue;
        }
        public static Boolean operator ==(Optional<T> Left, Optional<T> Right)
        {
            return Equals(Left, Right);
        }
        public static Boolean operator !=(Optional<T> Left, Optional<T> Right)
        {
            return !Equals(Left, Right);
        }
        public static Boolean operator ==(Optional<T>? Left, Optional<T>? Right)
        {
            return Equals(Left, Right);
        }
        public static Boolean operator !=(Optional<T>? Left, Optional<T>? Right)
        {
            return !Equals(Left, Right);
        }
        public override Boolean Equals(Object obj)
        {
            return Equals(this, obj);
        }
        public override Int32 GetHashCode()
        {
            if (OnNotHasValue) { return 0; }
            return HasValue.GetHashCode();
        }

        private static Boolean Equals(Optional<T> Left, Optional<T> Right)
        {
            if (Left.OnNotHasValue && Right.OnNotHasValue)
            {
                return true;
            }
            if (Left.OnNotHasValue || Right.OnNotHasValue)
            {
                return false;
            }
            return Left.HasValue.Equals(Right.HasValue);
        }
        private static Boolean Equals(Optional<T>? Left, Optional<T>? Right)
        {
            if ((!Left.HasValue || Left.Value.OnNotHasValue) && (!Right.HasValue || Right.Value.OnNotHasValue))
            {
                return true;
            }
            if (!Left.HasValue || Left.Value.OnNotHasValue || !Right.HasValue || Right.Value.OnNotHasValue)
            {
                return false;
            }
            return Equals(Left.Value, Right.Value);
        }

        public T ValueOrDefault(T Default)
        {
            if (OnHasValue)
            {
                return HasValue;
            }
            else
            {
                return Default;
            }
        }
    }
}
