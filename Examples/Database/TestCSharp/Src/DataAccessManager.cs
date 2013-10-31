using System;
using System.Collections.Generic;
using System.Linq;
using BaseSystem;
using Database.Database;

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

        private static readonly String SqlServerType = "Database.SqlServer.SqlServerDataAccessPool";
        private static readonly String PostgreSqlType = "Database.PostgreSql.PostgreSqlDataAccessPool";
        private static readonly String MySqlType = "Database.MySql.MySqlDataAccessPool";
        private static readonly String SqlServerConnectionString = "Data Source=(LocalDB)\v11.0;Integrated Security=True;Database=Test";
        private static readonly String PostgreSqlConnectionString = "Server=localhost;User ID=postgres;Password={Password};Database=test;";
        private static readonly String MySqlConnectionString = "server=localhost;uid=root;pwd={Password};database=Test;";

        private static Type GetType(String FullName, Boolean ThrowOnError = false)
        {
            var Types = System.Reflection.Assembly.GetEntryAssembly().GetTypes().Where(t => t.FullName.Equals(FullName, StringComparison.Ordinal)).ToArray();
            if (Types.Length == 0)
            {
                if (ThrowOnError)
                {
                    throw new TypeLoadException(FullName);
                }
                else
                {
                    return null;
                }
            }
            return Types.Single();
        }

        private class TransactionLock : Database.ITransactionLock
        {
            private BaseSystem.TransactionLock InnerLock;
            public TransactionLock(BaseSystem.TransactionLock InnerLock)
            {
                this.InnerLock = InnerLock;
            }
            public void Enter(IEnumerable<object> LockPath)
            {
                InnerLock.Enter(LockPath);
            }
            public void Exit(IEnumerable<object> LockPath)
            {
                InnerLock.Exit(LockPath);
            }
            public void ExitAll()
            {
                InnerLock.ExitAll();
            }
            public void Dispose()
            {
                InnerLock.Dispose();
            }
        }
        private static Func<String, ICascadeLock, IDataAccess> GetConstructor(Type t)
        {
            var c = (IDataAccessPool)(Activator.CreateInstance(t));
            return (ConnectionString, CascadeLock) => c.Create(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
        }

        public static String GetConnectionStringExample()
        {
            {
                var t = GetType(SqlServerType);
                if (t != null)
                {
                    return SqlServerConnectionString;
                }
            }
            {
                var t = GetType(PostgreSqlType);
                if (t != null)
                {
                    return PostgreSqlConnectionString;
                }
            }
            {
                var t = GetType(MySqlType);
                if (t != null)
                {
                    return MySqlConnectionString;
                }
            }
            throw new InvalidOperationException();
        }

        public static String GetConnectionStringExample(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                return SqlServerConnectionString;
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = GetType(PostgreSqlType, true);
                return PostgreSqlConnectionString;
            }
            else if (Type == DatabaseType.MySQL)
            {
                var t = GetType(MySqlType, true);
                return MySqlConnectionString;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public DataAccessManager(String ConnectionString, ICascadeLock CascadeLock)
        {
            {
                var t = GetType(SqlServerType);
                if (t != null)
                {
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString, CascadeLock);
                    return;
                }
            }
            {
                var t = GetType(PostgreSqlType);
                if (t != null)
                {
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString, CascadeLock);
                    return;
                }
            }
            {
                var t = GetType(MySqlType);
                if (t != null)
                {
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString, CascadeLock);
                    return;
                }
            }
            throw new InvalidOperationException();
        }
        public DataAccessManager(DatabaseType Type, String ConnectionString, ICascadeLock CascadeLock)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                var c = GetConstructor(t);
                ConnectionFactory = () => c(ConnectionString, CascadeLock);
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = GetType(PostgreSqlType, true);
                var c = GetConstructor(t);
                ConnectionFactory = () => c(ConnectionString, CascadeLock);
            }
            else if (Type == DatabaseType.MySQL)
            {
                var t = GetType(MySqlType, true);
                var c = GetConstructor(t);
                ConnectionFactory = () => c(ConnectionString, CascadeLock);
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
