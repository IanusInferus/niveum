#pragma once

#if defined(_MSC_VER) && (_MSC_VER < 1900)

#include <functional>
#include <memory>
#include <ppl.h>

namespace BaseSystem
{
    template <typename T>
    class ThreadLocalVariable
    {
    private:
        std::function<std::shared_ptr<T>()> Factory;
        concurrency::combinable<std::shared_ptr<T>> InnerValue;
    public:
        ThreadLocalVariable(std::function<std::shared_ptr<T>()> Factory)
            : Factory(Factory)
        {
        }

        ~ThreadLocalVariable()
        {
            InnerValue.local() = nullptr;
            Factory = nullptr;
        }

        std::shared_ptr<T> Value()
        {
            auto v = InnerValue.local();
            if ((v == nullptr) && (Factory != nullptr))
            {
                v = Factory();
                InnerValue.local() = v;
            }
            return v;
        }
    };
}

#elif 0 //C++11

#include <functional>
#include <memory>
#include <mutex>
#include <map>

namespace BaseSystem
{
    template <typename T>
    int ThreadLocalVariable_TotalObjectCount = 0;

    template <typename T>
    thread_local std::map<int, std::shared_ptr<T>> ThreadLocalVariable_Mappings{};

    template <typename T>
    class ThreadLocalVariable
    {
    private:
        std::function<std::shared_ptr<T>()> Factory;
        std::mutex Lockee;
        int CurrentObjectIndex;
    public:
        ThreadLocalVariable(std::function<std::shared_ptr<T>()> Factory)
            : Factory(Factory)
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            CurrentObjectIndex = ThreadLocalVariable_TotalObjectCount<T>;
            ThreadLocalVariable_TotalObjectCount<T> += 1;
        }

        std::shared_ptr<T> Value()
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            std::shared_ptr<T> v = nullptr;
            if (ThreadLocalVariable_Mappings<T>.count(CurrentObjectIndex) > 0)
            {
                return ThreadLocalVariable_Mappings<T>[CurrentObjectIndex];
            }
            if (Factory != nullptr)
            {
                v = Factory();
            }
            else
            {
                v = nullptr;
            }
            ThreadLocalVariable_Mappings<T>[CurrentObjectIndex] = v;
            return v;
        }
    };
}

#else //do_at_thread_exit

#include <functional>
#include <memory>
#include <map>
#include <mutex>
#include <thread>

void do_at_thread_exit(std::function<void()> f);

namespace BaseSystem
{
    // notice that boost::thread_specific_ptr is broken in openSUSE 12.1.
    // it causes segmentation fault when you call the reset function.
    // so I use the thread id to do a stable implementation.
    template <typename T>
    class ThreadLocalVariable : public std::enable_shared_from_this<ThreadLocalVariable<T>>
    {
    private:
        std::map<std::thread::id, std::shared_ptr<T>> Mappings;
        std::function<std::shared_ptr<T>()> Factory;
        std::mutex Lockee;
    public:
        ThreadLocalVariable(std::function<std::shared_ptr<T>()> Factory)
            : Factory(Factory)
        {
        }

        ~ThreadLocalVariable()
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            Mappings.clear();
            Factory = nullptr;
        }

        std::shared_ptr<T> Value()
        {
            auto id = std::this_thread::get_id();

            {
                std::unique_lock<std::mutex> Lock(Lockee);
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
                    do_at_thread_exit([ThisPtr, id]()
                    {
                        std::unique_lock<std::mutex> Lock(ThisPtr->Lockee);
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

#endif
