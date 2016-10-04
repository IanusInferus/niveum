#pragma once

#include <functional>

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

        void Execute()
        {
            if (ReleaseAction != nullptr)
            {
                ReleaseAction();
                ReleaseAction = nullptr;
            }
        }

        void Suppress()
        {
            ReleaseAction = nullptr;
        }

        ~AutoRelease()
        {
            if (ReleaseAction != nullptr)
            {
                ReleaseAction();
            }
        }
    };
}
