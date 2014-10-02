using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BaseSystem
{
    public class LockedVariable<T>
    {
        private T Value;
        private ReaderWriterLockSlim Lockee = new ReaderWriterLockSlim();
        public LockedVariable(T Value)
        {
            this.Value = Value;
        }

        public S Check<S>(Func<T, S> Map)
        {
            Lockee.EnterReadLock();
            try
            {
                return Map(Value);
            }
            finally
            {
                Lockee.ExitReadLock();
            }
        }

        public void DoAction(Action<T> Action)
        {
            Lockee.EnterWriteLock();
            try
            {
                Action(Value);
            }
            finally
            {
                Lockee.ExitWriteLock();
            }
        }

        public void Update(Func<T, T> Map)
        {
            Lockee.EnterWriteLock();
            try
            {
                Value = Map(Value);
            }
            finally
            {
                Lockee.ExitWriteLock();
            }
        }
    }
}
