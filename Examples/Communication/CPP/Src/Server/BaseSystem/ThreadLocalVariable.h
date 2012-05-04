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
            boost::mutex Lockee;
        private:
            static void Cleanup(T *p)
            {
                if (p == nullptr) { return; }
                delete p;
            }
        public:
            ThreadLocalVariable(std::function<T *()> Factory)
                : Factory(Factory),
                  ThreadSpecifier(Cleanup)
            {
            }

            T &Value()
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                if (ThreadSpecifier.get() == nullptr)
                {
                    ThreadSpecifier.reset(Factory());
                }
                return *ThreadSpecifier.get();
            }
        };
    }
}
