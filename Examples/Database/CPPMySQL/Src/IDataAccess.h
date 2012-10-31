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

        //{Select, Lock} x {Optional, One, Many, Range, All, Count} x {RecordName} (x {By} x {IndexName})? (x {OrderBy} x {IndexName})?
        //{Insert, Update, Upsert} x {One, Many} x {RecordName}
        //{Delete} x {One, Many, All} x {RecordName} x {By} x {IndexName}

        virtual void UpsertOneTestRecord(std::shared_ptr<TestRecord> v) = 0;
        virtual std::shared_ptr<TestRecord> SelectOptionalTestRecord(int SessionIndex) = 0;
        virtual void UpsertOneTestLockRecord(std::shared_ptr<TestLockRecord> v) = 0;
        virtual std::shared_ptr<TestLockRecord> SelectOptionalTestLockRecord() = 0;
        virtual std::shared_ptr<TestLockRecord> LockOptionalTestLockRecord() = 0;
	};
}
