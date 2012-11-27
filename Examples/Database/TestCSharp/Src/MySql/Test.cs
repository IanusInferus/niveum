using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.MySql
{
    public partial class MySqlDataAccess : IDataAccess
    {
        public void UpsertOneTestRecord(TestRecord v)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"INSERT INTO TestRecords (SessionIndex, Value) VALUES (@SessionIndex, @Value) ON DUPLICATE KEY UPDATE Value = @Value";
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
            cmd.CommandText = @"INSERT INTO TestLockRecords (Id, Value) VALUES (@Id, @Value) ON DUPLICATE KEY UPDATE Value = @Value";
            Add(cmd, "Id", v.Id);
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
            cmd.CommandText = @"SELECT Id, Value FROM TestLockRecords WHERE Id = @Id FOR UPDATE";
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
