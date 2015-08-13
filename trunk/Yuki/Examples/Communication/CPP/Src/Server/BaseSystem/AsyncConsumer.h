#pragma once

#include <queue>
#include <functional>
#include <mutex>

namespace BaseSystem
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    template<typename T>
    class AsyncConsumer
    {
    private:
        std::function<void(std::function<void()>)> QueueUserWorkItem;
        std::function<bool(T)> DoConsume;
        int MaxConsumerCount;
        std::queue<T> Entries;
        std::mutex EntriesMutex;
        int RunningCount;
        bool IsExited;

    public:
        AsyncConsumer(std::function<void(std::function<void()>)> QueueUserWorkItem, std::function<bool(T)> DoConsume, int MaxConsumerCount)
            : QueueUserWorkItem(QueueUserWorkItem), DoConsume(DoConsume), MaxConsumerCount(MaxConsumerCount), RunningCount(0), IsExited(false)
        {
        }

        void Push(T Entry)
        {
            bool NeedToRun = false;
            {
                std::unique_lock<std::mutex> Lock(EntriesMutex);
                if (IsExited)
                {
                    return;
                }
                Entries.push(Entry);
                if (RunningCount < MaxConsumerCount)
                {
                    RunningCount += 1;
                    NeedToRun = true;
                }
            }
            if (NeedToRun)
            {
                QueueUserWorkItem([this]() { Run(); });
            }
        }

    private:
        void Run()
        {
            T e;
            {
                std::unique_lock<std::mutex> Lock(EntriesMutex);
                if (IsExited)
                {
                    RunningCount -= 1;
                    return;
                }
                if (Entries.size() > 0)
                {
                    e = Entries.front();
                    Entries.pop();
                }
                else
                {
                    RunningCount -= 1;
                    return;
                }
            }
            if (!DoConsume(e))
            {
                std::unique_lock<std::mutex> Lock(EntriesMutex);
                IsExited = true;
                RunningCount -= 1;
                return;
            }
            QueueUserWorkItem([this]() { Run(); });
        }

    public:
        void DoOne()
        {
            T e;
            std::unique_lock<std::mutex> Lock(EntriesMutex);
            if (IsExited)
            {
                return;
            }
            if (Entries.size() > 0)
            {
                e = Entries.front();
                Entries.pop();
            }
            else
            {
                return;
            }
            if (!DoConsume(e))
            {
                IsExited = true;
            }
        }

        ~AsyncConsumer()
        {
            std::unique_lock<std::mutex> Lock(EntriesMutex);
            while (!IsExited && Entries.size() > 0)
            {
                auto e = Entries.front();
                Entries.pop();
                if (!DoConsume(e))
                {
                    IsExited = true;
                    return;
                }
            }
            IsExited = true;
        }
    };
}
