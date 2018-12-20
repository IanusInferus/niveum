//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.SchemaManipulator <Visual C#>
//  Description: 对象类型结构处理工具
//  Version:     2018.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Niveum.ObjectSchema;
using Niveum.ObjectSchema.CSharp;
using Niveum.ObjectSchema.CSharpBinary;
using Niveum.ObjectSchema.CSharpJson;
using Niveum.ObjectSchema.CSharpCompatible;
using Niveum.ObjectSchema.CSharpVersion;
using Niveum.ObjectSchema.Cpp;
using Niveum.ObjectSchema.CppBinary;
using Niveum.ObjectSchema.CppCompatible;
using Niveum.ObjectSchema.CppVersion;
using Niveum.ObjectSchema.Haxe;
using Niveum.ObjectSchema.HaxeJson;
using Niveum.ObjectSchema.Java;
using Niveum.ObjectSchema.JavaBinary;
using Niveum.ObjectSchema.Python;
using Niveum.ObjectSchema.VB;
using Niveum.ObjectSchema.Xhtml;
using Yuki.ObjectSchema.PythonBinary;
using OS = Niveum.ObjectSchema;

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
                        try
                        {
                            oslLegacy.LoadSchema(CookedObjectSchemaPath);
                        }
                        catch (Exception)
                        {
                        }
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
                        var ts = new TreeSerializer();
                        var t = ts.Write(s);
                        TreeFile.WriteDirect(CookedObjectSchemaPath, t);
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
                                try
                                {
                                    oslLegacy.LoadTypeRef(f);
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            osl.LoadTypeRef(ObjectSchemaPath);
                            try
                            {
                                oslLegacy.LoadTypeRef(ObjectSchemaPath);
                            }
                            catch (Exception)
                            {
                            }
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
                                try
                                {
                                    oslLegacy.LoadType(f);
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                        else
                        {
                            InvalidateSchema();
                            osl.LoadType(ObjectSchemaPath);
                            try
                            {
                                oslLegacy.LoadType(ObjectSchemaPath);
                            }
                            catch (Exception)
                            {
                            }
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
                        oslLegacy.AddImport(args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "async")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        InvalidateSchema();
                        LoadAsync(args[0]);
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
                    if (args.Length == 4)
                    {
                        ObjectSchemaToCSharpCompatibleCode(args[0], args[1], args[2], args[3]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2csv")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 2)
                    {
                        ObjectSchemaToCSharpVersionCode(args[0], args[1], args.Skip(2).ToList());
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
                        ObjectSchemaToJavaCode(args[0], args[1]);
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
                        ObjectSchemaToJavaBinaryCode(args[0], args[1]);
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
                        ObjectSchemaToCppBinaryCode(args[0], "", true, true);
                    }
                    else if (args.Length == 2)
                    {
                        ObjectSchemaToCppBinaryCode(args[0], args[1], true, true);
                    }
                    else if (args.Length == 4)
                    {
                        ObjectSchemaToCppBinaryCode(args[0], args[1], Boolean.Parse(args[2]), Boolean.Parse(args[3]));
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2cppc")
                {
                    var args = opt.Arguments;
                    if (args.Length == 4)
                    {
                        ObjectSchemaToCppCompatibleCode(args[0], args[1], args[2], args[3]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2cppv")
                {
                    var args = opt.Arguments;
                    if (args.Length >= 2)
                    {
                        ObjectSchemaToCppVersionCode(args[0], args[1], args.Skip(2).ToList());
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
                    if (args.Length == 2)
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
                    if (args.Length == 2)
                    {
                        ObjectSchemaToHaxeJsonCode(args[0], args[1]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2py")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToPythonCode(args[0]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "t2pyb")
                {
                    var args = opt.Arguments;
                    if (args.Length == 1)
                    {
                        ObjectSchemaToPythonBinaryCode(args[0]);
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
                    if (args.Length == 5)
                    {
                        GenerateCommunicationCompatibilityTreeFile(args[0], args[1], args[2], args[3], args[4]);
                    }
                    else
                    {
                        DisplayInfo();
                        return -1;
                    }
                }
                else if (optNameLower == "gentypecom")
                {
                    var args = opt.Arguments;
                    if (args.Length == 5)
                    {
                        GenerateTypeCompatibilityTreeFile(args[0], args[1], args[2], args[3], args[4]);
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
            Console.WriteLine(@"增加异步命令指定");
            Console.WriteLine(@"/async:<AsyncCommandListFile>");
            Console.WriteLine(@"指定所有命令为异步");
            Console.WriteLine(@"/async:*");
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
            Console.WriteLine(@"生成C#兼容类型");
            Console.WriteLine(@"/t2csc:<CsCodePath>,<NamespaceName>,<ImplementationNamespaceName>,<ImplementationClassName>");
            Console.WriteLine(@"生成C#版本类型");
            Console.WriteLine(@"/t2csv:<CsCodePath>,<NamespaceName>,<FullTypeName>*");
            Console.WriteLine(@"生成Java类型");
            Console.WriteLine(@"/t2jv:<JavaCodeDirPath>,<PackageName>");
            Console.WriteLine(@"生成Java二进制类型");
            Console.WriteLine(@"/t2jvb:<JavaCodeDirPath>,<PackageName>");
            Console.WriteLine(@"生成C++2011类型");
            Console.WriteLine(@"/t2cpp:<CppCodePath>[,<NamespaceName>]");
            Console.WriteLine(@"生成C++2011二进制通讯类型");
            Console.WriteLine(@"/t2cppb:<CppCodePath>[,<NamespaceName>[,<WithServer=true>,<WithClient=true>]]");
            Console.WriteLine(@"生成C++兼容类型");
            Console.WriteLine(@"/t2cppc:<CsCodePath>,<NamespaceName>,<ImplementationNamespaceName>,<ImplementationClassName>");
            Console.WriteLine(@"生成C++版本类型");
            Console.WriteLine(@"/t2cppv:<CsCodePath>,<NamespaceName>,<FullTypeName>*");
            Console.WriteLine(@"生成Haxe类型");
            Console.WriteLine(@"/t2hx:<HaxeCodeDirPath>,<PackageName>");
            Console.WriteLine(@"生成Haxe JSON通讯类型");
            Console.WriteLine(@"/t2hxj:<HaxeCodeDirPath>,<PackageName>");
            Console.WriteLine(@"生成Python类型");
            Console.WriteLine(@"/t2py:<PythonCodePath>");
            Console.WriteLine(@"生成Python二进制类型");
            Console.WriteLine(@"/t2pyb:<PythonCodePath>");
            Console.WriteLine(@"生成XHTML文档");
            Console.WriteLine(@"/t2xhtml:<XhtmlDir>,<Title>,<CopyrightText>");
            Console.WriteLine(@"生成通讯兼容类型结构Tree文件(当前加载的类型结构为Head，生成的兼容用对象类型结构为Old - New中的通讯命令 + Head中的类型)");
            Console.WriteLine(@"/gencom:<OldCookedObjectSchemaFile>,<OldVersion>,<NewCookedObjectSchemaFile>,<NewVersion>,<CompatibilityObjectSchemaFile>");
            Console.WriteLine(@"生成兼容类型结构Tree文件(当前加载的类型结构为Head，生成的兼容用对象类型结构为Old - New中的类型 + Head中的类型)");
            Console.WriteLine(@"/gentypecom:<OldCookedObjectSchemaFile>,<OldVersion>,<NewCookedObjectSchemaFile>,<NewVersion>,<CompatibilityObjectSchemaFile>");
            Console.WriteLine(@"CookedObjectSchemaFile 已编译过的对象类型结构Tree文件路径。");
            Console.WriteLine(@"ObjectSchemaDir|ObjectSchemaFile 对象类型结构Tree文件(夹)路径。");
            Console.WriteLine(@"AsyncCommandListFile 异步命令列表文件");
            Console.WriteLine(@"TreeFile Tree文件路径。");
            Console.WriteLine(@"BinaryFile 二进制文件路径。");
            Console.WriteLine(@"MainType 主类型。");
            Console.WriteLine(@"NamespaceName C#文件中的命名空间名称。");
            Console.WriteLine(@"FullTypeName 类型带命名空间但不带版本号名称。");
            Console.WriteLine(@"PackageName Java文件中的包名。");
            Console.WriteLine(@"ClassName Java文件中的类名。");
            Console.WriteLine(@"VbCodePath VB代码文件路径。");
            Console.WriteLine(@"CsCodePath C#代码文件路径。");
            Console.WriteLine(@"WithFirefly 是否使用Firefly库。");
            Console.WriteLine(@"ImplementationNamespaceName 实现所在的命名空间。");
            Console.WriteLine(@"ImplementationClassName 实现的类名。");
            Console.WriteLine(@"CppCodePath C++代码文件路径。");
            Console.WriteLine(@"WithServer 是否生成服务器代码。");
            Console.WriteLine(@"WithClient 是否生成客户端代码。");
            Console.WriteLine(@"JavaCodeDirPath Java代码文件目录路径。");
            Console.WriteLine(@"HaxeCodeDirPath Haxe代码文件目录路径。");
            Console.WriteLine(@"PythonCodePath Python代码文件路径。");
            Console.WriteLine(@"XhtmlDir XHTML文件夹路径。");
            Console.WriteLine(@"Title 标题。");
            Console.WriteLine(@"CopyrightText 版权文本。");
            Console.WriteLine(@"OldCookedObjectSchemaFile 旧的已编译过的对象类型结构Tree文件路径。");
            Console.WriteLine(@"OldVersion 旧类型结构Tree中类型所用版本号。");
            Console.WriteLine(@"NewCookedObjectSchemaFile 新的已编译过的对象类型结构Tree文件路径。");
            Console.WriteLine(@"NewVersion 新类型结构Tree中类型所用版本号。");
            Console.WriteLine(@"CompatibilityObjectSchemaFile 兼容用对象类型结构Tree文件。");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"SchemaManipulator /loadtype:Schema /t2cs:Src\Generated\Communication.cs,Communication");
        }

        private static ObjectSchemaLoader osl = new ObjectSchemaLoader();
        private static OS.ObjectSchemaLoaderResult oslr = null;
        private static Yuki.ObjectSchema.ObjectSchemaLoader oslLegacy = new Yuki.ObjectSchema.ObjectSchemaLoader();
        private static Yuki.ObjectSchema.ObjectSchemaLoaderResult oslrLegacy = null;
        private static Assembly osa = null;
        private static TreeBinaryConverter tbc = null;
        private static HashSet<String> AsyncCommands = new HashSet<String>();
        private static bool AsyncAll = false;
        private static OS.ObjectSchemaLoaderResult GetObjectSchemaLoaderResult()
        {
            if (oslr != null) { return oslr; }
            oslr = osl.GetResult();
            foreach (var t in oslr.Schema.Types)
            {
                if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    if (AsyncAll || AsyncCommands.Contains(cc.FullName()))
                    {
                        if (!cc.Attributes.Any(a => a.Key == "Async"))
                        {
                            cc.Attributes.Add(new KeyValuePair<String, List<String>>("Async", new List<String> { }));
                        }
                    }
                }
            }
            return oslr;
        }
        private static Yuki.ObjectSchema.ObjectSchemaLoaderResult GetObjectSchemaLoaderResultLegacy()
        {
            if (oslrLegacy != null) { return oslrLegacy; }
            oslrLegacy = oslLegacy.GetResult();
            foreach (var t in oslrLegacy.Schema.Types)
            {
                if (t.OnClientCommand)
                {
                    var cc = t.ClientCommand;
                    if (AsyncAll || AsyncCommands.Contains(cc.Name))
                    {
                        if (!cc.Attributes.Any(a => a.Key == "Async"))
                        {
                            cc.Attributes.Add(new KeyValuePair<String, List<String>>("Async", new List<String> { }));
                        }
                    }
                }
            }
            return oslrLegacy;
        }
        private static OS.Schema GetObjectSchema()
        {
            return GetObjectSchemaLoaderResult().Schema;
        }
        private static Yuki.ObjectSchema.Schema GetObjectSchemaLegacy()
        {
            return GetObjectSchemaLoaderResultLegacy().Schema;
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
            oslr = null;
            osa = null;
            tbc = null;
        }
        private static void LoadAsync(String AsyncCommandListFilePath)
        {
            if (AsyncCommandListFilePath == "*")
            {
                AsyncAll = true;
                return;
            }
            var f = Txt.ReadFile(AsyncCommandListFilePath);
            var Commands = f.UnifyNewLineToLf().Split('\n');
            foreach (var c in Commands)
            {
                if (c == "") { continue; }
                if (!AsyncCommands.Contains(c))
                {
                    AsyncCommands.Add(c);
                }
            }
        }

        public static void TreeToBinary(String TreePath, String BinaryPath, String MainType)
        {
            var TypeName = ObjectSchemaExtensions.GetDotNetFullNameFromVersionedName(MainType);
            var a = SchemaAssembly();
            var t = a.GetType(TypeName);
            if (t == null) { throw new InvalidOperationException("TypeNotExist: " + TypeName); }
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
            var TypeName = ObjectSchemaExtensions.GetDotNetFullNameFromVersionedName(MainType);
            var a = SchemaAssembly();
            var t = a.GetType(TypeName);
            if (t == null) { throw new InvalidOperationException("TypeNotExist: " + TypeName); }
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

        public static void ObjectSchemaToCSharpCompatibleCode(String CsCodePath, String NamespaceName, String ImplementationNamespaceName, String ImplementationClassName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCSharpCompatible(NamespaceName, ImplementationNamespaceName, ImplementationClassName);
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

        public static void ObjectSchemaToCSharpVersionCode(String CsCodePath, String NamespaceName, IEnumerable<String> TypeNames)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCSharpVersion(NamespaceName, TypeNames);
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

        public static void ObjectSchemaToJavaCode(String JavaCodeDirPath, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToJava(PackageName);
            foreach (var f in CompiledFiles)
            {
                var FilePath = FileNameHandling.GetPath(JavaCodeDirPath, f.Key.Replace('/', Path.DirectorySeparatorChar));
                var Compiled = f.Value;
                if (File.Exists(FilePath))
                {
                    var Original = Txt.ReadFile(FilePath);
                    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                var Dir = FileNameHandling.GetFileDirectory(FilePath);
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(FilePath, Compiled);
            }
        }

        public static void ObjectSchemaToJavaBinaryCode(String JavaCodeDirPath, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToJavaBinary(PackageName);
            foreach (var f in CompiledFiles)
            {
                var FilePath = FileNameHandling.GetPath(JavaCodeDirPath, f.Key.Replace('/', Path.DirectorySeparatorChar));
                var Compiled = f.Value;
                if (File.Exists(FilePath))
                {
                    var Original = Txt.ReadFile(FilePath);
                    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                var Dir = FileNameHandling.GetFileDirectory(FilePath);
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(FilePath, Compiled);
            }
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

        public static void ObjectSchemaToCppBinaryCode(String CppCodePath, String NamespaceName, Boolean WithServer, Boolean WithClient)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCppBinary(NamespaceName, WithServer, WithClient);
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

        public static void ObjectSchemaToCppCompatibleCode(String CppCodePath, String NamespaceName, String ImplementationNamespaceName, String ImplementationClassName)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCppCompatible(NamespaceName, ImplementationNamespaceName, ImplementationClassName);
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

        public static void ObjectSchemaToCppVersionCode(String CppCodePath, String NamespaceName, IEnumerable<String> TypeNames)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToCppVersion(NamespaceName, TypeNames);
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

        public static void ObjectSchemaToHaxeCode(String HaxeCodeDirPath, String PackageName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToHaxe(PackageName);
            foreach (var f in CompiledFiles)
            {
                var FilePath = FileNameHandling.GetPath(HaxeCodeDirPath, f.Key.Replace('/', Path.DirectorySeparatorChar));
                var Compiled = f.Value;
                if (File.Exists(FilePath))
                {
                    var Original = Txt.ReadFile(FilePath);
                    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                var Dir = FileNameHandling.GetFileDirectory(FilePath);
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(FilePath, Compiled);
            }
        }

        public static void ObjectSchemaToHaxeJsonCode(String HaxeCodeDirPath, String NamespaceName)
        {
            var ObjectSchema = GetObjectSchema();
            var CompiledFiles = ObjectSchema.CompileToHaxeJson(NamespaceName);
            foreach (var f in CompiledFiles)
            {
                var FilePath = FileNameHandling.GetPath(HaxeCodeDirPath, f.Key.Replace('/', Path.DirectorySeparatorChar));
                var Compiled = f.Value;
                if (File.Exists(FilePath))
                {
                    var Original = Txt.ReadFile(FilePath);
                    if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                var Dir = FileNameHandling.GetFileDirectory(FilePath);
                if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
                Txt.WriteFile(FilePath, Compiled);
            }
        }

        public static void ObjectSchemaToPythonCode(String PythonCodePath)
        {
            var ObjectSchema = GetObjectSchema();
            var Compiled = ObjectSchema.CompileToPython();
            if (File.Exists(PythonCodePath))
            {
                var Original = Txt.ReadFile(PythonCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(PythonCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(PythonCodePath, Compiled);
        }

        public static void ObjectSchemaToPythonBinaryCode(String PythonCodePath)
        {
            var ObjectSchema = GetObjectSchemaLegacy();
            var Compiled = ObjectSchema.CompileToPythonBinary();
            if (File.Exists(PythonCodePath))
            {
                var Original = Txt.ReadFile(PythonCodePath);
                if (String.Equals(Compiled, Original, StringComparison.Ordinal))
                {
                    return;
                }
            }
            var Dir = FileNameHandling.GetFileDirectory(PythonCodePath);
            if (Dir != "" && !Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); }
            Txt.WriteFile(PythonCodePath, Compiled);
        }

        public static void ObjectSchemaToXhtml(String XhtmlDir, String Title, String CopyrightText)
        {
            var oslr = GetObjectSchemaLoaderResult();
            var CompiledFiles = oslr.CompileToXhtml(Title, CopyrightText);
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

        public static void GenerateTypeCompatibilityTreeFile(String OldCookedObjectSchemaFile, String OldVersion, String NewCookedObjectSchemaFile, String NewVersion, String CompatibilityObjectSchemaFile)
        {
            var HeadObjectSchema = GetObjectSchema();
            var oosl = new ObjectSchemaLoader();
            oosl.LoadSchema(OldCookedObjectSchemaFile);
            var OldObjectSchemaLoaderResult = oosl.GetResult();
            var OldObjectSchema = OldObjectSchemaLoaderResult.Schema;
            var nosl = new ObjectSchemaLoader();
            nosl.LoadSchema(NewCookedObjectSchemaFile);
            var NewObjectSchemaLoaderResult = nosl.GetResult();
            var NewObjectSchema = NewObjectSchemaLoaderResult.Schema;

            var HeadSchema = HeadObjectSchema.GetSubSchema(HeadObjectSchema.Types.Where(t => t.Version() == ""), new TypeSpec[] { });
            var OldSchema = OldObjectSchema.GetSubSchema(OldObjectSchema.Types.Where(t => t.Version() == ""), new TypeSpec[] { });
            var NewSchema = NewObjectSchema.GetSubSchema(NewObjectSchema.Types.Where(t => t.Version() == ""), new TypeSpec[] { });

            var Comment = ""
                + @"==========================================================================" + "\r\n"
                + @"" + "\r\n"
                + @"  Notice:      This file is automatically generated." + "\r\n"
                + @"               Please don't modify this file." + "\r\n"
                + @"" + "\r\n"
                + @"  SchemaHash: Head 0x" + HeadSchema.GetNonattributed().Hash().ToString("X16") + "\r\n"
                + @"              " + NewVersion + " 0x" + NewSchema.GetNonattributed().Hash().ToString("X16") + "\r\n"
                + @"              " + OldVersion + " 0x" + OldSchema.GetNonattributed().Hash().ToString("X16") + "\r\n"
                + @"" + "\r\n"
                + @"==========================================================================" + "\r\n"
                ;

            var DiffGenerator = new ObjectSchemaDiffGenerator();
            var OldNewDiff = DiffGenerator.Generate(NewSchema, OldSchema);
            var ChangedTypes = new HashSet<String>(OldNewDiff.Patch.Types.Where(t => t.Version() == "").Select(t => t.VersionedName()), StringComparer.OrdinalIgnoreCase);
            var Patch = DiffGenerator.Generate(HeadObjectSchema, OldSchema.GetSubSchema(OldSchema.Types.Where(t => ChangedTypes.Contains(t.VersionedName())).ToList(), new List<TypeSpec> { })).Patch;
            var Result = Patch.GetTypesVersioned(OldVersion);

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

        public static void GenerateCommunicationCompatibilityTreeFile(String OldCookedObjectSchemaFile, String OldVersion, String NewCookedObjectSchemaFile, String NewVersion, String CompatibilityObjectSchemaFile)
        {
            var HeadObjectSchema = GetObjectSchema();
            var oosl = new ObjectSchemaLoader();
            oosl.LoadSchema(OldCookedObjectSchemaFile);
            var OldObjectSchemaLoaderResult = oosl.GetResult();
            var OldObjectSchema = OldObjectSchemaLoaderResult.Schema;
            var nosl = new ObjectSchemaLoader();
            nosl.LoadSchema(NewCookedObjectSchemaFile);
            var NewObjectSchemaLoaderResult = nosl.GetResult();
            var NewObjectSchema = NewObjectSchemaLoaderResult.Schema;

            var HeadCommandSchema = HeadObjectSchema.GetSubSchema(HeadObjectSchema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { });
            var OldCommandSchema = OldObjectSchema.GetSubSchema(OldObjectSchema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { });
            var NewCommandSchema = NewObjectSchema.GetSubSchema(NewObjectSchema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { });

            var Comment = ""
                + @"==========================================================================" + "\r\n"
                + @"" + "\r\n"
                + @"  Notice:      This file is automatically generated." + "\r\n"
                + @"               Please don't modify this file." + "\r\n"
                + @"" + "\r\n"
                + @"  SchemaHash: Head 0x" + HeadCommandSchema.GetNonattributed().Hash().ToString("X16") + "\r\n"
                + @"              " + NewVersion + " 0x" + NewCommandSchema.GetNonattributed().Hash().ToString("X16") + "\r\n"
                + @"              " + OldVersion + " 0x" + OldCommandSchema.GetNonattributed().Hash().ToString("X16") + "\r\n"
                + @"" + "\r\n"
                + @"==========================================================================" + "\r\n"
                ;

            var DiffGenerator = new ObjectSchemaDiffGenerator();
            var OldNewDiff = DiffGenerator.Generate(NewCommandSchema, OldCommandSchema);
            var ChangedCommands = new HashSet<String>(OldNewDiff.Patch.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == "").Select(t => t.VersionedName()), StringComparer.OrdinalIgnoreCase);
            var Patch = DiffGenerator.Generate(HeadObjectSchema, OldCommandSchema.GetSubSchema(OldCommandSchema.Types.Where(t => ChangedCommands.Contains(t.VersionedName())).ToList(), new List<TypeSpec> { })).Patch;
            var Result = Patch.GetTypesVersioned(OldVersion);

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
