//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 数据库示例程序
//  Version:     2012.06.28.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Reflection;
using Firefly;

namespace Database
{
    public static class Program
    {
        public static int Main(String[] args)
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
            DisplayTitle();

            var CmdLine = CommandLine.GetCmdLine();

            var UseLoadTest = false;
            var UsePerformanceTest = false;
            foreach (var opt in CmdLine.Options)
            {
                if ((opt.Name.ToLower() == "?") || (opt.Name.ToLower() == "help"))
                {
                    DisplayInfo();
                    return 0;
                }
                else if (opt.Name.ToLower() == "load")
                {
                    UseLoadTest = true;
                }
                else if (opt.Name.ToLower() == "perf")
                {
                    UsePerformanceTest = true;
                }
            }

            var argv = CmdLine.Arguments;
            if (argv.Length != 1)
            {
                DisplayInfo();
                return -1;
            }

            var ConnectionString = argv[0];

            var dam = new DataAccessManager(ConnectionString);
            if (UseLoadTest)
            {
                LoadTest.DoTest(dam);
            }
            else if (UsePerformanceTest)
            {
                PerformanceTest.DoTest(dam);
            }
            else
            {
                DisplayInfo();
                return -1;
            }

            return 0;
        }

        public static void DisplayTitle()
        {
            Console.WriteLine(@"数据库示例程序");
            Console.WriteLine(@"Author:      F.R.C.");
            Console.WriteLine(@"Copyright(C) Public Domain");
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"用法:");
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name + @" <ConnectionString> /load|/perf");
            Console.WriteLine(@"ConnectionString 数据库连接字符串");
            Console.WriteLine(@"/load 自动化负载测试");
            Console.WriteLine(@"/perf 自动化性能测试");
            Console.WriteLine(@"示例:");
            Console.WriteLine(Assembly.GetEntryAssembly().GetName().Name + @" """ + DataAccessManager.GetConnectionStringExample() + @""" /load");
        }
    }
}
