#pragma once

#include <functional>
#include <boost/thread.hpp>

namespace Communication
{
    namespace BaseSystem
    {
        template <typename T>
        class ThreadLocalVariable
        {
        private:
            std::function<T *()> Factory;
            boost::thread_specific_ptr<T> ThreadSpecifier;
        public:
            ThreadLocalVariable(std::function<T *()> Factory)
                : Factory(Factory),
                  ThreadSpecifier()
            {
            }

            T &Value()
            {
                if (ThreadSpecifier.get() == nullptr)
                {
                    ThreadSpecifier.reset(Factory());
                }
                return *ThreadSpecifier.get();
            }
        };
    }
}
