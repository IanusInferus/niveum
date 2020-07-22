#include "ThreadLocalVariable.h"

#if defined(_MSC_VER)

#include <windows.h>

namespace BaseSystem
{
namespace detail
{

struct Context : std::enable_shared_from_this<Context>
{
    HANDLE WaitHandle;
    HANDLE hThread2;
    std::mutex lockee;
    std::function<void()> f;
};

static void CALLBACK Callback(_In_ PVOID lpParameter, _In_ BOOLEAN TimerOrWaitFired)
{
    (void)(TimerOrWaitFired);
    auto c = (reinterpret_cast<Context *>(lpParameter))->shared_from_this();
    std::lock_guard<std::mutex> lock(c->lockee);
    if (UnregisterWait(c->WaitHandle) == 0) { throw std::runtime_error("UnexpectedReturnValue"); }
    if (CloseHandle(c->hThread2) == 0) { throw std::runtime_error("UnexpectedReturnValue"); }
    c->WaitHandle = NULL;
    c->hThread2 = NULL;
    c->f();
}

std::function<void()> do_at_thread_exit(std::function<void()> f)
{
    auto hProcess = GetCurrentProcess();
    auto hThread = GetCurrentThread();
    HANDLE hThread2;
    if (DuplicateHandle(hProcess, hThread, hProcess, &hThread2, NULL, FALSE, DUPLICATE_SAME_ACCESS) == 0) { throw std::runtime_error("UnexpectedReturnValue"); }

    auto c = std::make_shared<Context>();
    c->WaitHandle = NULL;
    c->hThread2 = hThread2;
    c->f = f;

    if (RegisterWaitForSingleObject(&c->WaitHandle, hThread2, Callback, c.get(), INFINITE, WT_EXECUTEINWAITTHREAD | WT_EXECUTEONLYONCE) == 0) { throw std::runtime_error("UnexpectedReturnValue"); }

    return [c]()
    {
        std::lock_guard<std::mutex> lock(c->lockee);
        if (c->WaitHandle == NULL) { return; }
        auto Result = UnregisterWait(c->WaitHandle);
        if (Result != 0) {
            if (CloseHandle(c->hThread2) == 0) { throw std::runtime_error("UnexpectedReturnValue"); }
        } else {
            auto Error = GetLastError();
            if (Error != ERROR_IO_PENDING) { throw std::runtime_error("UnexpectedReturnValue"); }
        }
    };
}

}
}

#else

#include <list>
#include <thread>
#include <pthread.h>
#include <cstdio>

namespace BaseSystem
{
namespace detail
{

struct Context
{
    std::mutex lockee;
    std::list<std::shared_ptr<std::function<void()>>> destroy_list;
};

static pthread_key_t key;
static pthread_once_t key_once = PTHREAD_ONCE_INIT;

static void destory_at_exit(void *value)
{
    if (value == nullptr) { return; }
    auto c = (Context *)(value);
    auto &destroy_list = c->destroy_list;
    while (!destroy_list.empty()) {
        auto pf = destroy_list.front();
        destroy_list.erase(destroy_list.begin());

        std::lock_guard<std::mutex> lock(c->lockee);
        if (pf != nullptr) {
            (*pf)();
        }
    }
    delete c; //destory_at_exit will not be called on main thread exit, as it will not call pthread_exit, but this will not cause a problem as the memory will be reclaimed by the system
}

static void make_key()
{
    pthread_key_create(&key, destory_at_exit);
}

std::function<void()> do_at_thread_exit(std::function<void()> f)
{
    pthread_once(&key_once, make_key);
    auto c = reinterpret_cast<Context *>(pthread_getspecific(key));
    if (c == nullptr) {
        c = new Context();
        pthread_setspecific(key, c);
    }
    auto pf = std::make_shared<std::function<void()>>(f);
    {
        std::lock_guard<std::mutex> lock(c->lockee);
        c->destroy_list.push_back(pf);
    }
    return [c, pf]()
    {
        std::lock_guard<std::mutex> lock(c->lockee);
        *pf = nullptr;
    };
}

}
}

#endif
