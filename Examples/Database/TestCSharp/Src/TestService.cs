using System;
using System.Collections.Generic;
using System.Linq;
using DB = Database.Database;

namespace Database
{
    public class TestService : ITestService
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
                if (v.OnSome)
                {
                    return v.Some.Value;
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

        private static int ConcurrentWriteNotBlocked = 0;
        private static Object Lockee = new Object();
        public void AddLockData(int Value)
        {
            using (var da = dam.Create())
            {
                var ov = da.FromTestLockRecordLockOptionalById(1);
                lock (Lockee)
                {
                    if (ConcurrentWriteNotBlocked >= 1)
                    {
                        throw new InvalidOperationException("LockingFailed");
                    }
                    ConcurrentWriteNotBlocked += 1;
                }
                DB.TestLockRecord v;
                try
                {
                    if (ov.OnSome)
                    {
                        v = ov.Some;
                    }
                    else
                    {
                        v = new DB.TestLockRecord { Id = 1, Value = 0 };
                    }
                    v.Value += Value;
                    da.FromTestLockRecordUpsertOne(v);
                }
                finally
                {
                    lock (Lockee)
                    {
                        ConcurrentWriteNotBlocked -= 1;
                    }
                }
                da.Complete();
            }
        }

        public int DeleteLockData()
        {
            using (var da = dam.Create())
            {
                var ov = da.FromTestLockRecordLockOptionalById(1);
                lock (Lockee)
                {
                    if (ConcurrentWriteNotBlocked >= 1)
                    {
                        throw new InvalidOperationException("LockingFailed");
                    }
                    ConcurrentWriteNotBlocked += 1;
                }
                DB.TestLockRecord v;
                try
                {
                    if (ov.OnSome)
                    {
                        v = ov.Some;
                    }
                    else
                    {
                        v = new DB.TestLockRecord { Id = 1, Value = 0 };
                    }
                    da.FromTestLockRecordDeleteOptionalById(1);
                }
                finally
                {
                    lock (Lockee)
                    {
                        ConcurrentWriteNotBlocked -= 1;
                    }
                }
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
                if (ov.OnSome)
                {
                    v = ov.Some;
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
