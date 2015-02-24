using System;
using MySql.Data.MySqlClient;
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
            return ex =>
            {
                if (ex is MySqlException)
                {
                    var x = (MySqlException)(ex);
                    var ErrorCode = (MySqlErrorCode)(x.ErrorCode);
                    if (ErrorCode == MySqlErrorCode.LockWaitTimeout) { return true; }
                    if (ErrorCode == MySqlErrorCode.LockDeadlock) { return true; }
                }
                return false;
            };
        }
    }
}
