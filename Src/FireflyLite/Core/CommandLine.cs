using System;
using System.Collections.Generic;
using System.Linq;
using Firefly.TextEncoding;

namespace Firefly
{
    public sealed class CommandLine
    {
        private CommandLine() { }

        public class CommandLineOption
        {
            public string Name;
            public string[] Arguments;
        }

        public class CommandLineArguments
        {
            public string[] Arguments;
            public CommandLineOption[] Options;
        }

        private static string DescapeQuote(string s)
        {
            if (s.Length < 2) return s;
            if (s.StartsWith("\"") && s.EndsWith("\""))
            {
                return s.Substring(1, s.Length - 2).Replace("\"\"", "\"");
            }
            else
            {
                return s;
            }
        }

        private static List<string> SplitCmdLineWithSpace(string CmdLine)
        {
            var l = new List<string>();
            var a = new List<Char32>();
            var InQuote = false;
            foreach (var c in CmdLine.ToUTF32())
            {
                if (c == (Char32)' ' && !InQuote)
                {
                    if (a.Count == 0) continue;
                    l.Add(DescapeQuote(a.ToUTF16B()));
                    a.Clear();
                    continue;
                }
                if (c == (Char32)'"')
                {
                    InQuote = !InQuote;
                }
                a.Add(c);
            }
            if (a.Count != 0)
            {
                l.Add(DescapeQuote(a.ToUTF16B()));
            }
            return l;
        }

        private static List<string> SplitCmdLineWithChar(string CmdLine, Char32 Splitter)
        {
            var l = new List<string>();
            var a = new List<Char32>();
            var InQuote = false;
            foreach (var c in CmdLine.ToUTF32())
            {
                if (c == Splitter && !InQuote)
                {
                    l.Add(DescapeQuote(a.ToUTF16B()));
                    a.Clear();
                    continue;
                }
                if (c == (Char32)'"')
                {
                    InQuote = !InQuote;
                }
                a.Add(c);
            }
            l.Add(DescapeQuote(a.ToUTF16B()));
            return l;
        }

        public static CommandLineArguments ParseCmdLine(string CmdLine, bool SuppressFirst)
        {
            var argv = SplitCmdLineWithSpace(CmdLine).Skip(1).ToList();

            var Arguments = new List<string>();
            var Options = new List<CommandLineOption>();

            foreach (var arg in argv)
            {
                if (arg.StartsWith("/"))
                {
                    var OptionLine = arg.Substring(1);
                    string Name;
                    var OptionArguments = new List<string>();
                    var Index = OptionLine.IndexOf(":");
                    if (Index >= 0)
                    {
                        Name = DescapeQuote(OptionLine.Substring(0, Index));
                        var ParameterLine = OptionLine.Substring(Index + 1);
                        OptionArguments = SplitCmdLineWithChar(ParameterLine, (Char32)',');
                    }
                    else
                    {
                        Name = DescapeQuote(OptionLine);
                    }
                    Options.Add(new CommandLineOption { Name = Name, Arguments = OptionArguments.ToArray() });
                }
                else
                {
                    Arguments.Add(arg);
                }
            }

            return new CommandLineArguments { Arguments = Arguments.ToArray(), Options = Options.ToArray() };
        }

        public static CommandLineArguments GetCmdLine()
        {
            if (Environment.GetEnvironmentVariable("ComSpec") != "")
            {
                return ParseCmdLine(Environment.CommandLine, true);
            }
            else
            {
                var Args = Environment.GetCommandLineArgs();
                var l = new List<string>();
                l.Add("\"\"");
                foreach (var a in Args.Skip(1))
                {
                    if (a.StartsWith("//"))
                    {
                        l.Add("\"" + a.Substring(2).Replace("\"", "\"\"") + "\"");
                    }
                    else if (a.StartsWith("/"))
                    {
                        l.Add(a);
                    }
                    else if (a.Contains("\"") || a.Contains(" "))
                    {
                        l.Add("\"" + a.Replace("\"", "\"\"") + "\"");
                    }
                    else if (a == "")
                    {
                        l.Add("\"\"");
                    }
                    else
                    {
                        l.Add(a);
                    }
                }
                var CommandLineStr = string.Join(" ", l.ToArray());
                return ParseCmdLine(CommandLineStr, true);
            }
        }
    }
}
