//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.SchemaManipulator <Visual C#>
//  Description: 对象类型结构处理工具
//  Version:     2013.12.08.
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
using Firefly.Mapping.XmlText;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Yuki.ObjectSchema;
using OS = Yuki.ObjectSchema;
using Yuki.ObjectSchema.VB;
using Yuki.ObjectSchema.CSharp;
using Yuki.ObjectSchema.CSharpBinary;
using Yuki.ObjectSchema.CSharpJson;
using Yuki.ObjectSchema.CSharpCompatible;
using Yuki.ObjectSchema.Java;
using Yuki.ObjectSchema.JavaBinary;
using Yuki.ObjectSchema.Cpp;
using Yuki.ObjectSchema.CppBinary;
using Yuki.ObjectSchema.ActionScript;
using Yuki.ObjectSchema.ActionScriptBinary;
using Yuki.ObjectSchema.ActionScriptJson;
using Yuki.ObjectSchema.Haxe;
using Yuki.ObjectSchema.HaxeJson;
using Yuki.ObjectSchema.Xhtml;

namespace Yuki.SchemaManipulator
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

            var xs = new XmlSerializer();
            foreach (var opt in CmdLine.Options)
            {
                var optNameLower = opt.Name.ToLower();
                if ((optNameLower == "?") || (optNameLower == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (optNameLower == "load")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var CookedObjectSchemaPath = args[0];
                        InvalidateSchema();
                        osl.LoadSchema(CookedObjectSchemaPath);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "save")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var CookedObjectSchemaPath = args[0];
                        var s = GetObjectSchema();
                        var x = xs.Write(s);
                        TreeFile.WriteFile(CookedObjectSchemaPath, x);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "loadtyperef")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        var ObjectSchemaPath = args[0];
                        if (Directory.Exists(ObjectSchemaPath))
                        {
                            foreach (var f in Directory.GetFiles(ObjectSchemaPath, "*.tree", SearchOption.AllDirectories).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                            {
                                InvalidateSchema();
                                osl.LoadTypeRef(f);
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            osl.LoadTypeRef(ObjectSchemaPath);
                        }
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
                        var ObjectSchemaPath = args[0];
                        if (Directory.Exists(ObjectSchemaPath))
                        {
                            foreach (var f in Directory.GetFiles(ObjectSchemaPath, "*.tree", SearchOption.AllDirectories).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                            {
                                InvalidateSchema();
                                osl.LoadType(f);
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            osl.LoadType(ObjectSchemaPath);
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
                        osl.AddImport(args[0]);
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
                    if (args.Length == 3)
                    {
                        TreeToBinary(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "b2t")
                {
                    var args = opt.Arguments;
                    if (args.Length == 3)
                    {
                        BinaryToTree(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2vb")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToVBCode(args[0], "", true);
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToVBCode(args[0], args[1], true);
                    }
                    else if (args.Length == 3)
                    {
                        ObjectSchemaToVBCode(args[0], args[1], Boolean.Parse(args[2]));
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2cs")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCSharpCode(args[0], "", true);
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpCode(args[0], args[1], true);
                    }
                    else if (args.Length == 3)
                    {
                        ObjectSchemaToCSharpCode(args[0], args[1], Boolean.Parse(args[2]));
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2csb")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCSharpBinaryCode(args[0], "", true);
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpBinaryCode(args[0], args[1], true);
                    }
                    else if (args.Length == 3)
                    {
                        ObjectSchemaToCSharpBinaryCode(args[0], args[1], Boolean.Parse(args[2]));
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2csj")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCSharpJsonCode(args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpJsonCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2csc")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToCSharpCompatibleCode(args[0], args[1], "");
                    }
                    else if (args.Length == 3)
                    {
                        ObjectSchemaToCSharpCompatibleCode(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2jv")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToJavaCode(args[0], args[1], "");
                    }
                    else if (args.Length == 3)
                    {
                        ObjectSchemaToJavaCode(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2jvb")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToJavaBinaryCode(args[0], args[1], "");
                    }
                    else if (args.Length == 3)
                    {
                        ObjectSchemaToJavaBinaryCode(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2cpp")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCppCode(args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCppCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2cppb")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToCppBinaryCode(args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCppBinaryCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2as")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToActionScriptCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2asb")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToActionScriptBinaryCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2asj")
                {
                    var args = opt.Arguments;
                    if (args.Length == 2)
                    {
                        ObjectSchemaToActionScriptJsonCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2hx")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToHaxeCode(args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToHaxeCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2hxj")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToHaxeJsonCode(args[0], "");
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToHaxeJsonCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2xhtml")
                {
                    var args = opt.Arguments;
                    if (args.Length == 3)
                    {
                        ObjectSchemaToXhtml(args[0], args[1], args[2]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "gencom")
                {
                    var args = opt.Arguments;
                    if (args.Length == 3)
                    {
                        GenerateCompatibilityTreeFile(args[0], args[1], args[2]);
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
            Console.WriteLine(@"Yuki.SchemaManipulator，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于从对象类型结构生成代码。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"SchemaManipulator (/<Command>)*");
            Console.WriteLine(@"装载类型定义和引用");
            Console.WriteLine(@"/load:<CookedObjectSchemaFile>");
            Console.WriteLine(@"保存类型定义和引用");
            Console.WriteLine(@"/save:<CookedObjectSchemaFile>");
            Console.WriteLine(@"装载类型引用");
            Console.WriteLine(@"/loadtyperef:<ObjectSchemaDir|ObjectSchemaFile>");
            Console.WriteLine(@"装载类型定义");
            Console.WriteLine(@"/loadtype:<ObjectSchemaDir|ObjectSchemaFile>");
            Console.WriteLine(@"增加命名空间引用");
            Console.WriteLine(@"/import:<NamespaceName>");
            Console.WriteLine(@"将Tree格式数据转化为二进制数据");
            Console.WriteLine(@"/t2b:<TreeFile>,<BinaryFile>,<MainType>");
            Console.WriteLine(@"将二进制数据转化为Tree格式数据");
            Console.WriteLine(@"/b2t:<BinaryFile>,<TreeFile>,<MainType>");
            Console.WriteLine(@"生成VB.Net类型");
            Console.WriteLine(@"/t2vb:<VbCodePath>[,<NamespaceName>[,<WithFirefly=true>]]");
            Console.WriteLine(@"生成C#类型");
            Console.WriteLine(@"/t2cs:<CsCodePath>[,<NamespaceName>[,<WithFirefly=true>]]");
            Console.WriteLine(@"生成C#二进制通讯类型");
            Console.WriteLine(@"/t2csb:<CsCodePath>[,<NamespaceName>[,<WithFirefly=true>]]");
            Console.WriteLine(@"生成C# JSON通讯类型");
            Console.WriteLine(@"/t2csj:<CsCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"生成C#通讯兼容类型");
            Console.WriteLine(@"/t2csc:<CsCodePath>,<ClassName>[,<NamespaceName>]");
            Console.WriteLine(@"生成Java类型");
            Console.WriteLine(@"/t2jv:<JavaCodePath>,<ClassName>[,<PackageName>]");
            Console.WriteLine(@"生成Java二进制类型");
            Console.WriteLine(@"/t2jvb:<JavaCodePath>,<ClassName>[,<PackageName>]");
            Console.WriteLine(@"生成C++2011类型");
            Console.WriteLine(@"/t2cpp:<CppCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"生成C++2011二进制通讯类型");
            Console.WriteLine(@"/t2cppb:<CppCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"生成ActionScript类型");
            Console.WriteLine(@"/t2as:<AsCodeDir>,<PackageName>");
            Console.WriteLine(@"生成ActionScript二进制通讯类型");
            Console.WriteLine(@"/t2asb:<AsCodeDir>,<PackageName>");
            Console.WriteLine(@"生成ActionScript JSON通讯类型");
            Console.WriteLine(@"/t2asj:<AsCodeDir>,<PackageName>");
            Console.WriteLine(@"生成Haxe类型");
            Console.WriteLine(@"/t2hx:<HaxeCodePath>,<PackageName>");
            Console.WriteLine(@"生成Haxe JSON通讯类型");
            Console.WriteLine(@"/t2hxj:<HaxeCodePath>,<PackageName>");
            Console.WriteLine(@"生成XHTML文档");
            Console.WriteLine(@"/t2xhtml:<XhtmlDir>,<Title>,<CopyrightText>");
            Console.WriteLine(@"生成兼容类型结构Tree文件");
            Console.WriteLine(@"/gencom:<CookedObjectSchemaFile>,<CompatibilityObjectSchemaFile>,<Version>");
            Console.WriteLine(@"CookedObjectSchemaFile 已编译过的对象类型结构Tree文件路径。");
            Console.WriteLine(@"ObjectSchemaDir|ObjectSchemaFile 对象类型结构Tree文件(夹)路径。");
            Console.WriteLine(@"TreeFile Tree文件路径。");
            Console.WriteLine(@"BinaryFile 二进制文件路径。");
            Console.WriteLine(@"MainType 主类型。");
            Console.WriteLine(@"NamespaceName C#文件中的命名空间名称。");
            Console.WriteLine(@"PackageName Java/ActionScript文件中的包名。");
            Console.WriteLine(@"ClassName Java文件中的类名。");
            Console.WriteLine(@"VbCodePath VB代码文件路径。");
            Console.WriteLine(@"CsCodePath C#代码文件路径。");
            Console.WriteLine(@"WithFirefly 是否使用Firefly库。");
            Console.WriteLine(@"CppCodePath C++代码文件路径。");
            Console.WriteLine(@"JavaCodePath Java代码文件路径。");
            Console.WriteLine(@"AsCodeDir ActionScript代码文件夹路径。");
            Console.WriteLine(@"HaxeCodePath Haxe代码文件路径。");
            Console.WriteLine(@"XhtmlDir XHTML文件夹路径。");
            Console.WriteLine(@"Title 标题。");
            Console.WriteLine(@"CopyrightText 版权文本。");
            Console.WriteLine(@"CompatibilityObjectSchemaFile 兼容用对象类型结构Tree文件。");
            Console.WriteLine(@"Version 兼容用对象类型结构Tree中类型所用版本号。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"SchemaManipulator /loadtype:Schema /t2cs:Src\Generated\Communication.cs,Communication");
        }

        private static ObjectSchemaLoader osl = new ObjectSchemaLoader();
        private static OS.Schema os = null;
        private static Assembly osa = null;
        private static TreeBinaryConverter tbc = null;
        private static OS.Schema GetObjectSchema()
        {
            if (os != null) { return os; }
            os = osl.GetResult();
            os.Verify();
            return os;
        }
        private static Assembly SchemaAssembly()
        {
            if (osa != null) { return osa; }
            var s = GetObjectSchema();
            var Code = s.CompileToCSharp();

            var cp = new CompilerParameters();
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(System.CodeDom.Compiler.CodeCompiler)).Location); //System.dll
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(System.Linq.Enumerable)).Location); //System.Core.dll
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(Firefly.N32)).Location); //Firefly.Core.dll
            cp.GenerateExecutable = false;
            cp.GenerateInMemory = true;
            var cr = (new Microsoft.CSharp.CSharpCodeProvider()).CompileAssemblyFromSource(cp, Code);
            if (cr.Errors.HasErrors)
            {
                var l = new List<String>();
                l.Add("CodeCompileFailed");
                foreach (var e in cr.Errors.Cast<CompilerError>())
                {
                    l.Add(e.ToString());
                }
                throw new InvalidOperationException(String.Join(Environment.NewLine, l.ToArray()));
            }
            osa = cr.CompiledAssembly;
            return osa;
        }
        private static TreeBinaryConverter TreeBinaryConverter()
        {
            if (tbc != null) { return tbc; }
            tbc = new TreeBinaryConverter();
            return tbc;
        }
        private static void InvalidateSchema()
        {
            os = null;
            osa = null;
            tbc = null;
        }

        public static void TreeToBinary(String TreePath, String BinaryPath, String MainType)
        {
            var TypeName = ObjectSchemaLoader.GetTypeFriendlyNameFromVersionedName(MainType);
            var a = SchemaAssembly();
            var t = a.GetType(TypeName);
            var tbc = TreeBinaryConverter();

            var Data = TreeFile.ReadFile(TreePath);
            var b = tbc.TreeToBinary(t, Data);
            var Dir = FileNameHandling.GetFileDirectory(BinaryPath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            using (var s = Streams.CreateWritable(BinaryPath))
            {
                s.Write(b);
            }
        }
        public static void BinaryToTree(String BinaryPath, String TreePath, String MainType)
        {
            var TypeName = ObjectSchemaLoader.GetTypeFriendlyNameFromVersionedName(MainType);
            var a = SchemaAssembly();
            var t = a.GetType(TypeName);
            var tbc = TreeBinaryConverter();

            Byte[] Data;
            using (var s = Streams.OpenReadable(BinaryPath))
            {
                Data = s.Read((int)(s.Length));
            }
            var x = tbc.BinaryToTree(t, Data);
            var Dir = FileNameHandling.GetFileDirectory(TreePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            TreeFile.WriteFile(TreePath, x);
        }

        public static void ObjectSchemaToVBCode(String VbCodePath, String NamespaceName, Boolean WithFirefly)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToVB(NamespaceName, WithFirefly);
            if (File.Exists(VbCodePath))
            {
                var Original = Txt.ReadFile(VbCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(VbCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(VbCodePath, Compiled);
        }

        public static void ObjectSchemaToCSharpCode(String CsCodePath, String NamespaceName, Boolean WithFirefly)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCSharp(NamespaceName, WithFirefly);
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

        public static void ObjectSchemaToCSharpBinaryCode(String CsCodePath, String NamespaceName, Boolean WithFirefly)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCSharpBinary(NamespaceName, WithFirefly);
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

        public static void ObjectSchemaToCSharpJsonCode(String CsCodePath, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCSharpJson(NamespaceName);
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

        public static void ObjectSchemaToCSharpCompatibleCode(String CsCodePath, String ClassName, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCSharpCompatible(NamespaceName, ClassName);
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

        public static void ObjectSchemaToJavaCode(String JavaCodePath, String ClassName, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToJava(ClassName, PackageName);
            if (File.Exists(JavaCodePath))
            {
                var Original = Txt.ReadFile(JavaCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(JavaCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(JavaCodePath, Compiled);
        }

        public static void ObjectSchemaToJavaBinaryCode(String JavaCodePath, String ClassName, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToJavaBinary(ClassName, PackageName);
            if (File.Exists(JavaCodePath))
            {
                var Original = Txt.ReadFile(JavaCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(JavaCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(JavaCodePath, Compiled);
        }

        public static void ObjectSchemaToCppCode(String CppCodePath, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCpp(NamespaceName);
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

        public static void ObjectSchemaToCppBinaryCode(String CppCodePath, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCppBinary(NamespaceName);
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

        public static void ObjectSchemaToActionScriptCode(String AsCodeDir, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToActionScript(PackageName);
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
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(AsCodePath, TextEncoding.UTF8, Compiled);
            }
        }

        public static void ObjectSchemaToActionScriptBinaryCode(String AsCodeDir, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToActionScriptBinary(PackageName);
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
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(AsCodePath, TextEncoding.UTF8, Compiled);
            }
        }

        public static void ObjectSchemaToActionScriptJsonCode(String AsCodeDir, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToActionScriptJson(PackageName);
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
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(AsCodePath, TextEncoding.UTF8, Compiled);
            }
        }

        public static void ObjectSchemaToHaxeCode(String HaxeCodePath, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToHaxe(NamespaceName);
            if (File.Exists(HaxeCodePath))
            {
                var Original = Txt.ReadFile(HaxeCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(HaxeCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(HaxeCodePath, Compiled);
        }

        public static void ObjectSchemaToHaxeJsonCode(String HaxeCodePath, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToHaxeJson(NamespaceName);
            if (File.Exists(HaxeCodePath))
            {
                var Original = Txt.ReadFile(HaxeCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(HaxeCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(HaxeCodePath, Compiled);
        }

        public static void ObjectSchemaToXhtml(String XhtmlDir, String Title, String CopyrightText)
        {
            var ObjectSchema = GetObjectSchema();
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
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(Path, TextEncoding.UTF8, Compiled);
            }
        }

        public static void GenerateCompatibilityTreeFile(String CookedObjectSchemaFile, String CompatibilityObjectSchemaFile, String Version)
        {
            var ObjectSchema = GetObjectSchema();
            var oosl = new ObjectSchemaLoader();
            oosl.LoadSchema(CookedObjectSchemaFile);
            var OldObjectSchema = oosl.GetResult();
            OldObjectSchema.Verify();

            var ObjectSchemaHash = ObjectSchema.GetSubSchema(ObjectSchema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { }).Hash();
            var OldObjectSchemaHash = OldObjectSchema.GetSubSchema(OldObjectSchema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { }).Hash();

            var Comment = ""
                + @"==========================================================================" + "\r\n"
                + @"" + "\r\n"
                + @"  Notice:      This file is automatically generated." + "\r\n"
                + @"               Please don't modify this file." + "\r\n"
                + @"" + "\r\n"
                + @"  SchemaHash: Head 0x" + ObjectSchemaHash.ToString("X16") + "\r\n"
                + @"              " + Version + " 0x" + OldObjectSchemaHash.ToString("X16") + "\r\n"
                + @"" + "\r\n"
                + @"==========================================================================" + "\r\n"
                ;

            var DiffGenerator = new ObjectSchemaDiffGenerator();
            var Result = DiffGenerator.Generate(ObjectSchema, OldObjectSchema).Patch.GetTypesVersioned(Version);
            if (ObjectSchema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Any())
            {
                Result = Result.GetSubSchema(Result.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToArray(), new TypeSpec[] { });
            }

            var osw = new ObjectSchemaWriter();
            var Compiled = osw.Write(Result.Types, Comment);

            if (File.Exists(CompatibilityObjectSchemaFile))
            {
                var Original = Txt.ReadFile(CompatibilityObjectSchemaFile);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(CompatibilityObjectSchemaFile);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(CompatibilityObjectSchemaFile, TextEncoding.UTF8, Compiled);
        }
    }
}
