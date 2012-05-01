#pragma once

#include <functional>
#include <boost/thread.hpp>

namespace Communication
{
    namespace BaseSystem
    {
        enum OptionalTag
        {
            /// <summary>û��ֵ</summary>
            OptionalTag_NotHasValue = 0,
            /// <summary>��ֵ</summary>
            OptionalTag_HasValue = 1
        };
        /// <summary>��ѡ����</summary>
        /* TaggedUnion */
        template<typename T>
        class Optional /* final */
        {
        public:
            /* Tag */ OptionalTag _Tag;
        
            /// <summary>û��ֵ</summary>
            Unit NotHasValue;
            /// <summary>��ֵ</summary>
            T HasValue;
        
            /// <summary>û��ֵ</summary>
            static std::shared_ptr<Optional<T>> CreateNotHasValue()
            {
                auto r = std::make_shared<Optional<T>>();
                r->_Tag = OptionalTag_NotHasValue;
                r->NotHasValue = Unit();
                return r;
            }
            /// <summary>��ֵ</summary>
            static std::shared_ptr<Optional<T>> CreateHasValue(T Value)
            {
                auto r = std::make_shared<Optional<T>>();
                r->_Tag = OptionalTag_HasValue;
                r->HasValue = Value;
                return r;
            }
        
            /// <summary>û��ֵ</summary>
            Boolean OnNotHasValue()
            {
                return _Tag == OptionalTag_NotHasValue;
            }
            /// <summary>��ֵ</summary>
            Boolean OnHasValue()
            {
                return _Tag == OptionalTag_HasValue;
            }
        };
    }
}
