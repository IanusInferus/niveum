#pragma once

#include <mutex>
#include <condition_variable>

namespace BaseSystem
{
    class AutoResetEvent
    {
    private:
        bool SetValue;
        std::mutex Lockee;
        std::condition_variable ConditionVariable;
    public:
        AutoResetEvent()
            : SetValue(false)
        {
        }

        void WaitOne()
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            while (!SetValue)
            {
                ConditionVariable.wait(Lock);
            }
            SetValue = false;
        }

        void Set()
        {
            {
                std::unique_lock<std::mutex> Lock(Lockee);
                SetValue = true;
            }
            ConditionVariable.notify_one();
        }
    };
}
