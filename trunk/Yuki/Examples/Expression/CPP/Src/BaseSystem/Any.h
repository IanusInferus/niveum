#pragma once

//http://www.open-std.org/jtc1/sc22/wg21/docs/papers/2013/n3804.html
//http://codereview.stackexchange.com/questions/20058/c11-any-class
//https://akrzemi1.wordpress.com/2013/10/10/too-perfect-forwarding/

#include <type_traits>
#include <typeinfo>
#include <utility>

class BadAnyCast : public std::bad_cast
{
};

class Any;

template<typename ValueType>
const ValueType* AnyCast(const Any* operand) noexcept;

template<typename ValueType>
ValueType* AnyCast(Any* operand) noexcept;

class Any
{
public:
    Any() noexcept
        : ptr(nullptr)
    {
    }

    Any(const Any& that)
        : ptr(that.clone())
    {
    }

    Any(Any&& that) noexcept
        : ptr(that.ptr)
    {
        that.ptr = nullptr;
    }

    template<bool B, class T = void>
    using enable_if_t = typename std::enable_if<B, T>::type;

    template<typename ValueType, typename = enable_if_t<!std::is_same<typename std::decay<ValueType>::type, Any>::value>>
    Any(ValueType&& value)
        : ptr(new Derived<typename std::decay<ValueType>::type>(std::forward<ValueType>(value)))
    {
        static_assert(std::is_copy_constructible<typename std::decay<ValueType>::type>::value, "std::decay<ValueType>::type shall satisfy the CopyConstructible requirements.");
    }

    ~Any()
    {
        clear();
    }

    // assignments

    Any& operator=(const Any& rhs)
    {
        if (ptr == rhs.ptr)
        {
            return *this;
        }

        auto old_ptr = ptr;

        ptr = rhs.clone();

        if (old_ptr)
        {
            delete old_ptr;
        }

        return *this;
    }

    Any& operator=(Any&& rhs) noexcept
    {
        if (ptr == rhs.ptr)
        {
            return *this;
        }

        std::swap(ptr, rhs.ptr);

        return *this;
    }

    template<typename ValueType, typename = enable_if_t<!std::is_same<typename std::decay<ValueType>::type, Any>::value>>
    Any& operator=(ValueType&& rhs)
    {
        static_assert(std::is_copy_constructible<typename std::decay<ValueType>::type>::value, "std::decay<ValueType>::type shall satisfy the CopyConstructible requirements.");

        Base* new_ptr = new Derived<typename std::decay<ValueType>::type>(std::forward<ValueType>(rhs));

        std::swap(ptr, new_ptr);

        if (new_ptr)
        {
            delete new_ptr;
        }

        return *this;
    }

    // modifiers

    void clear() noexcept
    {
        if (ptr)
        {
            delete ptr;
            ptr = nullptr;
        }
    }

    void swap(Any& rhs) noexcept
    {
        if (ptr == rhs.ptr)
        {
            return;
        }

        std::swap(ptr, rhs.ptr);
    }

    // observers

    bool empty() const noexcept
    {
        return ptr == nullptr;
    }

    const std::type_info& type() const noexcept
    {
        if (ptr != nullptr)
        {
            return ptr->type();
        }
        else
        {
            return typeid(void);
        }
    }

private:
    struct Base
    {
        virtual ~Base() {}
        virtual Base* clone() const = 0;
        virtual const std::type_info& type() const noexcept = 0;
    };

    template<typename T>
    struct Derived : Base
    {
        template<typename U>
        Derived(U&& value) : value(std::forward<U>(value)) { }
        T value;
        Base* clone() const { return new Derived<T>(value); }
        const std::type_info& type() const noexcept { return typeid(T); };
    };

    Base* clone() const
    {
        if (ptr != nullptr)
        {
            return ptr->clone();
        }
        else
        {
            return nullptr;
        }
    }

    Base* ptr;

    template<typename ValueType>
    friend const ValueType* AnyCast(const Any* operand) noexcept;

    template<typename ValueType>
    friend ValueType* AnyCast(Any* operand) noexcept;
};

inline void swap(Any& x, Any& y) noexcept
{
    x.swap(y);
}

template<typename ValueType>
ValueType AnyCast(const Any& operand)
{
    static_assert(std::is_reference<ValueType>::value || std::is_copy_constructible<ValueType>::value, "ValueType shall be a reference or satisfy the CopyConstructible requirements.");
    if (operand.type() != typeid(typename std::remove_reference<ValueType>::type)) { throw BadAnyCast(); }
    return *AnyCast<typename std::add_const<typename std::remove_reference<ValueType>::type>::type>(&operand);
}

template<typename ValueType>
ValueType AnyCast(Any& operand)
{
    static_assert(std::is_reference<ValueType>::value || std::is_copy_constructible<ValueType>::value, "ValueType shall be a reference or satisfy the CopyConstructible requirements.");
    if (operand.type() != typeid(typename std::remove_reference<ValueType>::type)) { throw BadAnyCast(); }
    return *AnyCast<typename std::remove_reference<ValueType>::type>(&operand);
}

template<typename ValueType>
ValueType AnyCast(Any&& operand)
{
    static_assert(std::is_reference<ValueType>::value || std::is_copy_constructible<ValueType>::value, "ValueType shall be a reference or satisfy the CopyConstructible requirements.");
    if (operand.type() != typeid(typename std::remove_reference<ValueType>::type)) { throw BadAnyCast(); }
    return *AnyCast<typename std::remove_reference<ValueType>::type>(&operand);
}

template<typename ValueType>
const ValueType* AnyCast(const Any* operand) noexcept
{
    static_assert(std::is_reference<ValueType>::value || std::is_copy_constructible<ValueType>::value, "ValueType shall be a reference or satisfy the CopyConstructible requirements.");
    if ((operand != nullptr) && (operand->type() == typeid(ValueType)))
    {
        return &dynamic_cast<Any::Derived<typename std::decay<ValueType>::type>*>(operand->ptr)->value;
    }
    else
    {
        return nullptr;
    }
}

template<typename ValueType>
ValueType* AnyCast(Any* operand) noexcept
{
    static_assert(std::is_reference<ValueType>::value || std::is_copy_constructible<ValueType>::value, "ValueType shall be a reference or satisfy the CopyConstructible requirements.");
    if ((operand != nullptr) && (operand->type() == typeid(ValueType)))
    {
        return &dynamic_cast<Any::Derived<typename std::decay<ValueType>::type>*>(operand->ptr)->value;
    }
    else
    {
        return nullptr;
    }
}
