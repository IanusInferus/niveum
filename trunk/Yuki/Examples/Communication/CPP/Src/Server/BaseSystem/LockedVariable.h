#pragma once

#include <functional>
#include <boost/thread.hpp>

namespace Communication
{
    namespace BaseSystem
    {
        template <typename T>
        class LockedVariable
        {
        private:
            T Value;
            boost::mutex Lockee;
        public:
            LockedVariable(T Value)
                : Value(Value)
            {
            }

            template <typename S>
            S Check(std::function<S(const T &)> Map)
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                return Map(Value);
            }

            void DoAction(std::function<void(T &)> Action)
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                Action(Value);
            }

            void Update(std::function<T(const T &)> Map)
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                Value = Map(Value);
            }
        };
    }
}
