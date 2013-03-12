//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.ExpressionManipulator <Visual C#>
//  Description: 表达式结构处理工具
//  Version:     2013.03.11.
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
using Firefly.Mapping;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using OS = Yuki.ObjectSchema;
using ES = Yuki.ExpressionSchema;
using Yuki.ExpressionSchema;
using Yuki.ObjectSchema;
using Yuki.ObjectSchema.CSharp;
using Yuki.ObjectSchema.Cpp;

namespace Yuki.ExpressionManipulator
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
                                esl.LoadType(f);
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            esl.LoadType(SchemaPath);
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
                        esl.AddImport(args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2b")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 2)
                    {
                        ExpressionSchemaToBinary(args.Take(args.Length - 1).ToArray(), args.Last());
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (opt.Name.ToLower() == "t2csbl")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ExpressionSchemaToCSharpBinaryLoaderCode(args[0], args[1]);
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
            Console.WriteLine(@"表达式结构处理工具");
            Console.WriteLine(@"Yuki.ExpressionManipulator，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于从表达式结构生成代码。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"ExpressionManipulator (/<Command>)*");
            Console.WriteLine(@"装载类型定义");
            Console.WriteLine(@"/loadtype:<ExpressionSchemaDir|ExpressionSchemaFile>");
            Console.WriteLine(@"增加命名空间引用");
            Console.WriteLine(@"/import:<NamespaceName>");
            Console.WriteLine(@"生成二进制程序集");
            Console.WriteLine(@"/t2b:<DataDir>*,<BinaryPath>");
            Console.WriteLine(@"生成C#二进制程序集装载类型");
            Console.WriteLine(@"/t2csbl:<CsCodePath>,<NamespaceName>");
            Console.WriteLine(@"ExpressionSchemaDir|ExpressionSchemaFile 表达式结构Tree文件(夹)路径。");
            Console.WriteLine(@"BinaryPath 二进制程序集文件路径。");
            Console.WriteLine(@"CsCodePath C#代码文件路径。");
            Console.WriteLine(@"NamespaceName 命名空间名称。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"ExpressionManipulator /loadtype:ModuleSchema /t2csbl:Src\Generated\Module.cs,Example.Module");
        }

        private static ES.ExpressionSchemaLoader esl = new ES.ExpressionSchemaLoader();
        private static ES.Schema es = null;
        private static ES.Schema GetExpressionSchema()
        {
            if (es != null) { return es; }
            es = esl.GetResult();
            es.Verify();
            return es;
        }
        private static void InvalidateSchema()
        {
            es = null;
        }

        public static void ExpressionSchemaToBinary(String[] DataDirs, String BinaryPath)
        {
            var ExpressionSchema = GetExpressionSchema();
            var eal = new ExpressionAssemblyLoader(ExpressionSchema);
            foreach (var DataDir in DataDirs)
            {
                foreach (var tf in Directory.EnumerateFiles(DataDir, "*.tree", SearchOption.AllDirectories))
                {
                    eal.LoadAssembly(tf);
                }
            }
            var a = eal.GetResult();
            var bs = BinarySerializerWithString.Create();
            Byte[] Compiled;
            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Write(a, ms);
                ms.Position = 0;
                Compiled = ms.Read((int)(ms.Length));
            }
            if (File.Exists(BinaryPath))
            {
                Byte[] Original;
                using (var fs = Streams.OpenReadable(BinaryPath))
                {
                    Original = fs.Read((int)(fs.Length));
                }
                if (Compiled.SequenceEqual(Original))
                {
                    return;
                }
            }
            var BinaryDir = FileNameHandling.GetFileDirectory(BinaryPath);
            if (BinaryDir != "" && !Directory.Exists(BinaryDir)) { Directory.CreateDirectory(BinaryDir); }
            using (var fs = Streams.CreateWritable(BinaryPath))
            {
                fs.Write(Compiled);
            }
        }

        public static void ExpressionSchemaToCSharpBinaryLoaderCode(String CsCodePath, String NamespaceName)
        {
            //var ExpressionSchema = GetExpressionSchema();
            //var Compiled = ExpressionSchema.CompileToCSharpPlain(NamespaceName, WithFirefly);
            //if (File.Exists(CsCodePath))
            //{
            //    var Original = Txt.ReadFile(CsCodePath);
            //    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
            //    {
            //        return;
            //    }
            //}
            //var Dir = FileNameHandling.GetFileDirectory(CsCodePath);
            //if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            //Txt.WriteFile(CsCodePath, Compiled);
        }
    }
}
