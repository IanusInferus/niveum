#include <functional>

#if defined(_MSC_VER)

#include <windows.h>

struct Context
{
    HANDLE WaitHandle;
    HANDLE hThread2;
    std::function<void()> f;
};

static void CALLBACK Callback(_In_ PVOID lpParameter, _In_ BOOLEAN TimerOrWaitFired)
{
    auto c = (Context *)lpParameter;
    if (UnregisterWait(c->WaitHandle) == 0) { throw "UnexpectedReturnValue"; }
    if (CloseHandle(c->hThread2) == 0) { throw "UnexpectedReturnValue"; }
    c->f();
}

void do_at_thread_exit(std::function<void()> f)
{
    auto hProcess = GetCurrentProcess();
    auto hThread = GetCurrentThread();
    HANDLE hThread2;
    if (DuplicateHandle(hProcess, hThread, hProcess, &hThread2, NULL, FALSE, DUPLICATE_SAME_ACCESS) == 0) { throw "UnexpectedReturnValue"; }

    auto c = new Context();
    c->WaitHandle = NULL;
    c->hThread2 = hThread2;
    c->f = f;

    if (RegisterWaitForSingleObject(&c->WaitHandle, hThread2, Callback, c, INFINITE, WT_EXECUTEINWAITTHREAD | WT_EXECUTEONLYONCE) == 0) { throw "UnexpectedReturnValue"; }
}

#else

//#include <boost/thread.hpp>
//void do_at_thread_exit(std::function<void()> f)
//{
//    boost::this_thread::at_thread_exit(f);
//}

#include <stack>
#include <pthread.h>

static pthread_key_t key;
static pthread_once_t key_once = PTHREAD_ONCE_INIT;

static void destory_at_exit(void *value)
{
    if (value == nullptr) { return; }
    auto v = (std::stack<std::function<void()>> *)(value);
    while (v->size() > 0)
    {
        auto f = v->top();
        v->pop();
        f();
    }
    delete v;
}

static void make_key()
{
    pthread_key_create(&key, destory_at_exit);
}

void do_at_thread_exit(std::function<void()> f)
{
    pthread_once(&key_once, make_key);
    auto v = (std::stack<std::function<void()>> *)(pthread_getspecific(key));
    if (v == nullptr)
    {
        v = new std::stack<std::function<void()>>();
        pthread_setspecific(key, v);
    }
    v->push(f);
}

#endif
