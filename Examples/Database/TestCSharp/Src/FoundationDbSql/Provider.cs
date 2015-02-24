using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Firefly;
using BaseSystem;
using Database.Database;

namespace Database.FoundationDbSql
{
    public class Provider : IDataAccessProvider
    {
        private FoundationDbSqlDataAccessPool Inner = new FoundationDbSqlDataAccessPool();

        public Func<String, ITransactionLock, IDataAccess> GetConnectionFactory()
        {
            return Inner.Create;
        }

        public Func<Exception, Boolean> GetIsRetryable()
        {
            return ex =>
            {
                if (ex is Npgsql.NpgsqlException)
                {
                    var x = (Npgsql.NpgsqlException)(ex);
                    if (x.Code == "40002") { return true; }
                    if (x.Code == "40004") { return true; }
                }
                return false;
            };
        }
    }
}
