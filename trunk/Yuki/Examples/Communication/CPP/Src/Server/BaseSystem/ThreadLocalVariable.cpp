#include <functional>

#if defined(_MSC_VER) && (_MSC_VER < 1900)

#elif 0 //C++11

#else //do_at_thread_exit

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
