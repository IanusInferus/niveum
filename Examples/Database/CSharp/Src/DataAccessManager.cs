using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace Database
{
    public enum DatabaseType
    {
        SqlServer,
        SqlServerCe,
        PostgreSQL,
        MySQL
    }

    /// <summary>线程安全。</summary>
    public class DataAccessManager
    {
        private Func<IDataAccess> ConnectionFactory;

        private static readonly String SqlServerType = "Database.SqlServer.SqlServerDataAccess";
        private static readonly String PostgreSqlType = "Database.PostgreSql.PostgreSqlDataAccess";
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Mail";
        private static readonly String PostgreSqlConnectionString = "Server=localhost;User ID=postgres;Password={Password};Database=mail;";

        public static String GetConnectionStringExample()
        {
            {
                var t = System.Type.GetType(SqlServerType);
                if (t != null)
                {
                    return SqlServerConnectionString;
                }
            }
            {
                var t = System.Type.GetType(PostgreSqlType);
                if (t != null)
                {
                    return PostgreSqlConnectionString;
                }
            }
            throw new InvalidOperationException();
        }

        public static String GetConnectionStringExample(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = System.Type.GetType(SqlServerType, true);
                return SqlServerConnectionString;
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = System.Type.GetType(PostgreSqlType, true);
                return PostgreSqlConnectionString;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public DataAccessManager(String ConnectionString)
        {
            {
                var t = System.Type.GetType(SqlServerType);
                if (t != null)
                {
                    ConnectionFactory = () => (IDataAccess)(Activator.CreateInstance(t, new Object[] { ConnectionString }));
                    return;
                }
            }
            {
                var t = System.Type.GetType(PostgreSqlType);
                if (t != null)
                {
                    ConnectionFactory = () => (IDataAccess)(Activator.CreateInstance(t, new Object[] { ConnectionString }));
                    return;
                }
            }
            throw new InvalidOperationException();
        }
        public DataAccessManager(DatabaseType Type, String ConnectionString)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = System.Type.GetType(SqlServerType, true);
                ConnectionFactory = () => (IDataAccess)(Activator.CreateInstance(t, new Object[] { ConnectionString }));
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = System.Type.GetType(PostgreSqlType, true);
                ConnectionFactory = () => (IDataAccess)(Activator.CreateInstance(t, new Object[] { ConnectionString }));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public IDataAccess Create()
        {
            return ConnectionFactory();
        }
    }
}
