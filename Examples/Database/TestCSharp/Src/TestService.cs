using System;
using System.Collections.Generic;
using System.Linq;
using DB = Database.Database;

namespace Database
{
    public class TestService
    {
        private DataAccessManager dam;
        public TestService(DataAccessManager dam)
        {
            this.dam = dam;
        }

        public void SaveData(int SessionIndex, int Value)
        {
            using (var da = dam.Create())
            {
                da.FromTestRecordUpsertOne(new DB.TestRecord { SessionIndex = SessionIndex, Value = Value });
                da.Complete();
            }
        }

        public int LoadData(int SessionIndex)
        {
            using (var da = dam.Create())
            {
                var v = da.FromTestRecordSelectOptionalBySessionIndex(SessionIndex);
                if (v.OnHasValue)
                {
                    return v.HasValue.Value;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public void SaveLockData(int Value)
        {
            using (var da = dam.Create())
            {
                da.FromTestLockRecordUpsertOne(new DB.TestLockRecord { Id = 1, Value = Value });
                da.Complete();
            }
        }

        public void AddLockData(int Value)
        {
            using (var da = dam.Create())
            {
                var ov = da.FromTestLockRecordLockOptionalById(1);
                DB.TestLockRecord v;
                if (ov.OnHasValue)
                {
                    v = ov.HasValue;
                }
                else
                {
                    v = new DB.TestLockRecord { Id = 1, Value = 0 };
                }
                v.Value += Value;
                da.FromTestLockRecordUpsertOne(v);
                da.Complete();
            }
        }

        public int DeleteLockData()
        {
            using (var da = dam.Create())
            {
                var ov = da.FromTestLockRecordLockOptionalById(1);
                DB.TestLockRecord v;
                if (ov.OnHasValue)
                {
                    v = ov.HasValue;
                }
                else
                {
                    v = new DB.TestLockRecord { Id = 1, Value = 0 };
                }
                da.FromTestLockRecordDeleteOptionalById(1);
                da.Complete();

                return v.Value;
            }
        }

        public int LoadLockData()
        {
            using (var da = dam.Create())
            {
                var ov = da.FromTestLockRecordSelectOptionalById(1);
                DB.TestLockRecord v;
                if (ov.OnHasValue)
                {
                    v = ov.HasValue;
                    return v.Value;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}
