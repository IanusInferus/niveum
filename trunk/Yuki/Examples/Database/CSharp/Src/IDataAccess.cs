using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace Database
{
    public interface IDataAccess : IDisposable
    {
        void Complete();

        //{Select, Lock} x {Optional, One, Many, Range, All, Count} x {RecordName} (x {By} x {IndexName})? (x {OrderBy} x {IndexName})?
        //{Insert, Update, Upsert} x {One, Many} x {RecordName}
        //{Delete} x {One, Many, All} x {RecordName} x {By} x {IndexName}

        void UpsertOneTestRecord(TestRecord v);
        TestRecord SelectOptionalTestRecord(int SessionIndex);
        void UpsertOneTestLockRecord(TestLockRecord v);
        TestLockRecord SelectOptionalTestLockRecord();
        TestLockRecord LockOptionalTestLockRecord();
    }
}
