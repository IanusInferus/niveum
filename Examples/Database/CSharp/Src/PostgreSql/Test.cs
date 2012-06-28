using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.PostgreSql
{
    public partial class PostgreSqlDataAccess : IDataAccess
    {
        public void UpsertOneTestRecord(TestRecord v)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"
UPDATE TestRecords SET Value = @Value WHERE SessionIndex = @SessionIndex;
INSERT INTO TestRecords (SessionIndex, Value) SELECT @SessionIndex, @Value WHERE NOT EXISTS (SELECT 1 FROM TestRecords WHERE SessionIndex = @SessionIndex)
";
            Add(cmd, "SessionIndex", v.SessionIndex);
            Add(cmd, "Value", v.Value);
            cmd.ExecuteNonQuery();
        }

        public TestRecord SelectOptionalTestRecord(int SessionIndex)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT SessionIndex, Value FROM TestRecords WHERE SessionIndex = @SessionIndex";
            Add(cmd, "SessionIndex", SessionIndex);
            TestRecord v = null;
            using (var dr = cmd.ExecuteReader())
            {
                if (dr.Read())
                {
                    v = new TestRecord
                    {
                        SessionIndex = GetInt(dr, "SessionIndex"),
                        Value = GetInt(dr, "Value")
                    };
                }
                if (dr.Read())
                {
                    throw new InvalidOperationException();
                }
            }
            return v;
        }

        public void UpsertOneTestLockRecord(TestLockRecord v)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"
UPDATE TestLockRecords SET Value = @Value WHERE Id = 1;
INSERT INTO TestLockRecords (Id, Value) SELECT 1, @Value WHERE NOT EXISTS (SELECT 1 FROM TestLockRecords WHERE Id = 1)
";
            Add(cmd, "Value", v.Value);
            cmd.ExecuteNonQuery();
        }

        public TestLockRecord SelectOptionalTestLockRecord()
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT Id, Value FROM TestLockRecords";
            TestLockRecord v = null;
            using (var dr = cmd.ExecuteReader())
            {
                if (dr.Read())
                {
                    v = new TestLockRecord
                    {
                        Id = GetInt(dr, "Id"),
                        Value = GetInt(dr, "Value")
                    };
                }
                if (dr.Read())
                {
                    throw new InvalidOperationException();
                }
            }
            return v;
        }

        public TestLockRecord LockOptionalTestLockRecord()
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT Id, Value FROM TestLockRecords FOR UPDATE";
            TestLockRecord v = null;
            using (var dr = cmd.ExecuteReader())
            {
                if (dr.Read())
                {
                    v = new TestLockRecord
                    {
                        Id = GetInt(dr, "Id"),
                        Value = GetInt(dr, "Value")
                    };
                }
                if (dr.Read())
                {
                    throw new InvalidOperationException();
                }
            }
            return v;
        }
    }
}
