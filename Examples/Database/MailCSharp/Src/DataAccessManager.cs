using System;
using System.Collections.Generic;
using System.Linq;
using BaseSystem;
using Database.Database;

namespace Database
{
    public enum DatabaseType
    {
        Memory,
        SqlServer,
        SqlServerCe,
        PostgreSQL,
        MySQL,
        Krustallos
    }

    public interface IDataAccessProvider : IDisposable
    {
        /// <summary>返回值应线程安全。</summary>
        Func<String, ITransactionLock, IDataAccess> GetConnectionFactory();
        /// <summary>返回值应线程安全。</summary>
        Func<Exception, Boolean> GetIsRetryable();
    }

    /// <summary>线程安全。</summary>
    public class DataAccessManager : IDisposable
    {
        private Func<IDataAccess> ConnectionFactory;
        private Func<Exception, Boolean> IsRetryable;
        private Action Dispose;

        private static readonly String MemoryType = "Database.Memory.Provider";
        private static readonly String SqlServerType = "Database.SqlServer.Provider";
        private static readonly String PostgreSqlType = "Database.PostgreSql.Provider";
        private static readonly String MySqlType = "Database.MySql.Provider";
        private static readonly String KrustallosType = "Database.Krustallos.Provider";
        private static readonly String MemoryConnectionString = "Mail.kd";
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Mail";
        private static readonly String PostgreSqlConnectionString = "Server=localhost;User ID=postgres;Password={Password};Database=mail;";
        private static readonly String MySqlConnectionString = "server=localhost;uid=root;pwd={Password};database=Mail;";
        private static readonly String KrustallosConnectionString = "File=Mail.kd";

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

        public static String GetConnectionStringExample()
        {
            {
                var t = GetType(MemoryType);
                if (t != null)
                {
                    return MemoryConnectionString;
                }
            }
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
            {
                var t = GetType(KrustallosType);
                if (t != null)
                {
                    return KrustallosConnectionString;
                }
            }
            throw new InvalidOperationException();
        }

        public static String GetConnectionStringExample(DatabaseType Type)
        {
            if (Type == DatabaseType.Memory)
            {
                var t = GetType(MemoryType, true);
                return MemoryConnectionString;
            }
            else if (Type == DatabaseType.SqlServer)
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
            else if (Type == DatabaseType.Krustallos)
            {
                var t = GetType(KrustallosType, true);
                return KrustallosConnectionString;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public DataAccessManager(String ConnectionString, ICascadeLock CascadeLock)
        {
            Type t = null;
            if (t == null)
            {
                t = GetType(MemoryType);
            }
            if (t == null)
            {
                t = GetType(SqlServerType);
            }
            if (t == null)
            {
                t = GetType(PostgreSqlType);
            }
            if (t == null)
            {
                t = GetType(MySqlType);
            }
            if (t == null)
            {
                t = GetType(KrustallosType);
            }
            if (t != null)
            {
                var o = Activator.CreateInstance(t);
                var p = (IDataAccessProvider)(o);
                var c = p.GetConnectionFactory();
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
                IsRetryable = p.GetIsRetryable();
                Dispose = p.Dispose;
                return;
            }
            throw new InvalidOperationException();
        }
        public DataAccessManager(DatabaseType Type, String ConnectionString, ICascadeLock CascadeLock)
        {
            Type t = null;
            if (Type == DatabaseType.Memory)
            {
                t = GetType(MemoryType, true);
            }
            else if (Type == DatabaseType.SqlServer)
            {
                t = GetType(SqlServerType, true);
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                t = GetType(PostgreSqlType, true);
            }
            else if (Type == DatabaseType.MySQL)
            {
                t = GetType(MySqlType, true);
            }
            else if (Type == DatabaseType.Krustallos)
            {
                t = GetType(KrustallosType, true);
            }
            else
            {
                throw new InvalidOperationException();
            }
            var o = Activator.CreateInstance(t);
            var p = (IDataAccessProvider)(o);
            var c = p.GetConnectionFactory();
            ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
            IsRetryable = p.GetIsRetryable();
            Dispose = p.Dispose;
        }

        public IDataAccess Create()
        {
            return ConnectionFactory();
        }

        public Func<Exception, Boolean> GetIsRetryable()
        {
            return IsRetryable;
        }

        void IDisposable.Dispose()
        {
            if (Dispose != null)
            {
                Dispose();
                Dispose = null;
            }
        }
    }
}
