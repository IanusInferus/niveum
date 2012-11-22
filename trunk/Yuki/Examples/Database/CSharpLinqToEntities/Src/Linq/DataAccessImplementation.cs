using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Data;
using System.Data.Common;
using Firefly;

namespace Database.Linq
{
    public partial class LinqDataAccess : IDataAccess
    {
        private DbConnection Connection;
        private DbRoot dbr;
        public LinqDataAccess(DatabaseType dt, String ConnectionString)
        {
            var f = GetConnectionFactory(dt);
            Connection = f(ConnectionString);
            dbr = new DbRoot(Connection);
        }

        public static IDataAccess Create(String ConnectionString)
        {
            return new LinqDataAccess(DatabaseType.SqlServer, ConnectionString);
        }

        public static IDataAccess Create(DatabaseType dt, String ConnectionString)
        {
            return new LinqDataAccess(dt, ConnectionString);
        }

        public void Dispose()
        {
            if (dbr != null)
            {
                dbr.Dispose();
                dbr = null;
            }
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }
        }

        public void Complete()
        {
            if (dbr != null)
            {
                dbr.Dispose();
                dbr = null;
            }
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static Func<String, DbConnection> GetConnectionFactory(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                return GetConnectionFactorySqlServer();
            }
            else if (Type == DatabaseType.MySQL)
            {
                return GetConnectionFactoryMySQL();
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private static Func<String, DbConnection> GetConnectionFactorySqlServer()
        {
            return ConnectionString => new System.Data.SqlClient.SqlConnection(ConnectionString);
        }
        private static Func<String, DbConnection> GetConnectionFactoryMySQL()
        {
            return ConnectionString => new MySql.Data.MySqlClient.MySqlConnection(ConnectionString);
        }
    }
}
