#pragma once

#include <boost/thread.hpp>

namespace BaseSystem
{
    class AutoResetEvent
    {
    private:
        bool SetValue;
        boost::mutex Lockee;
        boost::condition_variable ConditionVariable;
    public:
        AutoResetEvent()
            : SetValue(false)
        {
        }

        void WaitOne()
        {
            boost::unique_lock<boost::mutex> Lock(Lockee);
            while (!SetValue)
            {
                ConditionVariable.wait(Lock);
            }
            SetValue = false;
        }

        void Set()
        {
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                SetValue = true;
            }
            ConditionVariable.notify_one();
        }
    };
}
