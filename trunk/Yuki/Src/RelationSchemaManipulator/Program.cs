//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.RelationSchemaManipulator <Visual C#>
//  Description: 对象类型结构处理工具
//  Version:     2012.11.26.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.CodeDom.Compiler;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Yuki.RelationSchema;
using OS = Yuki.ObjectSchema;
using RS = Yuki.RelationSchema;
using Yuki.ObjectSchema.CSharp;
using Yuki.ObjectSchema.Cpp;
using Yuki.RelationSchema.TSql;
using Yuki.RelationSchema.PostgreSql;
using Yuki.RelationSchema.MySql;
using Yuki.RelationSchema.DbmlDatabase;
using Yuki.RelationSchema.CSharpLinqToSql;
using Yuki.RelationSchema.CSharpLinqToEntities;
using Yuki.RelationSchema.CSharpPlain;

namespace Yuki.RelationSchemaManipulator
{
    public static class Program
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
            TextEncoding.WritingDefault = TextEncoding.UTF8;

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
                                InvalidateSchema();
                                rsl.LoadTypeRef(f);
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            rsl.LoadTypeRef(SchemaPath);
                        }
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
                                InvalidateSchema();
                                rsl.LoadType(f);
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            rsl.LoadType(SchemaPath);
                        }
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "import")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        rsl.AddImport(args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2tsql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToTSqlCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2pgsql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToPostgreSqlCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2mysql")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToMySqlCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2dbml")
                {
                    var args = opt.Arguments;
                    if (args.Length == 5)
                    {
                        ObjectSchemaToDbmlDatabaseCode(args[0], args[1], args[2], args[3], args[4]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2csd")
                {
                    var args = opt.Arguments;
                    if (args.Length == 5)
                    {
                        ObjectSchemaToCSharpLinqToSqlCode(args[0], args[1], args[2], args[3], args[4]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2cse")
                {
                    var args = opt.Arguments;
                    if (args.Length == 5)
                    {
                        ObjectSchemaToCSharpLinqToEntitiesCode(args[0], args[1], args[2], args[3], args[4]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2csdp")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpDatabasePlainCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2cppdp")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToCppDatabasePlainCode(args[0], args[1]);
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
            Console.WriteLine(@"关系类型结构处理工具");
            Console.WriteLine("Yuki.RelationSchemaManipulator，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于从关系类型结构生成代码。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"RelationSchemaManipulator (/<Command>)*");
            Console.WriteLine(@"装载类型引用");
            Console.WriteLine(@"/loadtyperef:<RelationSchemaDir|RelationSchemaFile>");
            Console.WriteLine(@"装载类型定义");
            Console.WriteLine(@"/loadtype:<RelationSchemaDir|RelationSchemaFile>");
            Console.WriteLine(@"增加命名空间引用");
            Console.WriteLine(@"/import:<NamespaceName>");
            Console.WriteLine(@"生成T-SQL(SQL Server)数据库DROP和CREATE脚本");
            Console.WriteLine(@"/t2tsql:<SqlCodePath>,<DatabaseName>");
            Console.WriteLine(@"生成Dbml文件");
            Console.WriteLine(@"/t2dbml:<DbmlCodePath>,<DatabaseName>,<EntityNamespaceName>,<ContextNamespaceName>,<ContextClassName>");
            Console.WriteLine(@"生成C#数据库Linq to SQL类型");
            Console.WriteLine(@"/t2csd:<CsCodePath>,<DatabaseName>,<EntityNamespaceName>,<ContextNamespaceName>,<ContextClassName>");
            Console.WriteLine(@"生成C#数据库Linq to Entities类型");
            Console.WriteLine(@"/t2cse:<CsCodePath>,<DatabaseName>,<EntityNamespaceName>,<ContextNamespaceName>,<ContextClassName>");
            Console.WriteLine(@"生成C#数据库简单类型");
            Console.WriteLine(@"/t2csdp:<CsCodePath>,<EntityNamespaceName>");
            Console.WriteLine(@"生成C++数据库简单类型");
            Console.WriteLine(@"/t2cppdp:<CsCodePath>,<EntityNamespaceName>");
            Console.WriteLine(@"RelationSchemaDir|RelationSchemaFile 关系类型结构Tree文件(夹)路径。");
            Console.WriteLine(@"DatabaseName 数据库名。");
            Console.WriteLine(@"SqlCodePath SQL代码文件路径。");
            Console.WriteLine(@"DbmlCodePath Dbml文件路径。");
            Console.WriteLine(@"CsCodePath C#代码文件路径。");
            Console.WriteLine(@"EntityNamespaceName 实体命名空间名称。");
            Console.WriteLine(@"ContextNamespaceName 上下文命名空间名称。");
            Console.WriteLine(@"ContextClassName 上下文类名称。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"RelationSchemaManipulator /loadtype:DatabaseSchema /t2csd:Src\Generated\Database.cs,Example,Example.Database,Example.Database.Context,DbRoot");
        }

        private static RS.RelationSchemaLoader rsl = new RS.RelationSchemaLoader();
        private static RS.Schema rs = null;
        private static OS.Schema os = null;
        private static RS.Schema GetRelationSchema()
        {
            if (rs != null) { return rs; }
            rs = rsl.GetResult();
            rs.Verify();
            os = RS.PlainObjectSchemaGenerator.Generate(rs);
            return rs;
        }
        private static OS.Schema GetObjectSchema()
        {
            if (os != null) { return os; }
            rs = rsl.GetResult();
            rs.Verify();
            os = RS.PlainObjectSchemaGenerator.Generate(rs);
            return os;
        }
        private static void InvalidateSchema()
        {
            rs = null;
            os = null;
        }

        public static void ObjectSchemaToTSqlCode(String SqlCodePath, String DatabaseName)
        {
            var RelationSchema = GetRelationSchema();
            var Compiled = RelationSchema.CompileToTSql(DatabaseName, true);
            if (File.Exists(SqlCodePath))
            {
                var Original = Txt.ReadFile(SqlCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var SqlCodeDir = FileNameHandling.GetFileDirectory(SqlCodePath);
            if (SqlCodeDir != "" && !Directory.Exists(SqlCodeDir)) { Directory.CreateDirectory(SqlCodeDir); }
            Txt.WriteFile(SqlCodePath, Compiled);
        }

        public static void ObjectSchemaToPostgreSqlCode(String SqlCodePath, String DatabaseName)
        {
            var RelationSchema = GetRelationSchema();
            var Compiled = RelationSchema.CompileToPostgreSql(DatabaseName, true);
            if (File.Exists(SqlCodePath))
            {
                var Original = Txt.ReadFile(SqlCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var SqlCodeDir = FileNameHandling.GetFileDirectory(SqlCodePath);
            if (SqlCodeDir != "" && !Directory.Exists(SqlCodeDir)) { Directory.CreateDirectory(SqlCodeDir); }
            Txt.WriteFile(SqlCodePath, Compiled);
        }

        public static void ObjectSchemaToMySqlCode(String SqlCodePath, String DatabaseName)
        {
            var RelationSchema = GetRelationSchema();
            var Compiled = RelationSchema.CompileToMySql(DatabaseName, true);
            if (File.Exists(SqlCodePath))
            {
                var Original = Txt.ReadFile(SqlCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var SqlCodeDir = FileNameHandling.GetFileDirectory(SqlCodePath);
            if (SqlCodeDir != "" && !Directory.Exists(SqlCodeDir)) { Directory.CreateDirectory(SqlCodeDir); }
            Txt.WriteFile(SqlCodePath, Compiled);
        }

        public static void ObjectSchemaToDbmlDatabaseCode(String SqlCodePath, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            var RelationSchema = GetRelationSchema();
            var CompiledX = RelationSchema.CompileToDbmlDatabase(DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            String Compiled = "";
            using (var s = Streams.CreateMemoryStream())
            {
                using (var sw = Txt.CreateTextWriter(s.Partialize(0, Int64.MaxValue, 0).AsNewWriting(), TextEncoding.UTF8))
                {
                    XmlFile.WriteFile(sw, CompiledX);
                }
                s.Position = 0;
                using (var sr = Txt.CreateTextReader(s.Partialize(0, s.Length).AsNewReading(), TextEncoding.UTF8))
                {
                    Compiled = Txt.ReadFile(sr);
                }
            }
            if (File.Exists(SqlCodePath))
            {
                var Original = Txt.ReadFile(SqlCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var SqlCodeDir = FileNameHandling.GetFileDirectory(SqlCodePath);
            if (SqlCodeDir != "" && !Directory.Exists(SqlCodeDir)) { Directory.CreateDirectory(SqlCodeDir); }
            Txt.WriteFile(SqlCodePath, TextEncoding.UTF8, Compiled);
        }

        public static void ObjectSchemaToCSharpLinqToSqlCode(String CsCodePath, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            var RelationSchema = GetRelationSchema();
            var Compiled = RelationSchema.CompileToCSharpLinqToSql(DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            if (File.Exists(CsCodePath))
            {
                var Original = Txt.ReadFile(CsCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CsCodePath, Compiled);
        }

        public static void ObjectSchemaToCSharpLinqToEntitiesCode(String CsCodePath, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            var RelationSchema = GetRelationSchema();
            var Compiled = RelationSchema.CompileToCSharpLinqToEntities(DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            if (File.Exists(CsCodePath))
            {
                var Original = Txt.ReadFile(CsCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CsCodePath, Compiled);
        }

        public static void ObjectSchemaToCSharpDatabasePlainCode(String CsCodePath, String EntityNamespaceName)
        {
            var RelationSchema = GetRelationSchema();
            var Compiled = RelationSchema.CompileToCSharpPlain(EntityNamespaceName);
            if (File.Exists(CsCodePath))
            {
                var Original = Txt.ReadFile(CsCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CsCodePath, Compiled);
        }

        public static void ObjectSchemaToCppDatabasePlainCode(String CppCodePath, String EntityNamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCpp(EntityNamespaceName);
            if (File.Exists(CppCodePath))
            {
                var Original = Txt.ReadFile(CppCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CppCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CppCodePath, Compiled);
        }
    }
}
