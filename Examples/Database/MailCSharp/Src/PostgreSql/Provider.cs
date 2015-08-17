using System;
using Database.Database;

namespace Database.PostgreSql
{
    public class Provider : IDataAccessProvider
    {
        private PostgreSqlDataAccessPool Inner = new PostgreSqlDataAccessPool();

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
