#pragma once

#include "DatabaseEntities.h"

#include <memory>

namespace Database
{
    class IDataAccess
    {
    public:
        virtual ~IDataAccess() {}
        virtual void Complete() = 0;

        virtual void FromTestRecordUpsertOne(std::shared_ptr<TestRecord> v) = 0;
        virtual Optional<std::shared_ptr<TestRecord>> FromTestRecordSelectOptionalBySessionIndex(int SessionIndex) = 0;
        virtual void FromTestLockRecordUpsertOne(std::shared_ptr<TestLockRecord> v) = 0;
        virtual Optional<std::shared_ptr<TestLockRecord>> FromTestLockRecordSelectOptionalById(int Id) = 0;
        virtual Optional<std::shared_ptr<TestLockRecord>> FromTestLockRecordLockOptionalById(int Id) = 0;
    };
}
