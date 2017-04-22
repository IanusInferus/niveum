//==========================================================================
//
//  File:        ExceptionStackTrace.h
//  Description: C++异常捕捉时获得代码栈
//  Version:     2017.04.22.
//  Author:      F.R.C.(地狱门神)
//  Copyright(C) Public Domain
//
//==========================================================================

#pragma once

#include <type_traits>
#include <string>

namespace ExceptionStackTrace
{
    namespace detail
    {
        void *Execute(void *obj, void *(*f)(void *));

        template<class TFunc, typename T>
        class FuncWrapper
        {
        public:
            TFunc Func;
            T Result;

            FuncWrapper(TFunc Func) : Func(Func) {}

            static inline void *Run(void *obj)
            {
                auto v = (FuncWrapper *)(obj);
                v->Result = v->Func();
                return nullptr;
            }
        };

        template<typename TFunc>
        class FuncWrapper<TFunc, void>
        {
        public:
            TFunc Func;

            FuncWrapper(TFunc Func) : Func(Func) {}

            static inline void *Run(void *obj)
            {
                auto v = (FuncWrapper *)(obj);
                v->Func();
                return nullptr;
            }
        };
    }

    bool IsDebuggerAttached();
    std::string GetStackTrace();
    std::string GetStackFrame(int Skip);

    template<typename TFunc>
    static inline auto Execute(TFunc f, typename std::enable_if<!std::is_void<decltype(f())>::value>::type * = nullptr) -> decltype(f())
    {
        typedef decltype(f()) T;
        detail::FuncWrapper<TFunc, T> v(f);
        detail::Execute(&v, detail::FuncWrapper<TFunc, T>::Run);
        return v.Result;
    }

    template<typename TFunc>
    static inline auto Execute(TFunc f, typename std::enable_if<std::is_void<decltype(f())>::value>::type * = nullptr) -> decltype(f())
    {
        detail::FuncWrapper<TFunc, void> v(f);
        detail::Execute(&v, detail::FuncWrapper<TFunc, void>::Run);
    }
}
