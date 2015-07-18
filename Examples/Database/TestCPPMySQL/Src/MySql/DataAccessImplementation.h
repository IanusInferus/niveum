#pragma once

#include "Database.h"
#include "MySql/DataAccessBase.h"

#include <exception>
#include <stdexcept>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

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

            void FromTestLockRecordUpsertMany(std::shared_ptr<std::vector<std::shared_ptr<class TestLockRecord>>> l)
            {
                for(auto v : *l)
                {
                    auto cmd = CreateTextCommand(L"INSERT INTO TestLockRecords (Id, Value) VALUES (1, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
                    Add(L"Value", v->Value);
                    cmd->execute();
                }
            }

            void FromTestLockRecordDeleteOptionalById(Int Id)
            {
                auto cmd = CreateTextCommand(L"DELETE FROM TestLockRecords WHERE Id = @Id");
                Add(L"Id", Id);
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

            Optional<std::shared_ptr<class TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordSelectOptionalByA(String A) { throw logic_error("NotImplementedException"); }
            Optional<std::shared_ptr<class TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordSelectOptionalByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<class TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordSelectOneByA(String A) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<class TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordSelectOneByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectManyByA(String A) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectManyByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectAll() { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectRangeByAOrderByAAndBDesc(String A, Int _Skip_, Int _Take_) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectRangeOrderByADescAndB(Int _Skip_, Int _Take_) { throw logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordSelectCount() { throw logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordSelectCountByA(String A) { throw logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordSelectCountByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            Optional<std::shared_ptr<class TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordLockOptionalByA(String A) { throw logic_error("NotImplementedException"); }
            Optional<std::shared_ptr<class TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordLockOptionalByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<class TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordLockOneByA(String A) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<class TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordLockOneByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockManyByA(String A) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockManyByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockAll() { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockRangeByAOrderByAAndBDesc(String A, Int _Skip_, Int _Take_) { throw logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockRangeOrderByADescAndB(Int _Skip_, Int _Take_) { throw logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordLockCountByA(String A) { throw logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordLockCountByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordInsertOne(std::shared_ptr<class TestDuplicatedKeyNameRecord> v) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordInsertMany(std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> l) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordUpdateOptional(std::shared_ptr<class TestDuplicatedKeyNameRecord> v) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordUpdateOne(std::shared_ptr<class TestDuplicatedKeyNameRecord> v) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordUpdateMany(std::shared_ptr<std::vector<std::shared_ptr<class TestDuplicatedKeyNameRecord>>> l) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOptionalByA(String A) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOptionalByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOneByA(String A) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOneByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteManyByA(String A) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteManyByAAndB(String A, enum TestEnum B) { throw logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteAll() { throw logic_error("NotImplementedException"); }
        };
    }
}

namespace Database
{
    typedef _Impl::DataAccessImplementation DataAccessImplementation;
}
