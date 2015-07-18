#pragma once

#include <mutex>

namespace BaseSystem
{
    class CancellationToken
    {
    private:
        bool Cancelled;
        std::mutex Lockee;
    public:
        CancellationToken()
            : Cancelled(false)
        {
        }

        bool IsCancellationRequested()
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            return Cancelled;
        }

        void Cancel()
        {
            std::unique_lock<std::mutex> Lock(Lockee);
            Cancelled = true;
        }
    };
}
