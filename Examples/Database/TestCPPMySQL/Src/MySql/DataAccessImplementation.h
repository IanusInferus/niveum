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
            void FromTestRecordUpsertOne(shared_ptr<TestRecord> v)
            {
                auto cmd = CreateTextCommand(L"INSERT INTO TestRecords (SessionIndex, Value) VALUES (@SessionIndex, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
                Add(L"SessionIndex", v->SessionIndex);
                Add(L"Value", v->Value);
                cmd->execute();
            }

            virtual Optional<std::shared_ptr<TestRecord>> FromTestRecordSelectOptionalBySessionIndex(int SessionIndex)
            {
                auto cmd = CreateTextCommand(L"SELECT SessionIndex, Value FROM TestRecords WHERE SessionIndex = @SessionIndex");
                Add(L"SessionIndex", SessionIndex);
                auto ov = Optional<std::shared_ptr<TestRecord>>::Empty();
                auto rs = shared_ptr<ResultSet>(cmd->executeQuery());
                if (rs->next())
                {
                    auto v = make_shared<TestRecord>();
                    v->SessionIndex = GetInt(rs, L"SessionIndex");
                    v->Value = GetInt(rs, L"Value");
                    ov = Optional<std::shared_ptr<TestRecord>>::CreateHasValue(v);
                }
                if (rs->next())
                {
                    throw logic_error("InvalidOperationException");
                }
                return ov;
            }

            void FromTestLockRecordUpsertOne(shared_ptr<TestLockRecord> v)
            {
                auto cmd = CreateTextCommand(L"INSERT INTO TestLockRecords (Id, Value) VALUES (1, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
                Add(L"Value", v->Value);
                cmd->execute();
            }

            Optional<std::shared_ptr<TestLockRecord>> FromTestLockRecordSelectOptionalById(int Id)
            {
                auto cmd = CreateTextCommand(L"SELECT Id, Value FROM TestLockRecords WHERE Id = @Id");
                Add(L"Id", Id);
                auto ov = Optional<std::shared_ptr<TestLockRecord>>::Empty();
                auto rs = shared_ptr<ResultSet>(cmd->executeQuery());
                if (rs->next())
                {
                    auto v = make_shared<TestLockRecord>();
                    v->Id = GetInt(rs, L"Id");
                    v->Value = GetInt(rs, L"Value");
                    ov = Optional<std::shared_ptr<TestLockRecord>>::CreateHasValue(v);
                }
                if (rs->next())
                {
                    throw logic_error("InvalidOperationException");
                }
                return ov;
            }

            Optional<std::shared_ptr<TestLockRecord>> FromTestLockRecordLockOptionalById(int Id)
            {
                auto cmd = CreateTextCommand(L"SELECT Id, Value FROM TestLockRecords WHERE Id = @Id FOR UPDATE");
                Add(L"Id", Id);
                auto ov = Optional<std::shared_ptr<TestLockRecord>>::Empty();
                auto rs = shared_ptr<ResultSet>(cmd->executeQuery());
                if (rs->next())
                {
                    auto v = make_shared<TestLockRecord>();
                    v->Id = GetInt(rs, L"Id");
                    v->Value = GetInt(rs, L"Value");
                    ov = Optional<std::shared_ptr<TestLockRecord>>::CreateHasValue(v);
                }
                if (rs->next())
                {
                    throw logic_error("InvalidOperationException");
                }
                return ov;
            }
        };
    }
}

namespace Database
{
    typedef _Impl::DataAccessImplementation DataAccessImplementation;
}
