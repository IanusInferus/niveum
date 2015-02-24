using System;
using Database.Database;

namespace Database.KrustallosMySql
{
    public class Provider : IDataAccessProvider
    {
        private KrustallosMySqlDataAccessPool Inner = new KrustallosMySqlDataAccessPool();

        public Func<String, ITransactionLock, IDataAccess> GetConnectionFactory()
        {
            return Inner.Create;
        }

        public Func<Exception, Boolean> GetIsRetryable()
        {
            return ex => false;
        }
    }
}
