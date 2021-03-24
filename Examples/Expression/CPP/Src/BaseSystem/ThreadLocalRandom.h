#pragma once

#include <random>

namespace BaseSystem
{
    class ThreadLocalRandom final
    {
    private:
        thread_local static std::default_random_engine re;
    public:
        ThreadLocalRandom() = delete;

        /// <summary>获得范围为[0, MaxValue]中的随机整数</summary>
        template <typename T>
        static inline T NextInt(T MaxValue)
        {
            return NextInt<T>(0, MaxValue);
        }

        /// <summary>获得范围为[MinValue, MaxValue]中的随机整数</summary>
        template <typename T>
        static inline T NextInt(T MinValue, T MaxValue)
        {
            std::uniform_int_distribution<T> uid(MinValue, MaxValue);
            return uid(re);
        }

        /// <summary>获得范围为[MinValue, MaxValue)中的随机数</summary>
        template <typename T>
        static inline T NextReal(T MinValue, T MaxValue)
        {
            std::uniform_real_distribution<T> urd(MinValue, MaxValue);
            return urd(re);
        }
    };

    /// <summary>获得范围为[0, MaxValue]中的随机整数，由于C++11标准中std::uniform_int_distribution无法使用std::uint8_t和std::int8_t，需要绕过</summary>
    template <>
    inline std::uint8_t ThreadLocalRandom::NextInt<std::uint8_t>(std::uint8_t MinValue, std::uint8_t MaxValue)
    {
        std::uniform_int_distribution<std::uint32_t> uid(MinValue, MaxValue);
        return static_cast<std::uint8_t>(uid(re));
    }
    /// <summary>获得范围为[0, MaxValue]中的随机整数，由于C++11标准中std::uniform_int_distribution无法使用std::uint8_t和std::int8_t，需要绕过</summary>
    template <>
    inline std::int8_t ThreadLocalRandom::NextInt<std::int8_t>(std::int8_t MinValue, std::int8_t MaxValue)
    {
        std::uniform_int_distribution<std::int32_t> uid(MinValue, MaxValue);
        return static_cast<std::int8_t>(uid(re));
    }
}
