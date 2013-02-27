//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.DatabaseRegenerator <Visual C#>
//  Description: 数据库重建工具
//  Version:     2013.02.27.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using System.Data;
using Firefly;
using Firefly.Streaming;
using Yuki.ObjectSchema;
using OS = Yuki.ObjectSchema;
using Yuki.RelationSchema;
using Yuki.RelationSchema.TSql;
using Yuki.RelationSchema.PostgreSql;
using Yuki.RelationSchema.MySql;

namespace Yuki.DatabaseRegenerator
{
    public sealed class Program
    {
        public static int Main()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return MainInner();
            }
            else
            {
                try
                {
                    return MainInner();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                    return -1;
                }
            }
        }

        public static int MainInner()
        {
            var CmdLine = CommandLine.GetCmdLine();
            var argv = CmdLine.Arguments;

            if (CmdLine.Arguments.Length != 0)
            {
                DisplayInfo();
                return -1;
            }

            if (CmdLine.Options.Length == 0)
            {
                DisplayInfo();
                return 0;
            }

            var rsl = new RelationSchemaLoader();
            RelationSchema.Schema rs = null;
            Func<RelationSchema.Schema> Schema = () =>
            {
                if (rs == null)
                {
                    rs = rsl.GetResult();
                    rs.Verify();
                }
                return rs;
            };

            String ConnectionString = null;
            String DatabaseName = null;
            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (opt.Name.ToLower() == "loadtyperef")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var SchemaPath = args[0];
                        if (Directory.Exists(SchemaPath))
                        {
                            foreach (var f in Directory.GetFiles(SchemaPath, "*.tree", SearchOption.AllDirectories).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                            {
                                rsl.LoadTypeRef(f);
                            }
                        }
                        else
                        {
                            rsl.LoadTypeRef(SchemaPath);
                        }
                        rs = null;
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "loadtype")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var SchemaPath = args[0];
                        if (Directory.Exists(SchemaPath))
                        {
                            foreach (var f in Directory.GetFiles(SchemaPath, "*.tree", SearchOption.AllDirectories).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                            {
                                rsl.LoadType(f);
                            }
                        }
                        else
                        {
                            rsl.LoadType(SchemaPath);
                        }
                        rs = null;
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "connect")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ConnectionString = args[0];
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "database")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        DatabaseName = args[0];
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "regenm")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        RegenMemory(Schema(), ConnectionString, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "regenmssql")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        RegenSqlServer(Schema(), ConnectionString, DatabaseName, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "regenpgsql")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        RegenPostgreSQL(Schema(), ConnectionString, DatabaseName, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "regenmysql")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        RegenMySQL(Schema(), ConnectionString, DatabaseName, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else
                {
                    throw (new ArgumentException(opt.Name));
                }
            }
            return 0;
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"数据库重建工具");
            Console.WriteLine(@"Yuki.DatabaseRegenerator，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于重建数据库，并导入数据。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"DatabaseRegenerator (/<Command>)*");
            Console.WriteLine(@"装载类型引用");
            Console.WriteLine(@"/loadtyperef:<ObjectSchemaDir|ObjectSchemaFile>");
            Console.WriteLine(@"装载类型定义");
            Console.WriteLine(@"/loadtype:<ObjectSchemaDir|ObjectSchemaFile>");
            Console.WriteLine(@"连接数据库");
            Console.WriteLine(@"/connect:<ConnectionString>");
            Console.WriteLine(@"指定数据库名称");
            Console.WriteLine(@"/database:<DatabaseName>");
            Console.WriteLine(@"重建Memory数据库");
            Console.WriteLine(@"/regenm:<DataDir>*");
            Console.WriteLine(@"重建SQL Server数据库");
            Console.WriteLine(@"/regenmssql:<DataDir>*");
            Console.WriteLine(@"重建PostgreSQL数据库");
            Console.WriteLine(@"/regenpgsql:<DataDir>*");
            Console.WriteLine(@"重建MySQL数据库");
            Console.WriteLine(@"/regenmysql:<DataDir>*");
            Console.WriteLine(@"ObjectSchemaDir|ObjectSchemaFile 对象类型结构Tree文件(夹)路径。");
            Console.WriteLine(@"ConnectionString 数据库连接字符串。");
            Console.WriteLine(@"DataDir 数据目录，里面有若干tree数据文件。");
            Console.WriteLine(@"DataFilePath 数据文件路径。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:Data.md /regenm:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""Data Source=.;Integrated Security=True"" /database:Example /regenmssql:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""Server=localhost;User ID=postgres;Password=postgres;"" /database:Example /regenpgsql:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""server=localhost;uid=root"" /database:Example /regenmysql:Data,TestData");
        }

        public static void RegenMemory(RelationSchema.Schema s, String ConnectionString, String[] DataDirs)
        {
            var l = s.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToList();
            var d = l.ToDictionary(e => e.Name, e => (Byte[])(null), StringComparer.OrdinalIgnoreCase);
            foreach (var DataDir in DataDirs)
            {
                var ImportTableMetas = TableOperations.GetImportTableMetas(s, DataDir);
                var Tables = ImportTableMetas.Tables;
                var TableMetas = ImportTableMetas.TableMetas;
                var EnumUnderlyingTypes = ImportTableMetas.EnumUnderlyingTypes;
                var EnumMetas = ImportTableMetas.EnumMetas;
                foreach (var t in Tables)
                {
                    var EntityName = TableMetas[t.Key].Name;
                    using (var ms = Streams.CreateMemoryStream())
                    {
                        TableOperationsMemory.ImportTable(TableMetas, EnumUnderlyingTypes, EnumMetas, ms, t);
                        ms.Position = 0;
                        d[EntityName] = ms.Read((int)(ms.Length));
                    }
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(ConnectionString);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            using (var fs = Streams.CreateWritable(ConnectionString))
            {
                foreach (var e in l)
                {
                    var Bytes = d[e.Name];
                    if (Bytes == null)
                    {
                        fs.WriteInt32(0);
                    }
                    else
                    {
                        fs.Write(Bytes);
                    }
                }
            }
        }

        public static void RegenSqlServer(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirs)
        {
            if (DatabaseName == "")
            {
                throw new InvalidOperationException("数据库名称没有指定。");
            }

            var cf = GetConnectionFactory(DatabaseType.SqlServer);
            using (var c = cf(ConnectionString))
            {
                var RegenSqls = Regex.Split(s.CompileToTSql(DatabaseName, true), @"\r\nGO(\r\n)+", RegexOptions.ExplicitCapture);
                c.Open();
                try
                {
                    foreach (var Sql in RegenSqls)
                    {
                        if (Sql == "") { continue; }

                        var cmd = c.CreateCommand();
                        cmd.CommandText = Sql;
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    c.Close();
                }
            }

            foreach (var DataDir in DataDirs)
            {
                var ImportTableMetas = TableOperations.GetImportTableMetas(s, DataDir);
                var Tables = ImportTableMetas.Tables;
                var TableMetas = ImportTableMetas.TableMetas;
                var EnumMetas = ImportTableMetas.EnumMetas;

                using (var c = cf(ConnectionString))
                {
                    c.Open();
                    c.ChangeDatabase(DatabaseName);
                    try
                    {
                        using (var b = c.BeginTransaction())
                        {
                            var Success = false;
                            try
                            {
                                foreach (var t in TableMetas)
                                {
                                    var CollectionName = t.Key;

                                    {
                                        IDbCommand cmd = c.CreateCommand();
                                        cmd.Transaction = b;
                                        cmd.CommandText = String.Format("ALTER TABLE [{0}] NOCHECK CONSTRAINT ALL", CollectionName);
                                        cmd.CommandType = CommandType.Text;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                foreach (var t in Tables)
                                {
                                    TableOperations.ImportTable(TableMetas, EnumMetas, c, b, t, DatabaseType.SqlServer);
                                }
                                foreach (var t in TableMetas)
                                {
                                    var CollectionName = t.Key;

                                    {
                                        IDbCommand cmd = c.CreateCommand();
                                        cmd.Transaction = b;
                                        cmd.CommandText = String.Format("ALTER TABLE [{0}] WITH CHECK CHECK CONSTRAINT ALL", CollectionName);
                                        cmd.CommandType = CommandType.Text;
                                        cmd.ExecuteNonQuery();
                                    }
                                }

                                b.Commit();
                                Success = true;
                            }
                            finally
                            {
                                if (!Success)
                                {
                                    b.Rollback();
                                }
                            }
                        }
                    }
                    finally
                    {
                        c.Close();
                    }
                }
            }
        }

        public static void RegenPostgreSQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirs)
        {
            var GenSqls = s.CompileToPostgreSql(DatabaseName);
            var RegenSqls = Regex.Split(GenSqls, @"\r\n;(\r\n)+", RegexOptions.ExplicitCapture);
            RegenSqls = RegenSqls.SkipWhile(q => !q.StartsWith("CREATE TABLE")).ToArray();
            var CreateDatabases = new String[] { String.Format("DROP DATABASE IF EXISTS \"{0}\"", DatabaseName.ToLowerInvariant()), String.Format("CREATE DATABASE \"{0}\"", DatabaseName.ToLowerInvariant()) };
            var Creates = RegenSqls.Where(q => q.StartsWith("CREATE")).ToArray();
            var Alters = RegenSqls.Where(q => q.StartsWith("ALTER")).ToArray();

            var cf = GetConnectionFactory(DatabaseType.PostgreSQL);
            using (var c = cf(ConnectionString))
            {
                c.Open();
                try
                {
                    foreach (var Sql in CreateDatabases)
                    {
                        if (Sql == "") { continue; }

                        var cmd = c.CreateCommand();
                        cmd.CommandText = Sql;
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    c.Close();
                }
            }
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                try
                {
                    foreach (var Sql in Creates)
                    {
                        if (Sql == "") { continue; }

                        var cmd = c.CreateCommand();
                        cmd.CommandText = Sql;
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    c.Close();
                }
            }

            foreach (var DataDir in DataDirs)
            {
                var ImportTableMetas = TableOperations.GetImportTableMetas(s, DataDir);
                var Tables = ImportTableMetas.Tables;
                var TableMetas = ImportTableMetas.TableMetas;
                var EnumMetas = ImportTableMetas.EnumMetas;

                using (var c = cf(ConnectionString))
                {
                    c.Open();
                    c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                    try
                    {
                        using (var b = c.BeginTransaction())
                        {
                            var Success = false;
                            try
                            {
                                foreach (var t in Tables)
                                {
                                    TableOperations.ImportTable(TableMetas, EnumMetas, c, b, t, DatabaseType.PostgreSQL);
                                }

                                b.Commit();
                                Success = true;
                            }
                            finally
                            {
                                if (!Success)
                                {
                                    b.Rollback();
                                }
                            }
                        }
                    }
                    finally
                    {
                        c.Close();
                    }
                }
            }

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                try
                {
                    foreach (var Sql in Alters)
                    {
                        if (Sql == "") { continue; }

                        var cmd = c.CreateCommand();
                        cmd.CommandText = Sql;
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    c.Close();
                }
            }
        }

        public static void RegenMySQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirs)
        {
            var GenSqls = s.CompileToMySql(DatabaseName);
            var RegenSqls = Regex.Split(GenSqls, @"\r\n;(\r\n)+", RegexOptions.ExplicitCapture);
            var Creates = RegenSqls.TakeWhile(q => !q.StartsWith("ALTER")).ToArray();
            var Alters = RegenSqls.SkipWhile(q => !q.StartsWith("ALTER")).ToArray();

            var cf = GetConnectionFactory(DatabaseType.MySQL);
            using (var c = cf(ConnectionString))
            {
                c.Open();
                try
                {
                    foreach (var Sql in Creates)
                    {
                        if (Sql == "") { continue; }

                        var cmd = c.CreateCommand();
                        cmd.CommandText = Sql;
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    c.Close();
                }
            }

            foreach (var DataDir in DataDirs)
            {
                var ImportTableMetas = TableOperations.GetImportTableMetas(s, DataDir);
                var Tables = ImportTableMetas.Tables;
                var TableMetas = ImportTableMetas.TableMetas;
                var EnumMetas = ImportTableMetas.EnumMetas;

                using (var c = cf(ConnectionString))
                {
                    c.Open();
                    c.ChangeDatabase(DatabaseName);
                    try
                    {
                        using (var b = c.BeginTransaction())
                        {
                            var Success = false;
                            try
                            {
                                foreach (var t in Tables)
                                {
                                    TableOperations.ImportTable(TableMetas, EnumMetas, c, b, t, DatabaseType.MySQL);
                                }

                                b.Commit();
                                Success = true;
                            }
                            finally
                            {
                                if (!Success)
                                {
                                    b.Rollback();
                                }
                            }
                        }
                    }
                    finally
                    {
                        c.Close();
                    }
                }
            }

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                try
                {
                    foreach (var Sql in Alters)
                    {
                        if (Sql == "") { continue; }

                        var cmd = c.CreateCommand();
                        cmd.CommandText = Sql;
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    c.Close();
                }
            }
        }

        private static Func<String, IDbConnection> GetConnectionFactory(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                return GetConnectionFactorySqlServer();
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                return GetConnectionFactoryPostgreSQL();
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

        private static Func<String, IDbConnection> GetConnectionFactorySqlServer()
        {
            return ConnectionString => new System.Data.SqlClient.SqlConnection(ConnectionString);
        }
        private static Func<String, IDbConnection> GetConnectionFactoryPostgreSQL()
        {
            var Path = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Assembly.GetEntryAssembly().Location), "Npgsql.dll");
            var asm = Assembly.Load(AssemblyName.GetAssemblyName(Path));
            var t = asm.GetType("Npgsql.NpgsqlConnection");
            return ConnectionString => (IDbConnection)Activator.CreateInstance(t, ConnectionString);
        }
        private static Func<String, IDbConnection> GetConnectionFactoryMySQL()
        {
            var Path = FileNameHandling.GetPath(FileNameHandling.GetFileDirectory(Assembly.GetEntryAssembly().Location), "MySql.Data.dll");
            var asm = Assembly.Load(AssemblyName.GetAssemblyName(Path));
            var t = asm.GetType("MySql.Data.MySqlClient.MySqlConnection");
            return ConnectionString => (IDbConnection)Activator.CreateInstance(t, ConnectionString);
        }
    }
}
