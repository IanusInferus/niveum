#pragma once

#include "Database.h"
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
            auto tr = std::make_shared<Database::TestRecord>();
            tr->SessionIndex = SessionIndex;
            tr->Value = Value;
            da->FromTestRecordUpsertOne(tr);
            da->Complete();
        }

        int LoadData(int SessionIndex)
        {
            auto da = dam.Create();
            auto v = da->FromTestRecordSelectOptionalBySessionIndex(SessionIndex);
            if (v.has_value())
            {
                return v.value()->Value;
            }
            else
            {
                throw std::logic_error("InvalidOperationException");
            }
        }

        void SaveLockData(int Value)
        {
            auto da = dam.Create();
            auto tlr = std::make_shared<Database::TestLockRecord>();
            tlr->Id = 1;
            tlr->Value = Value;
            da->FromTestLockRecordUpsertOne(tlr);
            da->Complete();
        }

        void AddLockData(int Value)
        {
            auto da = dam.Create();
            auto ov = da->FromTestLockRecordLockOptionalById(1);
            std::shared_ptr<Database::TestLockRecord> v;
            if (ov.has_value())
            {
                v = ov.value();
            }
            else
            {
                v = std::make_shared<Database::TestLockRecord>();
                v->Id = 1;
                v->Value = 0;
            }
            v->Value += Value;
            da->FromTestLockRecordUpsertOne(v);
            da->Complete();
        }

        int LoadLockData()
        {
            auto da = dam.Create();
            auto ov = da->FromTestLockRecordSelectOptionalById(1);
            if (ov.has_value())
            {
                return ov.value()->Value;
            }
            else
            {
                throw std::logic_error("InvalidOperationException");
            }
        }
    };
}