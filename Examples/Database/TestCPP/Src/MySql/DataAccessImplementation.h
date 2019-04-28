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
    namespace MySql
    {
        class DataAccessImplementation : public Database::IDataAccess, private DataAccessBase
        {
        public:
            DataAccessImplementation(std::wstring ConnectionString)
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
            void FromTestRecordUpsertOne(std::shared_ptr<Database::TestRecord> v)
            {
                auto cmd = CreateTextCommand(L"INSERT INTO TestRecords (SessionIndex, Value) VALUES (@SessionIndex, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
                Add(L"SessionIndex", v->SessionIndex);
                Add(L"Value", v->Value);
                cmd->execute();
            }

            virtual std::optional<std::shared_ptr<Database::TestRecord>> FromTestRecordSelectOptionalBySessionIndex(int SessionIndex)
            {
                auto cmd = CreateTextCommand(L"SELECT SessionIndex, Value FROM TestRecords WHERE SessionIndex = @SessionIndex");
                Add(L"SessionIndex", SessionIndex);
                auto ov = std::optional<std::shared_ptr<Database::TestRecord>>{};
                auto rs = std::shared_ptr<sql::ResultSet>(cmd->executeQuery());
                if (rs->next())
                {
                    auto v = std::make_shared<Database::TestRecord>();
                    v->SessionIndex = GetInt(rs, L"SessionIndex");
                    v->Value = GetInt(rs, L"Value");
                    ov = v;
                }
                if (rs->next())
                {
                    throw std::logic_error("InvalidOperationException");
                }
                return ov;
            }

            void FromTestLockRecordUpsertOne(std::shared_ptr<Database::TestLockRecord> v)
            {
                auto cmd = CreateTextCommand(L"INSERT INTO TestLockRecords (Id, Value) VALUES (1, @Value) ON DUPLICATE KEY UPDATE Value = @Value");
                Add(L"Value", v->Value);
                cmd->execute();
            }

            void FromTestLockRecordUpsertMany(std::shared_ptr<std::vector<std::shared_ptr<Database::TestLockRecord>>> l)
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

            std::optional<std::shared_ptr<Database::TestLockRecord>> FromTestLockRecordSelectOptionalById(int Id)
            {
                auto cmd = CreateTextCommand(L"SELECT Id, Value FROM TestLockRecords WHERE Id = @Id");
                Add(L"Id", Id);
                auto ov = std::optional<std::shared_ptr<Database::TestLockRecord>>{};
                auto rs = std::shared_ptr<sql::ResultSet>(cmd->executeQuery());
                if (rs->next())
                {
                    auto v = std::make_shared<Database::TestLockRecord>();
                    v->Id = GetInt(rs, L"Id");
                    v->Value = GetInt(rs, L"Value");
                    ov = v;
                }
                if (rs->next())
                {
                    throw std::logic_error("InvalidOperationException");
                }
                return ov;
            }

            std::optional<std::shared_ptr<Database::TestLockRecord>> FromTestLockRecordLockOptionalById(int Id)
            {
                auto cmd = CreateTextCommand(L"SELECT Id, Value FROM TestLockRecords WHERE Id = @Id FOR UPDATE");
                Add(L"Id", Id);
                auto ov = std::optional<std::shared_ptr<Database::TestLockRecord>>{};
                auto rs = std::shared_ptr<sql::ResultSet>(cmd->executeQuery());
                if (rs->next())
                {
                    auto v = std::make_shared<Database::TestLockRecord>();
                    v->Id = GetInt(rs, L"Id");
                    v->Value = GetInt(rs, L"Value");
                    ov = v;
                }
                if (rs->next())
                {
                    throw std::logic_error("InvalidOperationException");
                }
                return ov;
            }

            std::optional<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordSelectOptionalByA(String A) { throw std::logic_error("NotImplementedException"); }
            std::optional<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordSelectOptionalByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordSelectOneByA(String A) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordSelectOneByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectManyByA(String A) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectManyByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectAll() { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectRangeByAOrderByAAndBDesc(String A, Int _Skip_, Int _Take_) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordSelectRangeOrderByADescAndB(Int _Skip_, Int _Take_) { throw std::logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordSelectCount() { throw std::logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordSelectCountByA(String A) { throw std::logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordSelectCountByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::optional<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordLockOptionalByA(String A) { throw std::logic_error("NotImplementedException"); }
            std::optional<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>> FromTestDuplicatedKeyNameRecordLockOptionalByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordLockOneByA(String A) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> FromTestDuplicatedKeyNameRecordLockOneByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockManyByA(String A) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockManyByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockAll() { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockRangeByAOrderByAAndBDesc(String A, Int _Skip_, Int _Take_) { throw std::logic_error("NotImplementedException"); }
            std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> FromTestDuplicatedKeyNameRecordLockRangeOrderByADescAndB(Int _Skip_, Int _Take_) { throw std::logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordLockCountByA(String A) { throw std::logic_error("NotImplementedException"); }
            Int FromTestDuplicatedKeyNameRecordLockCountByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordInsertOne(std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> v) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordInsertMany(std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> l) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordUpdateOptional(std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> v) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordUpdateOne(std::shared_ptr<class Database::TestDuplicatedKeyNameRecord> v) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordUpdateMany(std::shared_ptr<std::vector<std::shared_ptr<class Database::TestDuplicatedKeyNameRecord>>> l) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOptionalByA(String A) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOptionalByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOneByA(String A) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteOneByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteManyByA(String A) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteManyByAAndB(String A, enum class Database::TestEnum B) { throw std::logic_error("NotImplementedException"); }
            void FromTestDuplicatedKeyNameRecordDeleteAll() { throw std::logic_error("NotImplementedException"); }
        };
    }
}
