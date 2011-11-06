//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.SchemaManipulator <Visual C#>
//  Description: 对象类型结构处理工具
//  Version:     2011.11.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Yuki.ObjectSchema;
using OS = Yuki.ObjectSchema;
using Yuki.ObjectSchema.CSharpCommunication;
using Yuki.ObjectSchema.ActionScriptCommunication;
using Yuki.ObjectSchema.CSharp;
using Yuki.RelationSchema;
using RS = Yuki.RelationSchema;
using Yuki.RelationSchema.SqlDatabase;
using Yuki.RelationSchema.DbmlDatabase;
using Yuki.RelationSchema.CSharpDatabase;
using Yuki.ObjectSchema.Xhtml;

namespace Yuki.SchemaManipulator
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

            var osl = new ObjectSchemaLoader();
            Func<OS.Schema> Schema = () =>
            {
                var s = osl.GetResult();
                s.Verify();
                return s;
            };

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
                        var ObjectSchemaPath = args[0];
                        if (Directory.Exists(ObjectSchemaPath))
                        {
                            foreach (var f in Directory.EnumerateFiles(ObjectSchemaPath, "*.tree", SearchOption.AllDirectories))
                            {
                                osl.LoadTypeRef(f);
                            }
                        }
                        else
                        {
                            osl.LoadTypeRef(ObjectSchemaPath);
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
                        var ObjectSchemaPath = args[0];
                        if (Directory.Exists(ObjectSchemaPath))
                        {
                            foreach (var f in Directory.EnumerateFiles(ObjectSchemaPath, "*.tree", SearchOption.AllDirectories))
                            {
                                osl.LoadType(f);
                            }
                        }
                        else
                        {
                            osl.LoadType(ObjectSchemaPath);
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
                        osl.AddImport(args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2cs")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCSharpCode(Schema(), args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpCode(Schema(), args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2csc")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCSharpCommunicationCode(Schema(), args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpCommunicationCode(Schema(), args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2asc")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToActionScriptCommunicationCode(Schema(), args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2sqld")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToSqlDatabaseCode(Schema(), args[0], args[1]);
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
                        ObjectSchemaToDbmlDatabaseCode(Schema(), args[0], args[1], args[2], args[3], args[4]);
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
                        ObjectSchemaToCSharpDatabaseCode(Schema(), args[0], args[1], args[2], args[3], args[4]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2xhtml")
                {
                    var args = opt.Arguments;
                    if (args.Length == 3)
                    {
                        ObjectSchemaToXhtml(Schema(), args[0], args[1], args[2]);
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
            Console.WriteLine(@"对象类型结构处理工具");
            Console.WriteLine("Yuki.SchemaManipulator，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于从对象类型结构生成代码。目前只支持C#代码生成。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"SchemaManipulator (/<Command>)*");
            Console.WriteLine(@"装载类型引用");
            Console.WriteLine(@"/loadtyperef:<ObjectSchemaDir|ObjectSchemaFile>");
            Console.WriteLine(@"装载类型定义");
            Console.WriteLine(@"/loadtype:<ObjectSchemaDir|ObjectSchemaFile>");
            Console.WriteLine(@"增加命名空间引用");
            Console.WriteLine(@"/import:<NamespaceName>");
            Console.WriteLine(@"生成C#类型");
            Console.WriteLine(@"/t2cs:<CsCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"生成C#通讯类型");
            Console.WriteLine(@"/t2csc:<CsCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"生成ActionScript通讯类型");
            Console.WriteLine(@"/t2asc:<AsCodeDir>,<PackageName>");
            Console.WriteLine(@"生成SQL数据库DROP和CREATE脚本");
            Console.WriteLine(@"/t2sqld:<SqlCodePath>,<DatabaseName>");
            Console.WriteLine(@"生成Dbml文件");
            Console.WriteLine(@"/t2dbml:<DbmlCodePath>,<DatabaseName>,<EntityNamespaceName>,<ContextNamespaceName>,<ContextClassName>");
            Console.WriteLine(@"生成C#数据库类型");
            Console.WriteLine(@"/t2csd:<CsCodePath>,<DatabaseName>,<EntityNamespaceName>,<ContextNamespaceName>,<ContextClassName>");
            Console.WriteLine(@"生成XHTML文档");
            Console.WriteLine(@"/t2xhtml:<XhtmlDir>,<Title>,<CopyrightText>");
            Console.WriteLine(@"ObjectSchemaDir|ObjectSchemaFile 对象类型结构Tree文件(夹)路径。");
            Console.WriteLine(@"CsCodePath C#代码文件路径。");
            Console.WriteLine(@"NamespaceName C#文件中的命名空间名称。");
            Console.WriteLine(@"AsCodeDir ActionScript代码文件夹路径。");
            Console.WriteLine(@"PackageName ActionScript文件中的包名。");
            Console.WriteLine(@"UseTryWrapper 是否使用try包装构造函数。");
            Console.WriteLine(@"SqlCodePath SQL代码文件路径。");
            Console.WriteLine(@"DatabaseName 数据库名。");
            Console.WriteLine(@"DbmlCodePath Dbml文件路径。");
            Console.WriteLine(@"EntityNamespaceName 实体命名空间名称。");
            Console.WriteLine(@"ContextNamespaceName 上下文命名空间名称。");
            Console.WriteLine(@"ContextClassName 上下文类名称。");
            Console.WriteLine(@"XhtmlDir XHTML文件夹路径。");
            Console.WriteLine(@"Title 标题。");
            Console.WriteLine(@"CopyrightText 版权文本。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"SchemaManipulator /t2csc:..\..\Schema\Communication,..\..\GameServer\Src\Schema\Communication.cs,Yuki.Communication");
        }

        public static void ObjectSchemaToCSharpCommunicationCode(OS.Schema ObjectSchema, String CsCodePath, String NamespaceName)
        {
            var Compiled = ObjectSchema.CompileToCSharpCommunication(NamespaceName);
            if (File.Exists(CsCodePath))
            {
                var Original = Txt.ReadFile(CsCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CsCodePath, Compiled);
        }

        public static void ObjectSchemaToActionScriptCommunicationCode(OS.Schema ObjectSchema, String AsCodeDir, String PackageName)
        {
            var CompiledFiles = ObjectSchema.CompileToActionScriptCommunication(PackageName);
            foreach (var f in CompiledFiles)
            {
                var Compiled = f.Content;
                var AsCodePath = FileNameHandling.GetPath(AsCodeDir, f.Path + ".as");
                if (File.Exists(AsCodePath))
                {
                    var Original = Txt.ReadFile(AsCodePath);
                    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                var Dir = FileNameHandling.GetFileDirectory(AsCodePath);
                if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(AsCodePath, TextEncoding.UTF8, Compiled);
            }
        }

        public static void ObjectSchemaToCSharpCode(OS.Schema ObjectSchema, String CsCodePath, String NamespaceName)
        {
            var Compiled = ObjectSchema.CompileToCSharp(NamespaceName);
            if (File.Exists(CsCodePath))
            {
                var Original = Txt.ReadFile(CsCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CsCodePath, Compiled);
        }

        public static void ObjectSchemaToSqlDatabaseCode(OS.Schema ObjectSchema, String SqlCodePath, String DatabaseName)
        {
            var Compiled = ObjectSchema.CompileToSqlDatabase(DatabaseName, true);
            if (File.Exists(SqlCodePath))
            {
                var Original = Txt.ReadFile(SqlCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var SqlCodeDir = FileNameHandling.GetFileDirectory(SqlCodePath);
            if (!Directory.Exists(SqlCodeDir)) { Directory.CreateDirectory(SqlCodeDir); }
            Txt.WriteFile(SqlCodePath, Compiled);
        }

        public static void ObjectSchemaToDbmlDatabaseCode(OS.Schema ObjectSchema, String SqlCodePath, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            var CompiledX = ObjectSchema.CompileToDbmlDatabase(DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
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
            if (!Directory.Exists(SqlCodeDir)) { Directory.CreateDirectory(SqlCodeDir); }
            Txt.WriteFile(SqlCodePath, TextEncoding.UTF8, Compiled);
        }

        public static void ObjectSchemaToCSharpDatabaseCode(OS.Schema ObjectSchema, String CsCodePath, String DatabaseName, String EntityNamespaceName, String ContextNamespaceName, String ContextClassName)
        {
            var Compiled = ObjectSchema.CompileToCSharpDatabase(DatabaseName, EntityNamespaceName, ContextNamespaceName, ContextClassName);
            if (File.Exists(CsCodePath))
            {
                var Original = Txt.ReadFile(CsCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CsCodePath, Compiled);
        }

        public static void ObjectSchemaToXhtml(OS.Schema ObjectSchema, String XhtmlDir, String Title, String CopyrightText)
        {
            var CompiledFiles = ObjectSchema.CompileToXhtml(Title, CopyrightText);
            foreach (var f in CompiledFiles)
            {
                var Compiled = f.Content;
                var Path = FileNameHandling.GetPath(XhtmlDir, f.Path);
                if (File.Exists(Path))
                {
                    var Original = Txt.ReadFile(Path);
                    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                var Dir = FileNameHandling.GetFileDirectory(Path);
                if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(Path, TextEncoding.UTF8, Compiled);
            }
        }
    }
}
