#pragma once

#include <functional>
#include <string>

namespace Client
{
    class ClientContext
    {
    public:
        std::function<void(std::wstring)> DequeueCallback;
    };
}
