#pragma once

#include <functional>

#ifndef _UNIT_TYPE_
typedef struct {} Unit;
#define _UNIT_TYPE_
#endif
typedef bool Boolean;

#ifndef _OPTIONAL_TYPE_
enum OptionalTag
{
    OptionalTag_NotHasValue = 0,
    OptionalTag_HasValue = 1
};
/* TaggedUnion */
template<typename T>
class Optional /* final */
{
public:
    /* Tag */ OptionalTag _Tag;

    Unit NotHasValue;
    T HasValue;

    static Optional<T> CreateNotHasValue()
    {
        auto r = Optional<T>();
        r._Tag = OptionalTag_NotHasValue;
        r.NotHasValue = Unit();
        return r;
    }
    static Optional<T> CreateHasValue(T Value)
    {
        auto r = Optional<T>();
        r._Tag = OptionalTag_HasValue;
        r.HasValue = Value;
        return r;
    }

    Boolean OnNotHasValue() const
    {
        return _Tag == OptionalTag_NotHasValue;
    }
    Boolean OnHasValue() const
    {
        return _Tag == OptionalTag_HasValue;
    }

    static Optional<T> Empty() { return CreateNotHasValue(); }
    Optional()
        : _Tag(OptionalTag_NotHasValue),
        NotHasValue(Unit()),
        HasValue(T())
    {
    }
    Optional(const T &v)
        : _Tag(OptionalTag_HasValue),
        NotHasValue(Unit()),
        HasValue(v)
    {
    }
    Optional(std::nullptr_t v)
        : _Tag(OptionalTag_NotHasValue),
        NotHasValue(Unit()),
        HasValue(T())
    {
    }
    /* explicit operator T() const
    {
    if (OnNotHasValue())
    {
    throw std::logic_error("InvalidOperationException");
    }
    return HasValue;
    } */
    Boolean operator ==(const Optional<T> &Right) const
    {
        return Equals(*this, Right);
    }
    Boolean operator !=(const Optional<T> &Right) const
    {
        return !Equals(*this, Right);
    }
    Boolean operator ==(const T &Right) const
    {
        return Equals(*this, static_cast<const Optional<T> &>(Right));
    }
    Boolean operator !=(const T &Right) const
    {
        return !Equals(*this, static_cast<const Optional<T> &>(Right));
    }
    Boolean operator ==(std::nullptr_t Right) const
    {
        return Equals(*this, Right);
    }
    Boolean operator !=(std::nullptr_t Right) const
    {
        return !Equals(*this, Right);
    }

private:
    static Boolean Equals(const Optional<T> &Left, const Optional<T> &Right)
    {
        if (Left.OnNotHasValue() && Right.OnNotHasValue())
        {
            return true;
        }
        if (Left.OnNotHasValue() || Right.OnNotHasValue())
        {
            return false;
        }
        return Left.HasValue == Right.HasValue;
    }
    static Boolean Equals(const Optional<T> &Left, std::nullptr_t Right)
    {
        return Left.OnNotHasValue();
    }

public:
    T Value() const
    {
        if (OnHasValue())
        {
            return HasValue;
        }
        else
        {
            throw std::logic_error("InvalidOperationException");
        }
    }
    T ValueOrDefault(T Default) const
    {
        if (OnHasValue())
        {
            return HasValue;
        }
        else
        {
            return Default;
        }
    }
};

namespace std
{
    template <typename T>
    struct hash<Optional<T>>
    {
        size_t operator()(const Optional<T> &x) const
        {
            if (x.OnNotHasValue()) { return 0; }
            return hash<T>()(x.HasValue);
        }
    };
    template <typename T>
    struct less<Optional<T>>
    {
        bool operator()(const Optional<T> &x, const Optional<T> &y) const
        {
            if ((x == nullptr) && (y == nullptr)) { return false; }
            if (x == nullptr) { return true; }
            if (y == nullptr) { return false; }
            return x.HasValue < y.HasValue;
        }
    };
}

#define _OPTIONAL_TYPE_
#endif
