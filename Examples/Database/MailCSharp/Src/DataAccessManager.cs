using System;
using System.Collections.Generic;
using System.Linq;
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
        FoundationDBSQL
    }

    /// <summary>线程安全。</summary>
    public class DataAccessManager
    {
        private Func<IDataAccess> ConnectionFactory;

        private static readonly String MemoryType = "Database.Memory.MemoryDataAccessPool";
        private static readonly String SqlServerType = "Database.SqlServer.SqlServerDataAccessPool";
        private static readonly String PostgreSqlType = "Database.PostgreSql.PostgreSqlDataAccessPool";
        private static readonly String MySqlType = "Database.MySql.MySqlDataAccessPool";
        private static readonly String FoundationDbSqlType = "Database.FoundationDbSql.FoundationDbSqlDataAccessPool";
        private static readonly String MemoryConnectionString = "Mail.md";
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Mail";
        private static readonly String PostgreSqlConnectionString = "Server=localhost;User ID=postgres;Password={Password};Database=mail;";
        private static readonly String MySqlConnectionString = "server=localhost;uid=root;pwd={Password};database=Mail;";
        private static readonly String FoundationDbSqlConnectionString = "server=localhost;Port=15432;uid=root;pwd={Password};database=mail;";

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
                var t = GetType(FoundationDbSqlType);
                if (t != null)
                {
                    return FoundationDbSqlConnectionString;
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
            else if (Type == DatabaseType.FoundationDBSQL)
            {
                var t = GetType(FoundationDbSqlType, true);
                return FoundationDbSqlConnectionString;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public DataAccessManager(String ConnectionString)
        {
            {
                var t = GetType(MemoryType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(SqlServerType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(PostgreSqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(MySqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(FoundationDbSqlType);
                if (t != null)
                {
                    var o = Activator.CreateInstance(t);
                    var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            throw new InvalidOperationException();
        }
        public DataAccessManager(DatabaseType Type, String ConnectionString)
        {
            if (Type == DatabaseType.Memory)
            {
                var t = GetType(MemoryType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = GetType(PostgreSqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.MySQL)
            {
                var t = GetType(MySqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.FoundationDBSQL)
            {
                var t = GetType(FoundationDbSqlType, true);
                var o = Activator.CreateInstance(t);
                var c = (Func<String, IDataAccess>)(Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), o, t.GetMethod("Create", new Type[] { typeof(String) })));
                ConnectionFactory = () => c(ConnectionString);
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
