#pragma once

#include <functional>
#include <mutex>

namespace BaseSystem
{
    template <typename T>
    class LockedVariable
    {
    private:
        T Value;
        std::mutex Lockee;
    public:
        LockedVariable(T Value)
            : Value(Value)
        {
        }

        template <typename S>
        S Check(std::function<S(const T &)> Map)
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            return Map(Value);
        }

        void DoAction(std::function<void(T &)> Action)
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            Action(Value);
        }

        void Update(std::function<T(const T &)> Map)
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            Value = Map(Value);
        }
    };
}
