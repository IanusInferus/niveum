#pragma once

#include "IDataAccess.h"
#include "MySql/DataAccessBase.h"

#include <exception>
#include <stdexcept>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

#include <boost/thread.hpp>

namespace Database
{
	namespace _Impl
	{
		using namespace std;

		class DataAccessImplementation : public IDataAccess, private DataAccessBase
		{
		public:
		    DataAccessImplementation(wstring ConnectionString)
				: DataAccessBase(ConnectionString)
		    {
		    }

		    ~DataAccessImplementation()
		    {
		    }

			void Complete()
			{
				DataAccessBase::Complete();
			}

		public:
			void UpsertOneTestRecord(shared_ptr<TestRecord> v)
			{
				auto cmd = CreateTextCommand(L"INSERT INTO TestRecords (SessionIndex, Value) VALUES (@SessionIndex, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
				Add(L"SessionIndex", v->SessionIndex);
				Add(L"Value", v->Value);
				cmd->execute();
			}

			shared_ptr<TestRecord> SelectOptionalTestRecord(int SessionIndex)
			{
				auto cmd = CreateTextCommand(L"SELECT SessionIndex, Value FROM TestRecords WHERE SessionIndex = @SessionIndex");
				Add(L"SessionIndex", SessionIndex);
				shared_ptr<TestRecord> v = nullptr;
				auto rs = shared_ptr<ResultSet>(cmd->executeQuery());
				if (rs->next())
				{
					v = make_shared<TestRecord>();
					v->SessionIndex = GetInt(rs, L"SessionIndex");
					v->Value = GetInt(rs, L"Value");
				}
				if (rs->next())
				{
					throw logic_error("InvalidOperationException");
				}
				return v;
			}

			void UpsertOneTestLockRecord(shared_ptr<TestLockRecord> v)
			{
				auto cmd = CreateTextCommand(L"INSERT INTO TestLockRecords (Id, Value) VALUES (1, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
				Add(L"Value", v->Value);
				cmd->execute();
			}

			shared_ptr<TestLockRecord> SelectOptionalTestLockRecord()
			{
				auto cmd = CreateTextCommand(L"SELECT Id, Value FROM TestLockRecords");
				shared_ptr<TestLockRecord> v = nullptr;
				auto rs = shared_ptr<ResultSet>(cmd->executeQuery());
				if (rs->next())
				{
					v = make_shared<TestLockRecord>();
					v->Id = GetInt(rs, L"Id");
					v->Value = GetInt(rs, L"Value");
				}
				if (rs->next())
				{
					throw logic_error("InvalidOperationException");
				}
				return v;
			}

			shared_ptr<TestLockRecord> LockOptionalTestLockRecord()
			{
				auto cmd = CreateTextCommand(L"SELECT Id, Value FROM TestLockRecords FOR UPDATE");
				shared_ptr<TestLockRecord> v = nullptr;
				auto rs = shared_ptr<ResultSet>(cmd->executeQuery());
				if (rs->next())
				{
					v = make_shared<TestLockRecord>();
					v->Id = GetInt(rs, L"Id");
					v->Value = GetInt(rs, L"Value");
				}
				if (rs->next())
				{
					throw logic_error("InvalidOperationException");
				}
				return v;
			}
		};
	}
}

namespace Database
{
	typedef _Impl::DataAccessImplementation DataAccessImplementation;
}
