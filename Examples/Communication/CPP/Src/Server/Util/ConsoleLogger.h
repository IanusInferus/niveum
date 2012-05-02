#pragma once

#include "BaseSystem/AutoResetEvent.h"
#include "Util/SessionLogEntry.h"

#include <memory>
#include <string>
#include <queue>
#include <cstdio>
#include <boost/thread.hpp>

namespace Server
{
    class ConsoleLogger
    {
    private:
        std::shared_ptr<boost::thread> LogThread;
        Communication::BaseSystem::AutoResetEvent LogNotifier;

        boost::mutex Lockee;
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
                        boost::unique_lock<boost::mutex> Lock(Lockee);
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

                    auto Line = L"\"" + boost::posix_time::to_iso_extended_wstring(e->Time) + L"Z" + L"\"" + L"\t" + L"\"" + e->Type + L"\"" + L"\t" + L"\"" + e->Message + L"\"";
                    std::wprintf(L"%ls\n", Line.c_str());
                }
            }
        }

    public:
        ConsoleLogger()
            : LogThread(nullptr),
              IsExited(false)
        {
            LogThread = std::make_shared<boost::thread>([=]() { Consume(); });
        }

        ~ConsoleLogger()
        {
            {
                boost::unique_lock<boost::mutex> Lock(Lockee);
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
                boost::unique_lock<boost::mutex> Lock(Lockee);
                Entries.push(e);
            }
            LogNotifier.Set();
        }
    };
}
