//==========================================================================
//
//  File:        Program.cs
//  Location:    Yuki.Examples <Visual C#>
//  Description: 表达式计算工具
//  Version:     2013.03.12.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaProgramming;
using Firefly.Mapping.Binary;
using Firefly.Texting.TreeFormat.Syntax;
using Firefly.Streaming;
using Yuki.ObjectSchema;
using Yuki.ExpressionSchema;
using Yuki.Expression;

namespace DataConv
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
            var CmdLine = CommandLine.GetCmdLine();
            var argv = CmdLine.Arguments;

            if (CmdLine.Arguments.Length != 0)
            {
                DisplayInfo();
                return -1;
            }

            if (CmdLine.Options.Length != 0)
            {
                DisplayInfo();
                return -1;
            }

            Test();

            return 0;
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"表达式计算工具");
            Console.WriteLine(@"ExprCalc，Public Domain");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
        }

        public class VariableContext : IVariableProvider
        {
            public Dictionary<String, Object> Dict = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
            public VariableContext()
            {
            }

            public PrimitiveType[][] GetOverloads(String Name)
            {
                if (!Dict.ContainsKey(Name))
                {
                    return new PrimitiveType[][] { };
                }
                var t = Dict[Name].GetType();
                if (t == typeof(Boolean))
                {
                    return new PrimitiveType[][] { new PrimitiveType[] { PrimitiveType.Boolean } };
                }
                if (t == typeof(int))
                {
                    return new PrimitiveType[][] { new PrimitiveType[] { PrimitiveType.Int } };
                }
                if (t == typeof(double))
                {
                    return new PrimitiveType[][] { new PrimitiveType[] { PrimitiveType.Real } };
                }
                return new PrimitiveType[][] { };
            }

            public PrimitiveType[] GetMatched(String Name, PrimitiveType[] ParameterTypes)
            {
                if (!Dict.ContainsKey(Name))
                {
                    return new PrimitiveType[] { };
                }
                if (ParameterTypes.Length != 0)
                {
                    return new PrimitiveType[] { };
                }
                var t = Dict[Name].GetType();
                if (t == typeof(Boolean))
                {
                    return new PrimitiveType[] { PrimitiveType.Boolean };
                }
                if (t == typeof(int))
                {
                    return new PrimitiveType[] { PrimitiveType.Int };
                }
                if (t == typeof(double))
                {
                    return new PrimitiveType[] { PrimitiveType.Real };
                }
                return new PrimitiveType[] { };
            }

            public Delegate[] GetValue<TVariableContext>(String Name, PrimitiveType[] ParameterTypes, Delegate[] Parameters)
            {
                var ReturnTypes = GetMatched(Name, ParameterTypes);
                if (ReturnTypes.Length == 1)
                {
                    var t = ReturnTypes.Single();
                    var v = Dict[Name];
                    if (t == PrimitiveType.Boolean)
                    {
                        return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => (Boolean)(v)) };
                    }
                    else if (t == PrimitiveType.Int)
                    {
                        return new Delegate[] { (Func<TVariableContext, int>)(vc => (int)(v)) };
                    }
                    else if (t == PrimitiveType.Real)
                    {
                        return new Delegate[] { (Func<TVariableContext, double>)(vc => (double)(v)) };
                    }
                    else
                    {
                        return new Delegate[] { };
                    }
                }
                else
                {
                    return new Delegate[] { };
                }
            }
        }

        public static void Test()
        {
            Assembly a;
            var bs = BinarySerializerWithString.Create();
            using (var s = Streams.OpenReadable("Assembly.bin"))
            {
                a = bs.Read<Assembly>(s);
            }

            var vc = new VariableContext();

            TestInteractive(vc);
        }

        private static Object Evaluate(VariableContext vc, String s)
        {
            var p = new VariableProviderCombiner(vc, new ExpressionRuntimeProvider());
            var r = ExpressionParser.ParseExpr(p, s);
            var d = ExpressionEvaluator<VariableContext>.Compile(p, r).AdaptFunction<VariableContext, Object>();
            var o = d(vc);
            return o;
        }

        private static String ToString(Object o)
        {
            if (o.GetType() == typeof(Boolean))
            {
                var b = (Boolean)(o);
                if (b)
                {
                    return "true";
                }
                else
                {
                    return "false";
                }
            }
            return o.ToString();
        }

        private static Regex rAssignment = new Regex(@"^\s*(?<Identifier>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<Expr>.*)$", RegexOptions.ExplicitCapture);
        public static void TestInteractive(VariableContext vc)
        {
            while (true)
            {
                Console.Write("> ");
                var OutputStart = "> ".Length;
                var Line = Console.ReadLine();
                if (Line == "exit")
                {
                    break;
                }
                try
                {
                    {
                        var m = rAssignment.Match(Line);
                        if (m.Success)
                        {
                            var Identifier = m.Result("${Identifier}");
                            var Expr = m.Result("${Expr}");
                            OutputStart += Line.Length - Expr.Length;

                            var o = Evaluate(vc, Expr);
                            if (vc.Dict.ContainsKey(Identifier)) { vc.Dict.Remove(Identifier); }
                            vc.Dict.Add(Identifier, o);
                            Console.WriteLine(ToString(o));
                            continue;
                        }
                    }
                    {
                        var o = Evaluate(vc, Line);
                        Console.WriteLine(ToString(o));
                    }
                }
                catch (InvalidSyntaxException ex)
                {
                    if (ex.Range != null)
                    {
                        var r = ex.Range.Range;
                        if (r.OnHasValue)
                        {
                            var Start = r.Value.Start.CharIndex;
                            var End = r.Value.End.CharIndex;
                            var s = new String(' ', OutputStart + Start) + new String('~', End - Start);
                            Console.WriteLine(s);
                        }
                    }
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ExceptionInfo.GetExceptionInfo(ex));
                }
            }
        }
    }
}
