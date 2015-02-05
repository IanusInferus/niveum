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
        MySQL,
        FoundationDBSQL,
        Krustallos,
        KrustallosMySQL
    }

    /// <summary>线程安全。</summary>
    public class DataAccessManager
    {
        private Func<IDataAccess> ConnectionFactory;

        private static readonly String SqlServerType = "Database.SqlServer.SqlServerDataAccessPool";
        private static readonly String PostgreSqlType = "Database.PostgreSql.PostgreSqlDataAccessPool";
        private static readonly String MySqlType = "Database.MySql.MySqlDataAccessPool";
        private static readonly String FoundationDbSqlType = "Database.FoundationDbSql.FoundationDbSqlDataAccessPool";
        private static readonly String KrustallosType = "Database.Krustallos.KrustallosDataAccessPool";
        private static readonly String KrustallosMySqlType = "Database.KrustallosMySql.KrustallosMySqlDataAccessPool";
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Test";
        private static readonly String PostgreSqlConnectionString = "Server=localhost;User ID=postgres;Password={Password};Database=test;";
        private static readonly String MySqlConnectionString = "server=localhost;uid=root;pwd={Password};database=Test;";
        private static readonly String FoundationDbSqlConnectionString = "server=localhost;Port=15432;uid=root;pwd={Password};database=test;";
        private static readonly String KrustallosConnectionString = "";
        private static readonly String KrustallosMySqlConnectionString = "server=localhost;uid=root;pwd={Password};database=Test;";

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
                var t = GetType(KrustallosMySqlType);
                if (t != null)
                {
                    return KrustallosMySqlConnectionString;
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
                var t = GetType(FoundationDbSqlType);
                if (t != null)
                {
                    return FoundationDbSqlConnectionString;
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
            else if (Type == DatabaseType.FoundationDBSQL)
            {
                var t = GetType(FoundationDbSqlType, true);
                return FoundationDbSqlConnectionString;
            }
            else if (Type == DatabaseType.Krustallos)
            {
                var t = GetType(KrustallosType, true);
                return KrustallosConnectionString;
            }
            else if (Type == DatabaseType.KrustallosMySQL)
            {
                var t = GetType(KrustallosMySqlType, true);
                return KrustallosMySqlConnectionString;
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
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                    ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
                    return;
                }
            }
            {
                var t = GetType(PostgreSqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                    ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
                    return;
                }
            }
            {
                var t = GetType(KrustallosMySqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                    ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
                    return;
                }
            }
            {
                var t = GetType(MySqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                    ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
                    return;
                }
            }
            {
                var t = GetType(FoundationDbSqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                    ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
                    return;
                }
            }
            {
                var t = GetType(KrustallosType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                    ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
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
                var o = Activator.CreateInstance(t);
                var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = GetType(PostgreSqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
            }
            else if (Type == DatabaseType.MySQL)
            {
                var t = GetType(MySqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
            }
            else if (Type == DatabaseType.FoundationDBSQL)
            {
                var t = GetType(FoundationDbSqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
            }
            else if (Type == DatabaseType.Krustallos)
            {
                var t = GetType(KrustallosType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
            }
            else if (Type == DatabaseType.KrustallosMySQL)
            {
                var t = GetType(KrustallosMySqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, ITransactionLock, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, ITransactionLock, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String), typeof(ITransactionLock) })));
                ConnectionFactory = () => c(ConnectionString, CascadeLock != null ? new TransactionLock(new BaseSystem.TransactionLock(CascadeLock)) : null);
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
