#pragma once

#include "BaseSystem/LockedVariable.h"

#include <queue>
#include <list>
#include <functional>
#include <memory>
#include <exception>

namespace Server
{
    /// <summary>
    /// 本类的公共成员均是线程安全的。
    /// </summary>
    template<typename TRead, typename TWrite>
    class SessionStateMachine : public std::enable_shared_from_this<SessionStateMachine<TRead, TWrite>>
    {
    private:
        class Context
        {
        public:
            //0 初始
            //1 空闲
            //2 写入中
            //3 执行中
            //4 结束
            int State;

            bool IsReadEnabled;
            bool IsWriteEnabled;
            std::queue<TRead> Reads;
            std::queue<TWrite> Writes;
            bool IsInRawRead;
            bool IsReadShutDown;
            bool IsWriteShutDown;

            Context()
                : State(0), IsReadEnabled(false), IsWriteEnabled(false), IsInRawRead(false), IsReadShutDown(false), IsWriteShutDown(false)
            {
            }
        };

        class SessionActionQueue
        {
        public:
            bool IsRunning;
            std::list<std::function<void()>> Queue;

            SessionActionQueue()
                : IsRunning(false)
            {
            }
        };

        BaseSystem::LockedVariable<std::shared_ptr<Context>> c;
        BaseSystem::LockedVariable<std::shared_ptr<SessionActionQueue>> ActionQueue;
        std::function<bool(const std::exception &)> IsKnownException;
        std::function<void(const std::exception &)> OnCriticalError;
        std::function<void()> OnShutdownRead;
        std::function<void()> OnShutdownWrite;
        std::function<void(TWrite, std::function<void()>, std::function<void()>)> OnWrite;
        std::function<void(TRead, std::function<void()>, std::function<void()>)> OnExecute;
        std::function<void(std::function<void(std::shared_ptr<std::vector<TRead>>)>, std::function<void()>)> OnStartRawRead;
        std::function<void()> OnExit;
        std::function<void(std::function<void()>)> QueueUserWorkItem;

    public:
        SessionStateMachine(std::function<bool(const std::exception &)> IsKnownException, std::function<void(const std::exception &)> OnCriticalError, std::function<void()> OnShutdownRead, std::function<void()> OnShutdownWrite, std::function<void(TWrite, std::function<void()>, std::function<void()>)> OnWrite, std::function<void(TRead, std::function<void()>, std::function<void()>)> OnExecute, std::function<void(std::function<void(std::shared_ptr<std::vector<TRead>>)>, std::function<void()>)> OnStartRawRead, std::function<void()> OnExit, std::function<void(std::function<void()>)> QueueUserWorkItem)
            : c(nullptr), ActionQueue(std::make_shared<SessionActionQueue>()), IsKnownException(IsKnownException), OnCriticalError(OnCriticalError), OnShutdownRead(OnShutdownRead), OnShutdownWrite(OnShutdownWrite), OnWrite(OnWrite), OnExecute(OnExecute), OnStartRawRead(OnStartRawRead), OnExit(OnExit), QueueUserWorkItem(QueueUserWorkItem)
        {
            c.Update([](std::shared_ptr<Context> cc)
            {
                auto c = std::make_shared<Context>();
                c->State = 0;
                c->IsReadEnabled = true;
                c->IsWriteEnabled = true;
                c->IsInRawRead = false;
                c->IsReadShutDown = false;
                c->IsWriteShutDown = false;
                return c;
            });
        }

        void Start()
        {
            AddToActionQueue([this]() { Check(); });
        }

    private:
        void Check()
        {
            std::function<void()> AfterAction = nullptr;
            c.DoAction([&](std::shared_ptr<Context> cc)
            {
                if (cc->State == 0)
                {
                    cc->State = 1;
                    AfterAction = [=]() { AddToActionQueue([this]() { Check(); }); };
                    return;
                }
                if (cc->State == 1)
                {
                    if (cc->IsWriteEnabled)
                    {
                        if (cc->Writes.size() > 0)
                        {
                            cc->State = 2;
                            auto w = cc->Writes.front();
                            cc->Writes.pop();
                            AfterAction = [=]() { AddToActionQueue([=]() { Write(w); }); };
                            return;
                        }
                    }
                    if (cc->IsReadEnabled)
                    {
                        if (cc->Reads.size() > 0)
                        {
                            cc->State = 3;
                            auto r = cc->Reads.front();
                            cc->Reads.pop();
                            AfterAction = [=]() { AddToActionQueue([=]() { Execute(r); }); };
                            return;
                        }
                        if (!cc->IsInRawRead)
                        {
                            cc->IsInRawRead = true;
                            AfterAction = [=]() { OnStartRawRead([this](std::shared_ptr<std::vector<TRead>> Reads) { NotifyStartRawReadSuccess(Reads); }, [this]() { NotifyStartRawReadFailure(); }); };
                            return;
                        }
                    }
                    else
                    {
                        if (!cc->IsReadShutDown)
                        {
                            cc->IsReadShutDown = true;
                            AfterAction = [=]()
                            {
                                OnShutdownRead();
                                AddToActionQueue([this]() { Check(); });
                            };
                            return;
                        }
                        if (cc->IsWriteEnabled)
                        {
                            if (!cc->IsWriteShutDown)
                            {
                                cc->IsWriteEnabled = false;
                                cc->IsWriteShutDown = true;
                                AfterAction = [=]()
                                {
                                    OnShutdownWrite();
                                    AddToActionQueue([this]() { Check(); });
                                };
                                return;
                            }
                        }
                        else
                        {
                            if (cc->IsReadShutDown && cc->IsWriteShutDown && !cc->IsInRawRead)
                            {
                                cc->State = 4;
                                AfterAction = OnExit;
                                return;
                            }
                        }
                    }
                }
            });

            if (AfterAction != nullptr)
            {
                AfterAction();
            }
        }

        void Write(TWrite w)
        {
            auto OnSuccess = [=]()
            {
                c.DoAction([](std::shared_ptr<Context> cc)
                {
                    if (cc->State == 2)
                    {
                        cc->State = 1;
                    }
                });
                AddToActionQueue([this]() { Check(); });
            };
            auto OnFailure = [this]() { NotifyFailure(); };
            OnWrite(w, OnSuccess, OnFailure);
        }
        void Execute(TRead r)
        {
            auto OnSuccess = [=]()
            {
                c.DoAction([](std::shared_ptr<Context> cc)
                {
                    if (cc->State == 3)
                    {
                        cc->State = 1;
                    }
                });
                AddToActionQueue([this]() { Check(); });
            };
            auto OnFailure = [this]() { NotifyFailure(); };
            OnExecute(r, OnSuccess, OnFailure);
        }
    public:
        void NotifyWrite(TWrite w)
        {
            c.DoAction([=](std::shared_ptr<Context> cc)
            {
                if (cc->IsWriteEnabled)
                {
                    cc->Writes.push(w);
                }
            });
            AddToActionQueue([this]() { Check(); });
        }
    private:
        void NotifyStartRawReadSuccess(std::shared_ptr<std::vector<TRead>> Reads)
        {
            c.DoAction([=](std::shared_ptr<Context> cc)
            {
                if (cc->IsReadEnabled)
                {
                    for (auto r : *Reads)
                    {
                        cc->Reads.push(r);
                    }
                }
                cc->IsInRawRead = false;
            });
            AddToActionQueue([this]() { Check(); });
        }

    public:
        void NotifyFailure()
        {
            auto IsReadShutDown = false;
            auto IsWriteShutDown = false;
            c.DoAction([&](std::shared_ptr<Context> cc)
            {
                if (cc->State != 4)
                {
                    cc->State = 1;
                }
                cc->IsReadEnabled = false;
                cc->IsWriteEnabled = false;
                IsReadShutDown = cc->IsReadShutDown;
                cc->IsReadShutDown = true;
                IsWriteShutDown = cc->IsWriteShutDown;
                cc->IsWriteShutDown = true;
            });
            if (!IsReadShutDown)
            {
                OnShutdownRead();
            }
            if (!IsWriteShutDown)
            {
                OnShutdownWrite();
            }
            AddToActionQueue([this]() { Check(); });
        }
    private:
        void NotifyStartRawReadFailure()
        {
            auto IsReadShutDown = false;
            auto IsWriteShutDown = false;
            c.DoAction([&](std::shared_ptr<Context> cc)
            {
                if (cc->State != 4)
                {
                    cc->State = 1;
                }
                cc->IsReadEnabled = false;
                cc->IsWriteEnabled = false;
                IsReadShutDown = cc->IsReadShutDown;
                cc->IsReadShutDown = true;
                IsWriteShutDown = cc->IsWriteShutDown;
                cc->IsWriteShutDown = true;
                cc->IsInRawRead = false;
            });
            if (!IsReadShutDown)
            {
                OnShutdownRead();
            }
            if (!IsWriteShutDown)
            {
                OnShutdownWrite();
            }
            AddToActionQueue([this]() { Check(); });
        }

    public:
        void NotifyExit()
        {
            c.DoAction([](std::shared_ptr<Context> cc)
            {
                if (cc->State != 4)
                {
                    cc->State = 1;
                }
                cc->IsReadEnabled = false;
            });
            AddToActionQueue([this]() { Check(); });
        }

        bool IsExited()
        {
            return c.Check<bool>([](std::shared_ptr<Context> cc) { return cc->State == 4; });
        }

        void AddToActionQueue(std::function<void()> Action)
        {
            ActionQueue.DoAction([=](std::shared_ptr<SessionActionQueue> q)
            {
                q->Queue.push_back(Action);
                if (!q->IsRunning)
                {
                    q->IsRunning = true;
                    auto ThisPtr = this->shared_from_this();
                    QueueUserWorkItem([ThisPtr]() { ThisPtr->ExecuteActionQueue(); });
                }
            });
        }
    private:
        void ExecuteActionQueue()
        {
            int Count = 64;
            while (true)
            {
                std::function<void()> a = nullptr;
                ActionQueue.DoAction([&](std::shared_ptr<SessionActionQueue> q)
                {
                    if (q->Queue.size() > 0)
                    {
                        a = q->Queue.front();
                        q->Queue.pop_front();
                    }
                    else
                    {
                        q->IsRunning = false;
                    }
                });
                if (a == nullptr)
                {
                    return;
                }
                try
                {
                    a();
                }
                catch (std::exception &ex)
                {
                    if (!IsKnownException(ex))
                    {
                        OnCriticalError(ex);
                    }
                    NotifyFailure();
                }
                Count -= 1;
                if (Count == 0) { break; }
            }
            auto ThisPtr = this->shared_from_this();
            QueueUserWorkItem([ThisPtr]() { ThisPtr->ExecuteActionQueue(); });
        }
    };
}
