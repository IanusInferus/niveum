using System;
using Database.Database;

namespace Database.Memory
{
    public class Provider : IDataAccessProvider
    {
        private MemoryDataAccessPool Inner = new MemoryDataAccessPool();

        public Func<String, ITransactionLock, IDataAccess> GetConnectionFactory()
        {
            return (ConnectionString, TransactionLock) => Inner.Create(ConnectionString);
        }

        public Func<Exception, Boolean> GetIsRetryable()
        {
            return ex => false;
        }

        public void Dispose()
        {
        }
    }
}
