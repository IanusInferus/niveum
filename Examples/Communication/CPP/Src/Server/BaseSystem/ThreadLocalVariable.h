#pragma once

#include <functional>
#include <map>
#include <boost/thread.hpp>

namespace Communication
{
    namespace BaseSystem
    {
        // notice that boost::thread_specific_ptr is broken in openSUSE 12.1.
        // it causes segmentation fault when you call the reset function.
        // so I use the thread id to do a stable implementation.
        template <typename T>
        class ThreadLocalVariable : public std::enable_shared_from_this<ThreadLocalVariable<T>>
        {
        private:
            std::map<boost::thread::id, std::shared_ptr<T>> Mappings;
            std::function<std::shared_ptr<T>()> Factory;
            boost::mutex Lockee;
        public:
            ThreadLocalVariable(std::function<std::shared_ptr<T>()> Factory)
                : Factory(Factory)
            {
            }

            ~ThreadLocalVariable()
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                Mappings.clear();
                Factory = nullptr;
            }

            std::shared_ptr<T> Value()
            {
                auto id = boost::this_thread::get_id();

                {
                    boost::unique_lock<boost::mutex> Lock(Lockee);
                    if (Mappings.count(id) > 0)
                    {
                        return Mappings[id];
                    }
                    else if (Factory == nullptr)
                    {
                        return nullptr;
                    }
                    else
                    {
                        auto v = Factory();
                        Mappings[id] = v;
                        auto ThisPtr = this->shared_from_this();
                        boost::this_thread::at_thread_exit([ThisPtr, id]()
                        {
                            boost::unique_lock<boost::mutex> Lock(ThisPtr->Lockee);
                            if (ThisPtr->Mappings.count(id) > 0)
                            {
                                ThisPtr->Mappings.erase(id);
                            }
                        });
                        return v;
                    }
                }
            }
        };
    }
}
