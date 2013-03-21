#pragma once

#include "BaseSystem/ThreadLocalVariable.h"

#include <memory>
#include <random>

namespace BaseSystem
{
    class ThreadLocalRandom
    {
    private:
        std::shared_ptr<ThreadLocalVariable<std::default_random_engine>> re;
    public:
        ThreadLocalRandom()
            : re(nullptr)
        {
            re = std::make_shared<ThreadLocalVariable<std::default_random_engine>>([]() { return std::make_shared<std::default_random_engine>(std::random_device()()); });
        }

        ~ThreadLocalRandom()
        {
        }

        /// <summary>获得范围为[0, MaxValue]中的随机整数</summary>
        template <typename T>
        T NextInt(T MaxValue)
        {
            std::uniform_int_distribution<T> uid(0, MaxValue);
            return uid(*re->Value());
        }

        /// <summary>获得范围为[MinValue, MaxValue]中的随机整数</summary>
        template <typename T>
        T NextInt(T MinValue, T MaxValue)
        {
            std::uniform_int_distribution<T> uid(MinValue, MaxValue);
            return uid(*re->Value());
        }

        /// <summary>获得范围为[MinValue, MaxValue)中的随机数</summary>
        template <typename T>
        T NextReal(T MinValue, T MaxValue)
        {
            std::uniform_real_distribution<T> urd(MinValue, MaxValue);
            return urd(*re->Value());
        }
    };
}
