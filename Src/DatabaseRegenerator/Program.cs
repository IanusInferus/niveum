//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.DatabaseRegenerator <Visual C#>
//  Description: 数据库重建工具
//  Version:     2015.02.27.
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
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;
using Yuki.RelationSchema;
using Yuki.RelationSchema.TSql;
using Yuki.RelationSchema.PostgreSql;
using Yuki.RelationSchema.MySql;
using Yuki.RelationSchema.FoundationDbSql;
using Yuki.RelationValue;
using Yuki.RelationSchemaDiff;

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
                var optNameLower = opt.Name.ToLower();
                if ((optNameLower == "?") || (optNameLower == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (optNameLower == "loadtyperef")
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
                else if (optNameLower == "loadtype")
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
                else if (optNameLower == "connect")
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
                else if (optNameLower == "database")
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
                else if ((optNameLower == "genm") || (optNameLower == "regenm"))
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        GenerateMemoryFileWithoutSchema(Schema(), ConnectionString, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if ((optNameLower == "genms") || (optNameLower == "regenms"))
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        GenerateMemoryFileWithSchema(Schema(), ConnectionString, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "regenmssql")
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
                else if (optNameLower == "regenpgsql")
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
                else if (optNameLower == "regenmysql")
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
                else if (optNameLower == "regenfdbsql")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 0)
                    {
                        RegenFoundationDBSQL(Schema(), ConnectionString, DatabaseName, args);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "exportmssql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ExportSqlServer(Schema(), ConnectionString, DatabaseName, args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "exportpgsql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ExportPostgreSQL(Schema(), ConnectionString, DatabaseName, args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "exportmysql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ExportMySQL(Schema(), ConnectionString, DatabaseName, args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "exportfdbsql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ExportFoundationDBSQL(Schema(), ConnectionString, DatabaseName, args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "diff")
                {
                    var args = opt.Arguments;
                    if (args.Length == 3)
                    {
                        GenerateSchemaDiff(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "applydiff")
                {
                    var args = opt.Arguments;
                    if (args.Length == 4)
                    {
                        ApplySchemaDiff(args[0], args[1], args[2], args[3]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else
                {
                    throw new ArgumentException(opt.Name);
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
            Console.WriteLine(@"/loadtyperef:<RelationSchemaDir|RelationSchemaFile>");
            Console.WriteLine(@"装载类型定义");
            Console.WriteLine(@"/loadtype:<RelationSchemaDir|RelationSchemaFile>");
            Console.WriteLine(@"连接数据库");
            Console.WriteLine(@"/connect:<ConnectionString>");
            Console.WriteLine(@"指定数据库名称");
            Console.WriteLine(@"/database:<DatabaseName>");
            Console.WriteLine(@"创建Memory数据库(不包含Schema)");
            Console.WriteLine(@"/genm:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"/regenm:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"创建Memory数据库(包含Schema)");
            Console.WriteLine(@"/genms:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"/regenms:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"重建SQL Server数据库");
            Console.WriteLine(@"/regenmssql:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"重建PostgreSQL数据库");
            Console.WriteLine(@"/regenpgsql:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"重建MySQL数据库");
            Console.WriteLine(@"/regenmysql:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"重建FoundationDB/PostgreSQL数据库");
            Console.WriteLine(@"/regenfdbsql:(<DataDir>|<MemoryDatabaseFile>)*");
            Console.WriteLine(@"导出SQL Server数据库");
            Console.WriteLine(@"/exportmssql:<MemoryDatabaseFile>");
            Console.WriteLine(@"导出PostgreSQL数据库");
            Console.WriteLine(@"/exportpgsql:<MemoryDatabaseFile>");
            Console.WriteLine(@"导出MySQL数据库");
            Console.WriteLine(@"/exportmysql:<MemoryDatabaseFile>");
            Console.WriteLine(@"导出FoundationDB/PostgreSQL数据库");
            Console.WriteLine(@"/exportfdbsql:<MemoryDatabaseFile>");
            Console.WriteLine(@"生成数据库结构差异文件");
            Console.WriteLine(@"/diff:<MemoryDatabaseFileOld>,<MemoryDatabaseFileNew>,<SchemaDiffFile>");
            Console.WriteLine(@"应用数据库结构差异生成新数据库");
            Console.WriteLine(@"/applydiff:<MemoryDatabaseFileOld>,<MemoryDatabaseFileNew>,<SchemaDiffFile>,<MemoryDatabaseFileOutput>");
            Console.WriteLine(@"RelationSchemaDir|RelationSchemaFile 关系类型结构Tree文件(夹)路径。");
            Console.WriteLine(@"ConnectionString 数据库连接字符串。");
            Console.WriteLine(@"DataDir 数据目录，里面有若干tree数据文件。");
            Console.WriteLine(@"MemoryDatabaseFile 内存数据文件(包含Schema)路径。");
            Console.WriteLine(@"MemoryDatabaseFileOld 旧数据文件(包含Schema)路径。");
            Console.WriteLine(@"MemoryDatabaseFileNew 新数据文件(包含Schema)路径。");
            Console.WriteLine(@"SchemaDiffFile 结构差异文件路径。");
            Console.WriteLine(@"MemoryDatabaseFileOutput 输出数据文件(包含Schema)路径。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:Data.md /genm:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""Data Source=.;Integrated Security=True"" /database:Example /regenmssql:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""Server=localhost;User ID=postgres;Password=postgres;"" /database:Example /regenpgsql:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""server=localhost;uid=root"" /database:Example /regenmysql:Data,TestData");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""Data Source=.;Integrated Security=True"" /database:Example /exportmssql:Data.md");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""Server=localhost;User ID=postgres;Password=postgres;"" /database:Example /exportpgsql:Data.md");
            Console.WriteLine(@"DatabaseRegenerator /loadtype:DatabaseSchema /connect:""server=localhost;uid=root"" /database:Example /exportmysql:Data.md");
        }

        public static RelationVal LoadData(RelationSchema.Schema s, String[] DataDirOrMemoryDatabaseFiles, Firefly.Mapping.Binary.BinarySerializer bs)
        {
            var Entities = s.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToArray();
            var DuplicatedCollectionNames = Entities.GroupBy(e => e.CollectionName).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (DuplicatedCollectionNames.Count > 0)
            {
                throw new InvalidOperationException("DuplicatedCollectionNames: {0}".Formats(String.Join(" ", DuplicatedCollectionNames)));
            }
            var CollectionNameToEntity = Entities.ToDictionary(e => e.CollectionName, StringComparer.OrdinalIgnoreCase);

            var SchemaHash = s.Hash();

            var Files = new Dictionary<String, Byte[]>();
            var MemoryFiles = new List<RelationVal>();
            foreach (var DataDirOrMemoryDatabaseFile in DataDirOrMemoryDatabaseFiles)
            {
                if (Directory.Exists(DataDirOrMemoryDatabaseFile))
                {
                    foreach (var f in Directory.GetFiles(DataDirOrMemoryDatabaseFile, "*.tree", SearchOption.AllDirectories).OrderBy(ff => ff, StringComparer.OrdinalIgnoreCase))
                    {
                        Files.Add(f, System.IO.File.ReadAllBytes(f));
                    }
                }
                else
                {
                    Yuki.RelationValue.RelationVal FileValue;
                    using (var fs = Streams.OpenReadable(DataDirOrMemoryDatabaseFile))
                    {
                        var Hash = fs.ReadUInt64();
                        if (Hash != SchemaHash) { throw new InvalidOperationException("DatabaseSchemaVersionMismatch"); }
                        var FileSchema = bs.Read<Yuki.RelationSchema.Schema>(fs);
                        var rvs = new Yuki.RelationValue.RelationValueSerializer(FileSchema);
                        FileValue = rvs.Read(fs);
                        if (fs.Position != fs.Length) { throw new InvalidOperationException(); }
                    }
                    MemoryFiles.Add(FileValue);
                }
            }
            var Forests = Files.AsParallel().Select(p =>
            {
                using (var bas = new ByteArrayStream(p.Value))
                {
                    using (var sr = Txt.CreateTextReader(bas.AsNewReading(), Firefly.TextEncoding.TextEncoding.Default))
                    {
                        return TreeFile.ReadDirect(sr, p.Key, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting()).Value.Nodes;
                    }
                }
            }).ToList();

            var Tables = new Dictionary<String, List<Node>>();
            foreach (var Forest in Forests)
            {
                foreach (var n in Forest)
                {
                    if (!n.OnStem) { continue; }
                    var Name = n.Stem.Name;
                    if (CollectionNameToEntity.ContainsKey(Name))
                    {
                        Name = CollectionNameToEntity[Name].Name;
                    }
                    if (!Tables.ContainsKey(Name))
                    {
                        Tables.Add(Name, new List<Node>());
                    }
                    var t = Tables[Name];
                    t.AddRange(n.Stem.Children);
                }
            }

            var Missings = Entities.Select(e => e.Name).Except(Tables.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var Name in Missings)
            {
                Tables.Add(Name, new List<Node>());
            }

            var rvts = new RelationValueTreeSerializer(s);
            var Value = rvts.Read(Tables);
            foreach (var FileValue in MemoryFiles)
            {
                for (int k = 0; k < FileValue.Tables.Count; k += 1)
                {
                    Value.Tables[k].Rows.AddRange(FileValue.Tables[k].Rows);
                }
            }

            return Value;
        }
        public static void SaveData(RelationSchema.Schema s, String MemoryDatabaseFile, RelationVal Value, Firefly.Mapping.Binary.BinarySerializer bs)
        {
            var rvs = new RelationValueSerializer(s);

            Byte[] Bytes;
            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Write(ms, s);
                rvs.Write(ms, Value);
                ms.Position = 0;
                Bytes = ms.Read((int)(ms.Length));
            }

            var Dir = FileNameHandling.GetFileDirectory(MemoryDatabaseFile);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            using (var fs = Streams.CreateWritable(MemoryDatabaseFile))
            {
                fs.WriteUInt64(s.Hash());
                fs.Write(Bytes);
            }
        }
        public static void SaveDataWithoutSchema(RelationSchema.Schema s, String MemoryDatabaseFile, RelationVal Value, Firefly.Mapping.Binary.BinarySerializer bs)
        {
            var rvs = new RelationValueSerializer(s);

            Byte[] Bytes;
            using (var ms = Streams.CreateMemoryStream())
            {
                rvs.Write(ms, Value);
                ms.Position = 0;
                Bytes = ms.Read((int)(ms.Length));
            }

            var Dir = FileNameHandling.GetFileDirectory(MemoryDatabaseFile);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            using (var fs = Streams.CreateWritable(MemoryDatabaseFile))
            {
                fs.WriteUInt64(s.Hash());
                fs.Write(Bytes);
            }
        }

        public static void GenerateMemoryFileWithoutSchema(RelationSchema.Schema s, String ConnectionString, String[] DataDirOrMemoryDatabaseFiles)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();
            var rvs = new RelationValueSerializer(s);

            var Value = LoadData(s, DataDirOrMemoryDatabaseFiles, bs);
            SaveDataWithoutSchema(s, ConnectionString, Value, bs);
        }

        public static void GenerateMemoryFileWithSchema(RelationSchema.Schema s, String ConnectionString, String[] DataDirOrMemoryDatabaseFiles)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();
            var rvs = new RelationValueSerializer(s);

            var Value = LoadData(s, DataDirOrMemoryDatabaseFiles, bs);
            SaveData(s, ConnectionString, Value, bs);
        }

        public static void RegenSqlServer(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirOrMemoryDatabaseFiles)
        {
            if (DatabaseName == "")
            {
                throw new InvalidOperationException("数据库名称没有指定。");
            }

            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var Value = LoadData(s, DataDirOrMemoryDatabaseFiles, bs);

            var cf = GetConnectionFactory(DatabaseType.SqlServer);
            using (var c = cf(ConnectionString))
            {
                var RegenSqls = Regex.Split(s.CompileToTSql(DatabaseName, true), @"\r\nGO(\r\n)+", RegexOptions.ExplicitCapture);
                c.Open();
                foreach (var Sql in RegenSqls)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;
            var Tables = TableOperations.GetTableDictionary(s, EntityMetas, Value);

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                using (var b = c.BeginTransaction())
                {
                    var Success = false;
                    try
                    {
                        foreach (var t in EntityMetas)
                        {
                            var CollectionName = t.Value.CollectionName;

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
                            TableOperations.ImportTable(EntityMetas, EnumUnderlyingTypes, c, b, t, DatabaseType.SqlServer);
                        }
                        foreach (var t in EntityMetas)
                        {
                            var CollectionName = t.Value.CollectionName;

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
        }

        public static void RegenPostgreSQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirOrMemoryDatabaseFiles)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var Value = LoadData(s, DataDirOrMemoryDatabaseFiles, bs);

            var GenSqls = s.CompileToPostgreSql(DatabaseName, true);
            var RegenSqls = Regex.Split(GenSqls, @"\r\n;(\r\n)+", RegexOptions.ExplicitCapture);
            RegenSqls = RegenSqls.SkipWhile(q => !q.StartsWith("CREATE TABLE")).ToArray();
            var CreateDatabases = new String[] { String.Format("DROP DATABASE IF EXISTS \"{0}\"", DatabaseName.ToLowerInvariant()), String.Format("CREATE DATABASE \"{0}\"", DatabaseName.ToLowerInvariant()) };
            var Creates = RegenSqls.Where(q => q.StartsWith("CREATE")).ToArray();
            var Alters = RegenSqls.Where(q => q.StartsWith("ALTER") || q.StartsWith("COMMENT")).ToArray();

            var cf = GetConnectionFactory(DatabaseType.PostgreSQL);
            using (var c = cf(ConnectionString))
            {
                c.Open();
                foreach (var Sql in CreateDatabases)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                foreach (var Sql in Creates)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;
            var Tables = TableOperations.GetTableDictionary(s, EntityMetas, Value);

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                using (var b = c.BeginTransaction())
                {
                    var Success = false;
                    try
                    {
                        foreach (var t in Tables)
                        {
                            TableOperations.ImportTable(EntityMetas, EnumUnderlyingTypes, c, b, t, DatabaseType.PostgreSQL);
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

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                foreach (var Sql in Alters)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void RegenMySQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirOrMemoryDatabaseFiles)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var Value = LoadData(s, DataDirOrMemoryDatabaseFiles, bs);

            var GenSqls = s.CompileToMySql(DatabaseName, true);
            var RegenSqls = Regex.Split(GenSqls, @"\r\n;(\r\n)+", RegexOptions.ExplicitCapture);
            var Creates = RegenSqls.TakeWhile(q => !q.StartsWith("ALTER")).ToArray();
            var Alters = RegenSqls.SkipWhile(q => !q.StartsWith("ALTER")).ToArray();

            var cf = GetConnectionFactory(DatabaseType.MySQL);
            using (var c = cf(ConnectionString))
            {
                c.Open();
                foreach (var Sql in Creates)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;
            var Tables = TableOperations.GetTableDictionary(s, EntityMetas, Value);

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                using (var b = c.BeginTransaction())
                {
                    var Success = false;
                    try
                    {
                        foreach (var t in Tables)
                        {
                            TableOperations.ImportTable(EntityMetas, EnumUnderlyingTypes, c, b, t, DatabaseType.MySQL);
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

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                foreach (var Sql in Alters)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void RegenFoundationDBSQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String[] DataDirOrMemoryDatabaseFiles)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var Value = LoadData(s, DataDirOrMemoryDatabaseFiles, bs);

            var GenSqls = s.CompileToFoundationDbSql(DatabaseName);
            var RegenSqls = Regex.Split(GenSqls, @"\r\n;(\r\n)+", RegexOptions.ExplicitCapture);
            RegenSqls = RegenSqls.SkipWhile(q => !q.StartsWith("CREATE TABLE")).ToArray();
            var CreateDatabases = new String[] { String.Format("DROP SCHEMA IF EXISTS \"{0}\" CASCADE", DatabaseName.ToLowerInvariant()), String.Format("CREATE SCHEMA \"{0}\"", DatabaseName.ToLowerInvariant()) };
            var Creates = RegenSqls.Where(q => q.StartsWith("CREATE")).ToArray();
            var Alters = RegenSqls.Where(q => q.StartsWith("ALTER")).ToArray();

            var cf = GetConnectionFactory(DatabaseType.FoundationDBSQL);
            using (var c = cf(ConnectionString))
            {
                c.Open();
                foreach (var Sql in CreateDatabases)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                foreach (var Sql in Creates)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;
            var Tables = TableOperations.GetTableDictionary(s, EntityMetas, Value);

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                foreach (var t in Tables)
                {
                    var PartitionSize = 1000;
                    var NumPartition = (t.Value.Rows.Count + PartitionSize - 1) / PartitionSize;
                    for (int k = 0; k < NumPartition; k += 1)
                    {
                        var p = new KeyValuePair<String, TableVal>(t.Key, new TableVal { Rows = t.Value.Rows.Skip(k * PartitionSize).Take(PartitionSize).ToList() });
                        using (var b = c.BeginTransaction())
                        {
                            var Success = false;
                            try
                            {
                                TableOperations.ImportTable(EntityMetas, EnumUnderlyingTypes, c, b, p, DatabaseType.FoundationDBSQL);

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
                }
            }

            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                foreach (var Sql in Alters)
                {
                    if (Sql == "") { continue; }

                    var cmd = c.CreateCommand();
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void ExportSqlServer(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String MemoryDatabaseFile)
        {
            if (DatabaseName == "")
            {
                throw new InvalidOperationException("数据库名称没有指定。");
            }

            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var cf = GetConnectionFactory(DatabaseType.SqlServer);

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;

            var Value = new RelationVal { Tables = new List<TableVal>() };
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                using (var b = c.BeginTransaction())
                {
                    try
                    {
                        foreach (var t in EntityMetas)
                        {
                            var Table = TableOperations.ExportTable(EntityMetas, EnumUnderlyingTypes, c, b, t.Value.Name, DatabaseType.SqlServer);
                            Value.Tables.Add(Table);
                        }
                    }
                    finally
                    {
                        b.Rollback();
                    }
                }
            }

            SaveData(s, MemoryDatabaseFile, Value, bs);
        }

        public static void ExportPostgreSQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String MemoryDatabaseFile)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var cf = GetConnectionFactory(DatabaseType.PostgreSQL);

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;

            var Value = new RelationVal { Tables = new List<TableVal>() };
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName.ToLowerInvariant());
                using (var b = c.BeginTransaction())
                {
                    try
                    {
                        foreach (var t in EntityMetas)
                        {
                            var Table = TableOperations.ExportTable(EntityMetas, EnumUnderlyingTypes, c, b, t.Value.Name, DatabaseType.PostgreSQL);
                            Value.Tables.Add(Table);
                        }
                    }
                    finally
                    {
                        b.Rollback();
                    }
                }
            }

            SaveData(s, MemoryDatabaseFile, Value, bs);
        }

        public static void ExportMySQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String MemoryDatabaseFile)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var cf = GetConnectionFactory(DatabaseType.MySQL);

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;

            var Value = new RelationVal { Tables = new List<TableVal>() };
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                using (var b = c.BeginTransaction())
                {
                    try
                    {
                        foreach (var t in EntityMetas)
                        {
                            var Table = TableOperations.ExportTable(EntityMetas, EnumUnderlyingTypes, c, b, t.Value.Name, DatabaseType.MySQL);
                            Value.Tables.Add(Table);
                        }
                    }
                    finally
                    {
                        b.Rollback();
                    }
                }
            }

            SaveData(s, MemoryDatabaseFile, Value, bs);
        }

        public static void ExportFoundationDBSQL(RelationSchema.Schema s, String ConnectionString, String DatabaseName, String MemoryDatabaseFile)
        {
            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();

            var cf = GetConnectionFactory(DatabaseType.FoundationDBSQL);

            var TableInfo = TableOperations.GetTableInfo(s);
            var EntityMetas = TableInfo.EntityMetas;
            var EnumUnderlyingTypes = TableInfo.EnumUnderlyingTypes;

            var Value = new RelationVal { Tables = new List<TableVal>() };
            using (var c = cf(ConnectionString))
            {
                c.Open();
                c.ChangeDatabase(DatabaseName);
                using (var b = c.BeginTransaction())
                {
                    try
                    {
                        foreach (var t in EntityMetas)
                        {
                            var Table = TableOperations.ExportTable(EntityMetas, EnumUnderlyingTypes, c, b, t.Value.Name, DatabaseType.FoundationDBSQL);
                            Value.Tables.Add(Table);
                        }
                    }
                    finally
                    {
                        b.Rollback();
                    }
                }
            }

            SaveData(s, MemoryDatabaseFile, Value, bs);
        }

        private static Func<String, IDbConnection> GetConnectionFactory(DatabaseType Type)
        {
            if (Type == DatabaseType.SqlServer)
            {
                return GetConnectionFactorySqlServer();
            }
            else if ((Type == DatabaseType.PostgreSQL) || (Type == DatabaseType.FoundationDBSQL))
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

        public static void GenerateSchemaDiff(String MemoryDatabaseFileOld, String MemoryDatabaseFileNew, String SchemaDiffFile)
        {
            Schema SchemaOld;
            Schema SchemaNew;

            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();
            using (var ms = Streams.OpenReadable(MemoryDatabaseFileOld))
            {
                ms.ReadUInt64();
                SchemaOld = bs.Read<Schema>(ms);
            }
            using (var ms = Streams.OpenReadable(MemoryDatabaseFileNew))
            {
                ms.ReadUInt64();
                SchemaNew = bs.Read<Schema>(ms);
            }

            var Dir = FileNameHandling.GetFileDirectory(SchemaDiffFile);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }

            var d = RelationSchemaDiffGenerator.Generate(SchemaOld, SchemaNew);
            RelationSchemaDiffVerifier.Verifiy(SchemaOld, SchemaNew, d.Mappings);
            using (var sw = Txt.CreateTextWriter(SchemaDiffFile))
            {
                RelationSchemaDiffWriter.Write(sw, d);
            }
        }
        public static void ApplySchemaDiff(String MemoryDatabaseFileOld, String MemoryDatabaseFileNew, String SchemaDiffFile, String MemoryDatabaseFileOutput)
        {
            Schema SchemaOld;
            Schema SchemaNew;

            var bs = Yuki.ObjectSchema.BinarySerializerWithString.Create();
            using (var ms = Streams.OpenReadable(MemoryDatabaseFileNew))
            {
                ms.ReadUInt64();
                SchemaNew = bs.Read<Schema>(ms);
            }
            var Loader = new RelationSchemaDiffLoader(SchemaNew);
            Loader.LoadType(SchemaDiffFile);
            var l = Loader.GetResult();

            using (var ms = Streams.OpenReadable(MemoryDatabaseFileOld))
            {
                ms.ReadUInt64();
                SchemaOld = bs.Read<Schema>(ms);

                RelationSchemaDiffVerifier.Verifiy(SchemaOld, SchemaNew, l);
                var orvs = new RelationValueSerializer(SchemaOld);
                var nrvs = new RelationValueSerializer(SchemaNew);

                var OldEntities = SchemaOld.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToList();
                var OldEntityNameToIndex = OldEntities.Select((e, i) => new KeyValuePair<String, int>(e.Name, i)).ToDictionary(p => p.Key, p => p.Value);
                var NewEntities = SchemaNew.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToList();
                var dt = new RelationSchemaDiffTranslator(SchemaOld, SchemaNew, l);

                var Dir = FileNameHandling.GetFileDirectory(MemoryDatabaseFileOutput);
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }

                var OldEntityCount = OldEntities.Count;

                var Positions = new Dictionary<int, Int64>();
                var CurrentEntityTableIndex = 0;
                var CurrentCount = 0;
                if (OldEntityCount > 0)
                {
                    Positions.Add(0, ms.Position);
                    CurrentCount = ms.ReadInt32();
                }
                Action<int> AdvanceTo = EntityTableIndex =>
                {
                    if (Positions.ContainsKey(EntityTableIndex))
                    {
                        ms.Position = Positions[EntityTableIndex];
                        CurrentEntityTableIndex = EntityTableIndex;
                        CurrentCount = ms.ReadInt32();
                    }
                    else
                    {
                        if ((EntityTableIndex < 0) || (EntityTableIndex >= OldEntityCount)) { throw new InvalidOperationException(); }
                        while (CurrentEntityTableIndex < EntityTableIndex)
                        {
                            var rr = orvs.GetRowReader(OldEntities[CurrentEntityTableIndex]);
                            while (CurrentCount > 0)
                            {
                                rr(ms);
                                CurrentCount -= 1;
                            }
                            CurrentEntityTableIndex += 1;
                            if (CurrentEntityTableIndex >= OldEntityCount) { break; }
                            Positions.Add(CurrentEntityTableIndex, ms.Position);
                            CurrentCount = ms.ReadInt32();
                        }
                    }
                };

                using (var mso = Streams.CreateResizable(MemoryDatabaseFileOutput))
                {
                    mso.WriteUInt64(SchemaNew.Hash());
                    bs.Write(SchemaNew, mso);

                    foreach (var ne in NewEntities)
                    {
                        var oOldEntityName = dt.GetOldEntityName(ne.Name);
                        if (oOldEntityName.OnNotHasValue)
                        {
                            mso.WriteInt32(0);
                            continue;
                        }
                        var Index = OldEntityNameToIndex[oOldEntityName.HasValue];
                        var rr = orvs.GetRowReader(OldEntities[Index]);
                        var rw = nrvs.GetRowWriter(ne);
                        var t = dt.GetTranslator(ne.Name);
                        AdvanceTo(Index);

                        mso.WriteInt32(CurrentCount);
                        while (CurrentCount > 0)
                        {
                            rw(mso, t(rr(ms)));
                            CurrentCount -= 1;
                        }
                    }
                }
            }
        }
    }
}
