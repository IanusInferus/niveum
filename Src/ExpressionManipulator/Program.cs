//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.ExpressionManipulator <Visual C#>
//  Description: 表达式结构处理工具
//  Version:     2022.01.25.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

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
using OS = Niveum.ObjectSchema;
using ES = Niveum.ExpressionSchema;
using Niveum.ObjectSchema;
using Niveum.ExpressionSchema;
using Niveum.ExpressionSchema.CSharpBinaryLoader;
using Niveum.ExpressionSchema.CppBinaryLoader;
using Niveum.ExpressionSchema.CSource;
using Niveum.ExpressionSchema.RV64Asm;

namespace Niveum.ExpressionManipulator
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
                var optNameLower = opt.Name.ToLower();
                if ((optNameLower == "?") || (optNameLower == "help"))
                {
                    DisplayInfo();
                    return 0;
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
                else if (optNameLower == "import")
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
                else if (optNameLower == "t2b")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 2)
                    {
                        ExpressionSchemaToBinary(args.Take(args.Length - 1).ToList(), args.Last());
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2csbl")
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
                else if (optNameLower == "t2cppbl")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ExpressionSchemaToCppBinaryLoaderCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2c")
                {
                    var args = opt.Arguments;
                    if (args.Length == 4)
                    {
                        ExpressionSchemaToCSourceCode(args[0], args[1], args[2], args[3]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2rv64")
                {
                    var args = opt.Arguments;
                    if (args.Length == 4)
                    {
                        ExpressionSchemaToRV64Code(args[0], args[1], args[2], args[3]);
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
            Console.WriteLine(@"表达式结构处理工具");
            Console.WriteLine(@"Niveum.ExpressionManipulator，按BSD许可证分发");
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
            Console.WriteLine(@"生成C++二进制程序集装载类型");
            Console.WriteLine(@"/t2cppbl:<CppCodePath>,<NamespaceName>");
            Console.WriteLine(@"生成C源代码");
            Console.WriteLine(@"/t2c:<BinaryPath>,<CHeaderPath>,<CSourcePath>,<NamespaceName>");
            Console.WriteLine(@"生成RISC-V(RV64G-LP64D)汇编代码");
            Console.WriteLine(@"/t2rv64:<BinaryPath>,<CHeaderPath>,<AssemblyPath>,<NamespaceName>");
            Console.WriteLine(@"ExpressionSchemaDir|ExpressionSchemaFile 表达式结构Tree文件(夹)路径。");
            Console.WriteLine(@"BinaryPath 二进制程序集文件路径。");
            Console.WriteLine(@"CsCodePath C#代码文件路径。");
            Console.WriteLine(@"CppCodePath C++代码文件路径。");
            Console.WriteLine(@"NamespaceName 命名空间名称。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"ExpressionManipulator /loadtype:ModuleSchema /t2csbl:Src\Generated\Module.cs,Example.Module");
        }

        private static ES.ExpressionSchemaLoader esl = new ES.ExpressionSchemaLoader();
        private static ES.Schema? es = null;
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

        public static void ExpressionSchemaToBinary(List<String> DataDirs, String BinaryPath)
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
            var ExpressionSchema = GetExpressionSchema();
            var Compiled = ExpressionSchema.CompileToCSharpBinaryLoader(NamespaceName);
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

        public static void ExpressionSchemaToCppBinaryLoaderCode(String CppCodePath, String NamespaceName)
        {
            var ExpressionSchema = GetExpressionSchema();
            var Compiled = ExpressionSchema.CompileToCppBinaryLoader(NamespaceName);
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

        public static void ExpressionSchemaToCSourceCode(String BinaryPath, String CHeaderPath, String CSourcePath, String NamespaceName)
        {
            var ExpressionSchema = GetExpressionSchema();
            Niveum.ExpressionSchema.Assembly a;
            var bs = BinarySerializerWithString.Create();
            using (var fs = Streams.OpenReadable(BinaryPath))
            {
                a = bs.Read<Niveum.ExpressionSchema.Assembly>(fs);
            }

            var HeaderCompiled = ExpressionSchema.CompileToCHeader(NamespaceName);
            var SkipHeader = false;
            if (File.Exists(CHeaderPath))
            {
                var Original = Txt.ReadFile(CHeaderPath);
                if (String.Equals(HeaderCompiled, Original, StringComparison.Ordinal))
                {
                    SkipHeader = true;
                }
            }
            if (!SkipHeader)
            {
                var HeaderDir = FileNameHandling.GetFileDirectory(CHeaderPath);
                if (HeaderDir != "" && !Directory.Exists(HeaderDir)) { Directory.CreateDirectory(HeaderDir); }
                Txt.WriteFile(CHeaderPath, HeaderCompiled);
            }

            var SourceCompiled = ExpressionSchema.CompileToCSource(NamespaceName, a);
            var SkipSource = false;
            if (File.Exists(CSourcePath))
            {
                var Original = Txt.ReadFile(CSourcePath);
                if (String.Equals(SourceCompiled, Original, StringComparison.Ordinal))
                {
                    SkipSource = true;
                }
            }
            if (!SkipSource)
            {
                var SourceDir = FileNameHandling.GetFileDirectory(CSourcePath);
                if (SourceDir != "" && !Directory.Exists(SourceDir)) { Directory.CreateDirectory(SourceDir); }
                Txt.WriteFile(CSourcePath, SourceCompiled);
            }
        }

        public static void ExpressionSchemaToRV64Code(String BinaryPath, String CHeaderPath, String AssemblyPath, String NamespaceName)
        {
            var ExpressionSchema = GetExpressionSchema();
            Niveum.ExpressionSchema.Assembly a;
            var bs = BinarySerializerWithString.Create();
            using (var fs = Streams.OpenReadable(BinaryPath))
            {
                a = bs.Read<Niveum.ExpressionSchema.Assembly>(fs);
            }

            var HeaderCompiled = ExpressionSchema.CompileToRV64CHeader(NamespaceName);
            var SkipHeader = false;
            if (File.Exists(CHeaderPath))
            {
                var Original = Txt.ReadFile(CHeaderPath);
                if (String.Equals(HeaderCompiled, Original, StringComparison.Ordinal))
                {
                    SkipHeader = true;
                }
            }
            if (!SkipHeader)
            {
                var HeaderDir = FileNameHandling.GetFileDirectory(CHeaderPath);
                if (HeaderDir != "" && !Directory.Exists(HeaderDir)) { Directory.CreateDirectory(HeaderDir); }
                Txt.WriteFile(CHeaderPath, TextEncoding.UTF8, HeaderCompiled, false);
            }

            var AssemblyCompiled = ExpressionSchema.CompileToRV64Assembly(NamespaceName, a);
            var SkipAssembly = false;
            if (File.Exists(AssemblyPath))
            {
                var Original = Txt.ReadFile(AssemblyPath);
                if (String.Equals(AssemblyCompiled, Original, StringComparison.Ordinal))
                {
                    SkipAssembly = true;
                }
            }
            if (!SkipAssembly)
            {
                var SourceDir = FileNameHandling.GetFileDirectory(AssemblyPath);
                if (SourceDir != "" && !Directory.Exists(SourceDir)) { Directory.CreateDirectory(SourceDir); }
                Txt.WriteFile(AssemblyPath, TextEncoding.UTF8, AssemblyCompiled, false);
            }
        }
    }
}
