using System;
using System.Collections.Generic;
using System.Linq;

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

[Record]
public struct Unit { }
public enum OptionalTag
{
    None = 0,
    Some = 1
}
public struct Optional<T>
{
    [Tag] public OptionalTag _Tag;

    public Unit None;
    public T Some;

    public static Optional<T> CreateNone() { return new Optional<T> { _Tag = OptionalTag.None, None = new Unit() }; }
    public static Optional<T> CreateSome(T Value) { return new Optional<T> { _Tag = OptionalTag.Some, Some = Value }; }

    public Boolean OnNone { get { return _Tag == OptionalTag.None; } }
    public Boolean OnSome { get { return _Tag == OptionalTag.Some; } }

    public static Optional<T> Empty { get { return CreateNone(); } }
    public static implicit operator Optional<T>(T v)
    {
        if (v == null)
        {
            return CreateNone();
        }
        else
        {
            return CreateSome(v);
        }
    }
    public static explicit operator T(Optional<T> v)
    {
        if (v.OnNone)
        {
            throw new InvalidOperationException();
        }
        return v.Some;
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
        if (obj == null) { return Equals(this, null); }
        if (obj.GetType() != typeof(Optional<T>)) { return false; }
        var o = (Optional<T>)(obj);
        return Equals(this, o);
    }
    public override Int32 GetHashCode()
    {
        if (OnNone) { return 0; }
        return Some.GetHashCode();
    }

    private static Boolean Equals(Optional<T> Left, Optional<T> Right)
    {
        if (Left.OnNone && Right.OnNone)
        {
            return true;
        }
        if (Left.OnNone || Right.OnNone)
        {
            return false;
        }
        return Left.Some.Equals(Right.Some);
    }
    private static Boolean Equals(Optional<T>? Left, Optional<T>? Right)
    {
        if ((!Left.HasValue || Left.Value.OnNone) && (!Right.HasValue || Right.Value.OnNone))
        {
            return true;
        }
        if (!Left.HasValue || Left.Value.OnNone || !Right.HasValue || Right.Value.OnNone)
        {
            return false;
        }
        return Equals(Left.Value, Right.Value);
    }

    public T Value
    {
        get
        {
            if (OnSome)
            {
                return Some;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
    public T ValueOrDefault(T Default)
    {
        if (OnSome)
        {
            return Some;
        }
        else
        {
            return Default;
        }
    }

    public override String ToString()
    {
        if (OnSome)
        {
            return Some.ToString();
        }
        else
        {
            return "-";
        }
    }
}
