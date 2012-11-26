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

        virtual void UpsertOneTestRecord(std::shared_ptr<TestRecord> v) = 0;
        virtual Optional<std::shared_ptr<TestRecord>> SelectOptionalTestRecordBySessionIndex(int SessionIndex) = 0;
        virtual void UpsertOneTestLockRecord(std::shared_ptr<TestLockRecord> v) = 0;
        virtual Optional<std::shared_ptr<TestLockRecord>> SelectOptionalTestLockRecordById(int Id) = 0;
        virtual Optional<std::shared_ptr<TestLockRecord>> LockOptionalTestLockRecordById(int Id) = 0;
	};
}
