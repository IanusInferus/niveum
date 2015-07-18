#pragma once

#include "BaseSystem/AutoResetEvent.h"
#include "BaseSystem/Times.h"
#include "Util/SessionLogEntry.h"

#include <memory>
#include <string>
#include <queue>
#include <cstdio>
#include <mutex>
#include <thread>

namespace Server
{
    class ConsoleLogger
    {
    private:
        std::shared_ptr<std::thread> LogThread;
        BaseSystem::AutoResetEvent LogNotifier;

        std::mutex Lockee;
        std::queue<std::shared_ptr<SessionLogEntry>> Entries;
        bool IsExited;

        void Consume()
        {
            while (true)
            {
                LogNotifier.WaitOne();
                while (true)
                {
                    if (IsExited) { return; }
                    std::shared_ptr<SessionLogEntry> e = nullptr;
                    {
                        std::unique_lock<std::mutex> Lock(Lockee);
                        if (Entries.size() > 0)
                        {
                            e = Entries.front();
                            Entries.pop();
                        }
                    }
                    if (e == nullptr)
                    {
                        break;
                    }

                    auto Line = DateTimeUtcToString(e->Time) + L" " + e->Token + L" " + e->Type + L" " + e->Name + L" " + e->Message;
                    std::wprintf(L"%ls\n", Line.c_str());
                }
            }
        }

    public:
        ConsoleLogger()
            : LogThread(nullptr),
              IsExited(false)
        {
            LogThread = std::make_shared<std::thread>([=]() { Consume(); });
        }

        ~ConsoleLogger()
        {
            {
                std::unique_lock<std::mutex> Lock(Lockee);
                IsExited = true;
            }
            LogNotifier.Set();
            if (LogThread != nullptr)
            {
                LogThread->join();
                LogThread = nullptr;
            }
        }

        void Log(std::shared_ptr<SessionLogEntry> e)
        {
            {
                std::unique_lock<std::mutex> Lock(Lockee);
                Entries.push(e);
            }
            LogNotifier.Set();
        }
    };
}
