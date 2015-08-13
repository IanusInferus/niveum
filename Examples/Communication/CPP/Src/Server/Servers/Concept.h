#pragma once

namespace Server
{
    class IServer
    {
    public:
        virtual ~IServer() {}

        virtual void Start() = 0;
        virtual void Stop() = 0;
    };
}
