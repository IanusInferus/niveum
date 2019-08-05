//==========================================================================
//
//  File:        Program.cs
//  Location:    Niveum.Examples <Visual C#>
//  Description: 表达式计算工具
//  Version:     2019.08.06.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.MetaProgramming;
using Firefly.Mapping.Binary;
using Firefly.Texting.TreeFormat.Syntax;
using Firefly.Streaming;
using Niveum.ObjectSchema;
using Niveum.ExpressionSchema;
using Niveum.Expression;

namespace ExprCalc
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
            DisplayInfo();

            Test();

            return 0;
        }

        public static void DisplayInfo()
        {
            Console.WriteLine(@"表达式计算工具");
            Console.WriteLine(@"ExprCalc，Public Domain");
            Console.WriteLine(@"F.R.C.");
            Console.WriteLine(@"");
            Console.WriteLine(@"请输入表达式，如");
            Console.WriteLine(@"1+1");
            Console.WriteLine(@"1+1==2");
            Console.WriteLine(@"clamp(1.1, 1, 2)");
            Console.WriteLine(@"a=1");
            Console.WriteLine(@"a");
            Console.WriteLine(@"a(x:Int):Int=x*2");
            Console.WriteLine(@"a(2)");
            Console.WriteLine(@"del a");
            Console.WriteLine(@"exit");
            Console.WriteLine(@"");
        }

        public static void Test()
        {
            Assembly a;
            var bs = BinarySerializerWithString.Create();
            using (var s = Streams.OpenReadable("Assembly.bin"))
            {
                a = bs.Read<Assembly>(s);
            }

            var vc = new VariableContext<int>();
            foreach (var m in a.Modules)
            {
                foreach (var f in m.Functions)
                {
                    vc.Replace(f.Name, f);
                }
            }

            var c = new Calculation(a);
            Trace.Assert(c.Character.GetUpgradeExperience(1, 2) == 2);

            TestBasic(vc);

            TestInteractive(vc);
        }

        private static void TestBasic(VariableContext<int> vc)
        {
            //等于/不等于
            Trace.Assert(Evaluate(vc, "false==false").Equals(true));
            Trace.Assert(Evaluate(vc, "false==true").Equals(false));
            Trace.Assert(Evaluate(vc, "true==false").Equals(false));
            Trace.Assert(Evaluate(vc, "true==true").Equals(true));
            Trace.Assert(Evaluate(vc, "false!=false").Equals(false));
            Trace.Assert(Evaluate(vc, "false!=true").Equals(true));
            Trace.Assert(Evaluate(vc, "true!=false").Equals(true));
            Trace.Assert(Evaluate(vc, "true!=true").Equals(false));
            Trace.Assert(Evaluate(vc, "1==1").Equals(true));
            Trace.Assert(Evaluate(vc, "1==2").Equals(false));
            Trace.Assert(Evaluate(vc, "1!=1").Equals(false));
            Trace.Assert(Evaluate(vc, "1!=2").Equals(true));
            Func<String, Boolean> e = s => (Boolean)(Evaluate(vc, s));
            Trace.Assert(e("1==1"));
            Trace.Assert(e("1!=2"));

            //特殊运算符
            Trace.Assert(e("if(true,1,2)==1"));
            Trace.Assert(e("if(false,1,2)==2"));
            Trace.Assert(e("abs(if(true,1.0,2.0)-1.0)<0.00001"));
            Trace.Assert(e("abs(if(false,1.0,2.0)-2.0)<0.00001"));
            Trace.Assert(e("if(1==1,1,div(1,0))==1"));
            try
            {
                e("if(1==2,1,div(1,0))==1");
                Trace.Assert(false);
            }
            catch (DivideByZeroException)
            {
                Trace.Assert(true);
            }
            Trace.Assert(e("true && true"));
            Trace.Assert(e("!(false && true)"));
            Trace.Assert(e("!(true && false)"));
            Trace.Assert(e("!(false && false)"));
            Trace.Assert(e("!(false && (div(1,0)==1))"));
            try
            {
                Trace.Assert(e("true && (div(1,0)==1)"));
                Trace.Assert(false);
            }
            catch (DivideByZeroException)
            {
                Trace.Assert(true);
            }
            Trace.Assert(e("!(false || false)"));
            Trace.Assert(e("false || true"));
            Trace.Assert(e("true || false"));
            Trace.Assert(e("true || true"));
            Trace.Assert(e("true || (div(1,0)==1)"));
            try
            {
                Trace.Assert(e("false || (div(1,0)==1)"));
                Trace.Assert(false);
            }
            catch (DivideByZeroException)
            {
                Trace.Assert(true);
            }
            Trace.Assert(e("true && true && true"));
            Trace.Assert(e("!(false || false || false)"));
            try
            {
                Trace.Assert(e("true && true || true"));
                Trace.Assert(false);
            }
            catch (Exception)
            {
                Trace.Assert(true);
            }

            //算术运算
            Trace.Assert(e("1+1==2"));
            Trace.Assert(e("2-1==1"));
            Trace.Assert(e("2*3==6"));
            Trace.Assert(e("abs(3/4-3/4)<0.00001"));
            Trace.Assert(e("abs(1.1+1.2-2.3)<0.00001"));
            Trace.Assert(e("abs(1.1*1.2-1.32)<0.00001"));
            Trace.Assert(e("abs(2.5/5.0-0.5)<0.00001"));
            Trace.Assert(e("abs(2.5/5-0.5)<0.00001"));
            Trace.Assert(e("pow(2,4)==16"));
            Trace.Assert(e("pow(3,5)==243"));
            Trace.Assert(e("abs(pow(2.0,4.0)-16.0)<0.00001"));
            Trace.Assert(e("abs(pow(3.0,5.0)-243.0)<0.00001"));
            Trace.Assert(e("abs(exp(2.2)-9.02501)<0.00001"));
            Trace.Assert(e("abs(log(2.2)-0.788457)<0.00001"));
            Trace.Assert(e("mod(6,3)==0"));
            Trace.Assert(e("mod(7,3)==1"));
            Trace.Assert(e("mod(6,-3)==0"));
            Trace.Assert(e("mod(7,-3)==-2"));
            Trace.Assert(e("mod(-6,3)==0"));
            Trace.Assert(e("mod(-7,3)==2"));
            Trace.Assert(e("mod(-6,-3)==0"));
            Trace.Assert(e("mod(-7,-3)==-1"));
            Trace.Assert(e("div(6,3)==2"));
            Trace.Assert(e("div(7,3)==2"));
            Trace.Assert(e("div(6,-3)==-2"));
            Trace.Assert(e("div(7,-3)==-3"));
            Trace.Assert(e("div(-6,3)==-2"));
            Trace.Assert(e("div(-7,3)==-3"));
            Trace.Assert(e("div(-6,-3)==2"));
            Trace.Assert(e("div(-7,-3)==2"));

            //逻辑运算
            Trace.Assert(e("!false==true"));
            Trace.Assert(e("!true==false"));

            //关系运算
            Trace.Assert(e("1<2==true"));
            Trace.Assert(e("1<1==false"));
            Trace.Assert(e("2<1==false"));
            Trace.Assert(e("1>2==false"));
            Trace.Assert(e("1>1==false"));
            Trace.Assert(e("2>1==true"));
            Trace.Assert(e("1<=2==true"));
            Trace.Assert(e("1<=1==true"));
            Trace.Assert(e("2<=1==false"));
            Trace.Assert(e("1>=2==false"));
            Trace.Assert(e("1>=1==true"));
            Trace.Assert(e("2>=1==true"));
            Trace.Assert(e("1.1<2.1==true"));
            Trace.Assert(e("1.1<1.1==false"));
            Trace.Assert(e("2.1<1.1==false"));
            Trace.Assert(e("1.1>2.1==false"));
            Trace.Assert(e("1.1>1.1==false"));
            Trace.Assert(e("2.1>1.1==true"));
            Trace.Assert(e("1.1<=2.1==true"));
            Trace.Assert(e("1.1<=1.1==true"));
            Trace.Assert(e("2.1<=1.1==false"));
            Trace.Assert(e("1.1>=2.1==false"));
            Trace.Assert(e("1.1>=1.1==true"));
            Trace.Assert(e("2.1>=1.1==true"));

            //取整运算
            Trace.Assert(e("round(1.5)==2"));
            Trace.Assert(e("round(1.9)==2"));
            Trace.Assert(e("round(2.0)==2"));
            Trace.Assert(e("round(2.1)==2"));
            Trace.Assert(e("round(2.5)==2"));
            Trace.Assert(e("round(2.9)==3"));
            Trace.Assert(e("round(3.0)==3"));
            Trace.Assert(e("round(3.1)==3"));
            Trace.Assert(e("round(3.5)==4"));
            Trace.Assert(e("round(-1.5)==-2"));
            Trace.Assert(e("round(-1.9)==-2"));
            Trace.Assert(e("round(-2.0)==-2"));
            Trace.Assert(e("round(-2.1)==-2"));
            Trace.Assert(e("round(-2.5)==-2"));
            Trace.Assert(e("round(-2.9)==-3"));
            Trace.Assert(e("round(-3.0)==-3"));
            Trace.Assert(e("round(-3.1)==-3"));
            Trace.Assert(e("round(-3.5)==-4"));
            Trace.Assert(e("floor(1.5)==1"));
            Trace.Assert(e("floor(1.9)==1"));
            Trace.Assert(e("floor(2.0)==2"));
            Trace.Assert(e("floor(2.1)==2"));
            Trace.Assert(e("floor(2.5)==2"));
            Trace.Assert(e("floor(-1.5)==-2"));
            Trace.Assert(e("floor(-1.9)==-2"));
            Trace.Assert(e("floor(-2.0)==-2"));
            Trace.Assert(e("floor(-2.1)==-3"));
            Trace.Assert(e("floor(-2.5)==-3"));
            Trace.Assert(e("ceil(1.5)==2"));
            Trace.Assert(e("ceil(1.9)==2"));
            Trace.Assert(e("ceil(2.0)==2"));
            Trace.Assert(e("ceil(2.1)==3"));
            Trace.Assert(e("ceil(2.5)==3"));
            Trace.Assert(e("ceil(-1.5)==-1"));
            Trace.Assert(e("ceil(-1.9)==-1"));
            Trace.Assert(e("ceil(-2.0)==-2"));
            Trace.Assert(e("ceil(-2.1)==-2"));
            Trace.Assert(e("ceil(-2.5)==-2"));
            Trace.Assert(e("abs(round(0.15, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(round(0.19, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(round(0.20, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(round(0.21, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(round(0.25, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(round(0.29, 1)-0.3)<0.00001"));
            Trace.Assert(e("abs(round(0.30, 1)-0.3)<0.00001"));
            Trace.Assert(e("abs(round(0.31, 1)-0.3)<0.00001"));
            Trace.Assert(e("abs(round(0.35, 1)-0.4)<0.00001"));
            Trace.Assert(e("abs(round(-0.15, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(round(-0.19, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(round(-0.20, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(round(-0.21, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(round(-0.25, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(round(-0.29, 1)--0.3)<0.00001"));
            Trace.Assert(e("abs(round(-0.30, 1)--0.3)<0.00001"));
            Trace.Assert(e("abs(round(-0.31, 1)--0.3)<0.00001"));
            Trace.Assert(e("abs(round(-0.35, 1)--0.4)<0.00001"));
            Trace.Assert(e("abs(floor(0.15, 1)-0.1)<0.00001"));
            Trace.Assert(e("abs(floor(0.19, 1)-0.1)<0.00001"));
            Trace.Assert(e("abs(floor(0.20, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(floor(0.21, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(floor(0.25, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(floor(-0.15, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(floor(-0.19, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(floor(-0.20, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(floor(-0.21, 1)--0.3)<0.00001"));
            Trace.Assert(e("abs(floor(-0.25, 1)--0.3)<0.00001"));
            Trace.Assert(e("abs(ceil(0.15, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(ceil(0.19, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(ceil(0.20, 1)-0.2)<0.00001"));
            Trace.Assert(e("abs(ceil(0.21, 1)-0.3)<0.00001"));
            Trace.Assert(e("abs(ceil(0.25, 1)-0.3)<0.00001"));
            Trace.Assert(e("abs(ceil(-0.15, 1)--0.1)<0.00001"));
            Trace.Assert(e("abs(ceil(-0.19, 1)--0.1)<0.00001"));
            Trace.Assert(e("abs(ceil(-0.20, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(ceil(-0.21, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(ceil(-0.25, 1)--0.2)<0.00001"));
            Trace.Assert(e("abs(round(15, -1)-20)<0.00001"));
            Trace.Assert(e("abs(round(19, -1)-20)<0.00001"));
            Trace.Assert(e("abs(round(20, -1)-20)<0.00001"));
            Trace.Assert(e("abs(round(21, -1)-20)<0.00001"));
            Trace.Assert(e("abs(round(25, -1)-20)<0.00001"));
            Trace.Assert(e("abs(round(29, -1)-30)<0.00001"));
            Trace.Assert(e("abs(round(30, -1)-30)<0.00001"));
            Trace.Assert(e("abs(round(31, -1)-30)<0.00001"));
            Trace.Assert(e("abs(round(35, -1)-40)<0.00001"));
            Trace.Assert(e("abs(round(-15, -1)--20)<0.00001"));
            Trace.Assert(e("abs(round(-19, -1)--20)<0.00001"));
            Trace.Assert(e("abs(round(-20, -1)--20)<0.00001"));
            Trace.Assert(e("abs(round(-21, -1)--20)<0.00001"));
            Trace.Assert(e("abs(round(-25, -1)--20)<0.00001"));
            Trace.Assert(e("abs(round(-29, -1)--30)<0.00001"));
            Trace.Assert(e("abs(round(-30, -1)--30)<0.00001"));
            Trace.Assert(e("abs(round(-31, -1)--30)<0.00001"));
            Trace.Assert(e("abs(round(-35, -1)--40)<0.00001"));
            Trace.Assert(e("abs(floor(15, -1)-10)<0.00001"));
            Trace.Assert(e("abs(floor(19, -1)-10)<0.00001"));
            Trace.Assert(e("abs(floor(20, -1)-20)<0.00001"));
            Trace.Assert(e("abs(floor(21, -1)-20)<0.00001"));
            Trace.Assert(e("abs(floor(25, -1)-20)<0.00001"));
            Trace.Assert(e("abs(floor(-15, -1)--20)<0.00001"));
            Trace.Assert(e("abs(floor(-19, -1)--20)<0.00001"));
            Trace.Assert(e("abs(floor(-20, -1)--20)<0.00001"));
            Trace.Assert(e("abs(floor(-21, -1)--30)<0.00001"));
            Trace.Assert(e("abs(floor(-25, -1)--30)<0.00001"));
            Trace.Assert(e("abs(ceil(15, -1)-20)<0.00001"));
            Trace.Assert(e("abs(ceil(19, -1)-20)<0.00001"));
            Trace.Assert(e("abs(ceil(20, -1)-20)<0.00001"));
            Trace.Assert(e("abs(ceil(21, -1)-30)<0.00001"));
            Trace.Assert(e("abs(ceil(25, -1)-30)<0.00001"));
            Trace.Assert(e("abs(ceil(-15, -1)--10)<0.00001"));
            Trace.Assert(e("abs(ceil(-19, -1)--10)<0.00001"));
            Trace.Assert(e("abs(ceil(-20, -1)--20)<0.00001"));
            Trace.Assert(e("abs(ceil(-21, -1)--20)<0.00001"));
            Trace.Assert(e("abs(ceil(-25, -1)--20)<0.00001"));

            //范围限制运算
            Trace.Assert(e("min(1,1)==1"));
            Trace.Assert(e("min(1,2)==1"));
            Trace.Assert(e("min(2,1)==1"));
            Trace.Assert(e("max(1,1)==1"));
            Trace.Assert(e("max(1,2)==2"));
            Trace.Assert(e("max(2,1)==2"));
            Trace.Assert(e("clamp(0,1,4)==1"));
            Trace.Assert(e("clamp(1,1,4)==1"));
            Trace.Assert(e("clamp(2,1,4)==2"));
            Trace.Assert(e("clamp(3,1,4)==3"));
            Trace.Assert(e("clamp(4,1,4)==4"));
            Trace.Assert(e("clamp(5,1,4)==4"));
            Trace.Assert(e("abs(min(1.1,1.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(min(1.1,2.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(min(2.1,1.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(max(1.1,1.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(max(1.1,2.1)-2.1)<0.00001"));
            Trace.Assert(e("abs(max(2.1,1.1)-2.1)<0.00001"));
            Trace.Assert(e("abs(clamp(0.1,1.1,4.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(clamp(1.1,1.1,4.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(clamp(2.1,1.1,4.1)-2.1)<0.00001"));
            Trace.Assert(e("abs(clamp(3.1,1.1,4.1)-3.1)<0.00001"));
            Trace.Assert(e("abs(clamp(4.1,1.1,4.1)-4.1)<0.00001"));
            Trace.Assert(e("abs(clamp(5.1,1.1,4.1)-4.1)<0.00001"));

            //其他运算
            Trace.Assert(e("abs(1)==1"));
            Trace.Assert(e("abs(-2)==2"));
            Trace.Assert(e("abs(abs(1.1)-1.1)<0.00001"));
            Trace.Assert(e("abs(abs(-2.1)-2.1)<0.00001"));
            for (int k = 0; k < 100; k += 1)
            {
                Trace.Assert(e("rand()>=0"));
                Trace.Assert(e("rand()<1"));
                Trace.Assert(e("rand(4,21)>=4"));
                Trace.Assert(e("rand(4,21)<21"));
                Trace.Assert(e("rand(4.0,21.0)>=4"));
                Trace.Assert(e("rand(4.0,21.0)<21"));
            }
            Trace.Assert(e("abs(creal(1)-1.0)<0.00001"));
            Trace.Assert(e("abs(1-1.0)<0.00001"));
        }

        private static Object Evaluate(VariableContext<int> vc, String s)
        {
            var p = new VariableProviderCombiner<int>(vc, new ExpressionRuntimeProvider<int>());
            var r = ExpressionParser.ParseExpr(p, s);
            var d = ExpressionEvaluator<int>.Compile(p, r).AdaptFunction<int, Object>();
            var o = d(0);
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

        private static Regex rAssignment = new Regex(@"^\s*(?<Identifier>[A-Za-z_][A-Za-z0-9_]*)\s*(?<!=)=(?!=)\s*(?<Expr>.*)$", RegexOptions.ExplicitCapture);
        private static Regex rFunctionDefinition = new Regex(@"^\s*(?<Signature>.*?)\s*(?<!=)=(?!=)\s*(?<Expr>.*)$", RegexOptions.ExplicitCapture);
        private static Regex rDelete = new Regex(@"^\s*del\s*(?<Identifier>[A-Za-z_][A-Za-z0-9_]*)\s*", RegexOptions.ExplicitCapture);
        public static void TestInteractive(VariableContext<int> vc)
        {
            while (true)
            {
                Console.Write("> ");
                var OutputStart = "> ".Length;
                var Line = "";
                while (true)
                {
                    var TempLine = Console.ReadLine();
                    if (TempLine.EndsWith(@"\"))
                    {
                        Line += TempLine.Substring(0, TempLine.Length - 1);
                    }
                    else
                    {
                        Line += TempLine;
                        break;
                    }
                }
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

                            var v = Evaluate(vc, Expr);
                            vc.Replace(Identifier, v);
                            Console.WriteLine(ToString(v));
                            continue;
                        }
                    }
                    {
                        var m = rFunctionDefinition.Match(Line);
                        if (m.Success)
                        {
                            var Signature = m.Result("${Signature}");
                            var Expr = m.Result("${Expr}");
                            var p = new VariableProviderCombiner<int>(vc, new ExpressionRuntimeProvider<int>());
                            var rs = ExpressionParser.ParseSignature(Signature);
                            OutputStart += Line.Length - Expr.Length;
                            var rb = ExpressionParser.ParseBody(p, rs.Declaration, Expr);
                            var r = new ExpressionParserResult
                            {
                                Definition = new FunctionDef
                                {
                                    Name = rs.Declaration.Name,
                                    Parameters = rs.Declaration.Parameters,
                                    ReturnValue = rs.Declaration.ReturnValue,
                                    Body = rb.Body
                                },
                                Positions = rb.Positions,
                                TypeDict = rb.TypeDict
                            };
                            vc.Replace(r.Definition.Name, r.Definition);
                            Console.WriteLine(rs.Declaration.Name);
                            continue;
                        }
                    }
                    {
                        var m = rDelete.Match(Line);
                        if (m.Success)
                        {
                            var Identifier = m.Result("${Identifier}");
                            var Overloads = vc.GetOverloads(Identifier);
                            foreach (var o in Overloads)
                            {
                                vc.TryRemove(Identifier, o.ParameterTypes);
                                String t;
                                if (o.ParameterTypes == null)
                                {
                                    t = o.ReturnType.ToString();
                                }
                                else
                                {
                                    t = String.Join("->", o.ParameterTypes.Concat(new PrimitiveType[] { o.ReturnType }).Select(p => p.ToString()).ToArray());
                                }
                                Console.WriteLine(String.Format("{0}:{1} deleted.", Identifier, t));
                            }
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
                    if (ex.Range.OnSome)
                    {
                        var r = ex.Range.Value.Range;
                        if (r.OnSome)
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
