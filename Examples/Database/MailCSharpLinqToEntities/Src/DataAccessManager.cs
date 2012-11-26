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

        private static readonly String SqlServerType = "Database.Linq.LinqDataAccess";
        private static readonly String MySqlType = "Database.Linq.LinqDataAccess";
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Mail";
        private static readonly String MySqlConnectionString = "server=localhost;uid=root;pwd={Password};database=Mail;";

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
        private static Func<DatabaseType, String, IDataAccess> GetConstructor(DatabaseType Type, Type t)
        {
            var c = t.GetMethod("Create", (new Type[] { typeof(DatabaseType), typeof(String) }));
            var d = Delegate.CreateDelegate(typeof(Func<DatabaseType, String, IDataAccess>), c);
            return (Func<DatabaseType, String, IDataAccess>)(d);
        }

        public static String GetConnectionStringExample(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                return SqlServerConnectionString;
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

        public DataAccessManager(DatabaseType Type, String ConnectionString)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                var c = GetConstructor(Type, t);
                ConnectionFactory = () => c(Type, ConnectionString);
            }
            else if (Type == DatabaseType.MySQL)
            {
                var t = GetType(MySqlType, true);
                var c = GetConstructor(Type, t);
                ConnectionFactory = () => c(Type, ConnectionString);
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
