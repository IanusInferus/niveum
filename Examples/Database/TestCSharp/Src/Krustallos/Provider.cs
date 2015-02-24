using System;
using Database.Database;

namespace Database.Krustallos
{
    public class Provider : IDataAccessProvider
    {
        private KrustallosDataAccessPool Inner = new KrustallosDataAccessPool();

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
