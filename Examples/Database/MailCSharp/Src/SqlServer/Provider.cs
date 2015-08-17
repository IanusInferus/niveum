using System;
using Database.Database;

namespace Database.SqlServer
{
    public class Provider : IDataAccessProvider
    {
        private SqlServerDataAccessPool Inner = new SqlServerDataAccessPool();

        public Func<String, ITransactionLock, IDataAccess> GetConnectionFactory()
        {
            return Inner.Create;
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
