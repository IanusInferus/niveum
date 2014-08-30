﻿using System;
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
        MySQL
    }

    /// <summary>线程安全。</summary>
    public class DataAccessManager
    {
        private Func<IDataAccess> ConnectionFactory;

        private static readonly String MemoryType = "Database.Memory.MemoryDataAccessPool";
        private static readonly String SqlServerType = "Database.SqlServer.SqlServerDataAccessPool";
        private static readonly String PostgreSqlType = "Database.PostgreSql.PostgreSqlDataAccessPool";
        private static readonly String MySqlType = "Database.MySql.MySqlDataAccessPool";
        private static readonly String MemoryConnectionString = "Mail.md";
        private static readonly String SqlServerConnectionString = "Data Source=.;Integrated Security=True;Database=Mail";
        private static readonly String PostgreSqlConnectionString = "Server=localhost;User ID=postgres;Password={Password};Database=mail;";
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
        private static Func<String, IDataAccess> GetConstructor(Type t)
        {
            var c = (IDataAccessPool)(Activator.CreateInstance(t));
            return c.Create;
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
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(SqlServerType);
                if (t != null)
                {
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(PostgreSqlType);
                if (t != null)
                {
                    var c = GetConstructor(t);
                    ConnectionFactory = () => c(ConnectionString);
                    return;
                }
            }
            {
                var t = GetType(MySqlType);
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
            if (Type == DatabaseType.Memory)
            {
                var t = GetType(MemoryType, true);
                var c = GetConstructor(t);
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.SqlServer)
            {
                var t = GetType(SqlServerType, true);
                var c = GetConstructor(t);
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                var t = GetType(PostgreSqlType, true);
                var c = GetConstructor(t);
                ConnectionFactory = () => c(ConnectionString);
            }
            else if (Type == DatabaseType.MySQL)
            {
                var t = GetType(MySqlType, true);
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