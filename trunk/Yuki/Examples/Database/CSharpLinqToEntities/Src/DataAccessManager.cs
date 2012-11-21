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
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Mail";

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
        private static Func<String, IDataAccess> GetConstructor(Type t)
        {
            var c = t.GetMethod("Create", (new Type[] { typeof(String) }));
            var d = Delegate.CreateDelegate(typeof(Func<String, IDataAccess>), c);
            return (Func<String, IDataAccess>)(d);
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
            throw new InvalidOperationException();
        }

        public static String GetConnectionStringExample(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                return SqlServerConnectionString;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public DataAccessManager(String ConnectionString)
        {
            {
                var t = GetType(SqlServerType);
                if (t != null)
                {
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            throw new InvalidOperationException();
        }
        public DataAccessManager(DatabaseType Type, String ConnectionString)
        {
            if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                var c = GetConstructor(t);
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
