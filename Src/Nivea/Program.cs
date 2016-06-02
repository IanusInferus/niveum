//==========================================================================
//
//  File:        Program.cs
//  Location:    Nivea <Visual C#>
//  Description: 模板语言运行时
//  Version:     2016.06.02.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Nivea.Template.Syntax;

namespace Nivea.CUI
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
                else if (optNameLower == "dumpsyn")
                {
                    if (opt.Arguments.Length == 2)
                    {
                        DumpSyntaxResult(opt.Arguments[0], opt.Arguments[1]);
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
            Console.WriteLine(@"模板语言运行时");
            Console.WriteLine(@"Nivea，按BSD许可证分发");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"本工具用于从类型结构和类型模板生成代码。");
            Console.WriteLine(@"");
            Console.WriteLine(@"用法:");
            Console.WriteLine(@"");
            Console.WriteLine(@"示例:");
            Console.WriteLine(@"Nivea /schemakind:ObjectSchema /template:CSharp /loadtype:Schema /p:Namespace=Communication,Output=Src\Generated\Communication.cs");
        }

        public static void DumpSyntaxResult(String InputDirectory, String OutputDirectory)
        {
            foreach (var FilePath in Directory.EnumerateFiles(InputDirectory, "*.tree", SearchOption.AllDirectories))
            {
                var RelativePath = FileNameHandling.GetRelativePath(FilePath, InputDirectory);
                var AbsolutePath = FileNameHandling.GetAbsolutePath(FilePath, InputDirectory);
                var FileName = FileNameHandling.GetFileName(FilePath);
                var FileContent = Txt.ReadFile(FilePath);
                var Text = TokenParser.BuildText(FileContent, AbsolutePath);
                var Result = FileParser.ParseFile(Text);
                var fd = new FileDumper();
                var Comment
                    = "==========================================================================" + "\r\n"
                    + "\r\n"
                    + "  SourceFile:  " + FileName + "\r\n"
                    + "\r\n"
                    + "==========================================================================";
                var f = fd.Dump(Result, Comment);
                var OutputPath = FileNameHandling.GetPath(FileNameHandling.GetPath(OutputDirectory, FileNameHandling.GetFileDirectory(RelativePath)), FileNameHandling.GetMainFileName(FileName) + ".syn.tree");
                var OutputDir = FileNameHandling.GetFileDirectory(OutputPath);
                if (!Directory.Exists(OutputDir)) { Directory.CreateDirectory(OutputDir); }
                TreeFile.WriteRaw(OutputPath, f);
            }
        }
    }
}
