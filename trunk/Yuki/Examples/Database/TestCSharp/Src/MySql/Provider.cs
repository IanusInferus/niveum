using System;
using Database.Database;

namespace Database.MySql
{
    public class Provider : IDataAccessProvider
    {
        private MySqlDataAccessPool Inner = new MySqlDataAccessPool();

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
