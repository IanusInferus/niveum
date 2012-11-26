using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.SqlServer
{
    public partial class SqlServerDataAccess : IDataAccess
    {
        public void UpsertOneTestRecord(TestRecord v)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"
UPDATE TestRecords SET Value = @Value WHERE SessionIndex = @SessionIndex
IF @@ROWCOUNT = 0 INSERT INTO TestRecords (SessionIndex, Value) VALUES (@SessionIndex, @Value)
";
            Add(cmd, "SessionIndex", v.SessionIndex);
            Add(cmd, "Value", v.Value);
            cmd.ExecuteNonQuery();
        }

        public Optional<TestRecord> SelectOptionalTestRecordBySessionIndex(Int32 SessionIndex)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT SessionIndex, Value FROM TestRecords WHERE SessionIndex = @SessionIndex";
            Add(cmd, "SessionIndex", SessionIndex);
            var v = Optional<TestRecord>.Empty;
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
UPDATE TestLockRecords SET Value = @Value WHERE Id = 1
IF @@ROWCOUNT = 0 INSERT INTO TestLockRecords (Id, Value) VALUES (1, @Value)
";
            Add(cmd, "Value", v.Value);
            cmd.ExecuteNonQuery();
        }

        public Optional<TestLockRecord> SelectOptionalTestLockRecordById(Int32 Id)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT Id, Value FROM TestLockRecords WHERE Id = @Id";
            Add(cmd, "Id", Id);
            var v = Optional<TestLockRecord>.Empty;
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

        public Optional<TestLockRecord> LockOptionalTestLockRecordById(Int32 Id)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT Id, Value FROM TestLockRecords WITH (UPDLOCK) WHERE Id = @Id";
            Add(cmd, "Id", Id);
            var v = Optional<TestLockRecord>.Empty;
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
