#pragma once

#include "IDataAccess.h"
#include "DataAccessManager.h"

#include <memory>
#include <string>
#include <stdexcept>

namespace Database
{
	class TestService
	{
	private:
		DataAccessManager &dam;
	public:
		TestService(DataAccessManager &dam)
			: dam(dam)
		{
		}

		void SaveData(int SessionIndex, int Value)
		{
			auto da = dam.Create();
			auto tr = std::make_shared<TestRecord>();
			tr->SessionIndex = SessionIndex;
			tr->Value = Value;
			da->UpsertOneTestRecord(tr);
			da->Complete();
		}

		int LoadData(int SessionIndex)
		{
			auto da = dam.Create();
			auto v = da->SelectOptionalTestRecordBySessionIndex(SessionIndex);
            if (v.OnHasValue())
            {
                return v.HasValue->Value;
            }
            else
            {
                throw std::logic_error("InvalidOperationException");
            }
		}
		
		void SaveLockData(int Value)
		{
			auto da = dam.Create();
			auto tlr = std::make_shared<TestLockRecord>();
			tlr->Id = 1;
			tlr->Value = Value;
			da->UpsertOneTestLockRecord(tlr);
			da->Complete();
		}
		
		void AddLockData(int Value)
		{
			auto da = dam.Create();
			auto ov = da->LockOptionalTestLockRecordById(1);
            std::shared_ptr<TestLockRecord> v;
            if (ov.OnHasValue())
            {
                v = ov.HasValue;
            }
            else
            {
                v = std::make_shared<TestLockRecord>();
                v->Id = 1;
                v->Value = 0;
            }
			v->Value += Value;
			da->UpsertOneTestLockRecord(v);
			da->Complete();
		}
		
		int LoadLockData()
		{
			auto da = dam.Create();
			auto ov = da->SelectOptionalTestLockRecordById(1);
            std::shared_ptr<TestLockRecord> v;
            if (ov.OnHasValue())
            {
                v = ov.HasValue;
			    return v->Value;
            }
            else
            {
                throw std::logic_error("InvalidOperationException");
            }
		}
	};
}