#pragma once

#include <boost/thread.hpp>

namespace Communication
{
    namespace BaseSystem
    {
        class CancellationToken
        {
        private:
            bool Cancelled;
            boost::mutex Lockee;
        public:
            CancellationToken()
                : Cancelled(false)
            {
            }

            bool IsCancellationRequested()
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                return Cancelled;
            }

            void Cancel()
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
                Cancelled = true;
            }
        };
    }
}
