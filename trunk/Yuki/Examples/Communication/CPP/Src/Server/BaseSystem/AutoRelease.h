#pragma once

#include <functional>

namespace Communication
{
    namespace BaseSystem
    {
        class AutoRelease /* final */
        {
        private:
            std::function<void()> ReleaseAction;
        public:
            AutoRelease(std::function<void()> ReleaseAction)
            {
                this->ReleaseAction = ReleaseAction;
            }

            ~AutoRelease()
            {
                ReleaseAction();
            }
        };
    }
}
