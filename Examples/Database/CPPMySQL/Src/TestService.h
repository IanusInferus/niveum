#pragma once

#include "IDataAccess.h"
#include "DataAccessManager.h"

#include <memory>
#include <string>

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
			auto v = da->SelectOptionalTestRecord(SessionIndex);
			return v->Value;
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
			auto v = da->LockOptionalTestLockRecord();
			v->Value += Value;
			da->UpsertOneTestLockRecord(v);
			da->Complete();
		}
		
		int LoadLockData()
		{
			auto da = dam.Create();
			auto v = da->SelectOptionalTestLockRecord();
			return v->Value;
		}
	};
}