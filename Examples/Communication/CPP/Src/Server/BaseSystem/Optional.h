#pragma once

#include <functional>
#include <boost/thread.hpp>

namespace Communication
{
    namespace BaseSystem
    {
        enum OptionalTag
        {
            /// <summary>没有值</summary>
            OptionalTag_NotHasValue = 0,
            /// <summary>有值</summary>
            OptionalTag_HasValue = 1
        };
        /// <summary>可选类型</summary>
        /* TaggedUnion */
        template<typename T>
        class Optional /* final */
        {
        public:
            /* Tag */ OptionalTag _Tag;
        
            /// <summary>没有值</summary>
            Unit NotHasValue;
            /// <summary>有值</summary>
            T HasValue;
        
            /// <summary>没有值</summary>
            static std::shared_ptr<Optional<T>> CreateNotHasValue()
            {
                auto r = std::make_shared<Optional<T>>();
                r->_Tag = OptionalTag_NotHasValue;
                r->NotHasValue = Unit();
                return r;
            }
            /// <summary>有值</summary>
            static std::shared_ptr<Optional<T>> CreateHasValue(T Value)
            {
                auto r = std::make_shared<Optional<T>>();
                r->_Tag = OptionalTag_HasValue;
                r->HasValue = Value;
                return r;
            }
        
            /// <summary>没有值</summary>
            Boolean OnNotHasValue()
            {
                return _Tag == OptionalTag_NotHasValue;
            }
            /// <summary>有值</summary>
            Boolean OnHasValue()
            {
                return _Tag == OptionalTag_HasValue;
            }
        };
    }
}
