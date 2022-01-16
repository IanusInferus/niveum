//==========================================================================
//
//  File:        CSource.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 表达式结构C源代码生成器
//  Version:     2022.01.17.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using Niveum.Expression;
using System;
using System.Collections.Generic;
using System.Linq;
using OS = Niveum.ObjectSchema;

namespace Niveum.ExpressionSchema.CSource
{
    public static class CodeGenerator
    {
        public static String CompileToCHeader(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.GenerateHeader(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSource(this Schema Schema, String NamespaceName, Niveum.ExpressionSchema.Assembly a)
        {
            var t = new Templates(Schema);
            var Lines = t.GenerateSource(Schema, NamespaceName, a).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
    }

    public partial class Templates
    {
        private OS.Cpp.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new OS.Cpp.Templates(new OS.Schema
            {
                Types = new List<OS.TypeDef> { },
                TypeRefs = new List<OS.TypeDef>
                    {
                        OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Unit" }, GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } }),
                        OS.TypeDef.CreatePrimitive(new OS.PrimitiveDef { Name = new List<String> { "Boolean" }, GenericParameters = new List<OS.VariableDef> { }, Description = "", Attributes = new List<KeyValuePair<String, List<String>>> { } })
                    },
                Imports = new List<String> { }
            });
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public Boolean IsInclude(String s)
        {
            return Inner.IsInclude(s);
        }

        private String BuildBody(FunctionDef f)
        {
            return "return " + BuildExpr(f.Body) + ";";
        }

        private class FunctionBuilder
        {
            public String Name { get; init; }
            public List<PrimitiveType> ParameterTypes { get; init; }
            public Func<List<String>, String> Build { get; init; }

            public static FunctionBuilder CreateOperator(String Name, Func<List<String>, String> Build)
            {
                return new FunctionBuilder { Name = Name, ParameterTypes = new List<PrimitiveType> { }, Build = Build };
            }
            public static FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, Func<List<String>, String> Build)
            {
                return new FunctionBuilder { Name = Name, ParameterTypes = new List<PrimitiveType> { pt0 }, Build = Build };
            }
            public static FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, PrimitiveType pt1, Func<List<String>, String> Build)
            {
                return new FunctionBuilder { Name = Name, ParameterTypes = new List<PrimitiveType> { pt0, pt1 }, Build = Build };
            }
            public static FunctionBuilder CreateOperator(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, Func<List<String>, String> Build)
            {
                return new FunctionBuilder { Name = Name, ParameterTypes = new List<PrimitiveType> { pt0, pt1, pt2 }, Build = Build };
            }
            private static FunctionBuilder CreateRuntimeFunction(String Name, List<PrimitiveType> ParameterTypes)
            {
                Func<List<String>, String> Build = Arguments =>
                {
                    var ParameterSuffices = Arguments.Count == 0 ? "V" : String.Join("", ParameterTypes.Select(pt => pt.ToString().First().ToString()));
                    return $"Niveum_Expression_{Name}_{ParameterSuffices}({String.Join(", ", Arguments)})";
                };
                return new FunctionBuilder { Name = Name, ParameterTypes = ParameterTypes, Build = Build };
            }
            public static FunctionBuilder CreateRuntimeFunction(String Name)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { });
            }
            public static FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType pt0)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { pt0 });
            }
            public static FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType pt0, PrimitiveType pt1)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { pt0, pt1 });
            }
            public static FunctionBuilder CreateRuntimeFunction(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2)
            {
                return CreateRuntimeFunction(Name, new List<PrimitiveType> { pt0, pt1, pt2 });
            }
        }

        private static Dictionary<String, List<FunctionBuilder>> FunctionBuilders = (new List<FunctionBuilder>
        {
            FunctionBuilder.CreateOperator("+", PrimitiveType.Int, Arguments => $"+{Arguments[0]}"),
            FunctionBuilder.CreateOperator("-", PrimitiveType.Int, Arguments => $"-{Arguments[0]}"),
            FunctionBuilder.CreateOperator("+", PrimitiveType.Real, Arguments => $"+{Arguments[0]}"),
            FunctionBuilder.CreateOperator("-", PrimitiveType.Real, Arguments => $"-{Arguments[0]}"),
            FunctionBuilder.CreateOperator("+", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} + {Arguments[1]})"),
            FunctionBuilder.CreateOperator("-", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} - {Arguments[1]})"),
            FunctionBuilder.CreateOperator("*", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} * {Arguments[1]})"),
            FunctionBuilder.CreateOperator("/", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"((double){Arguments[0]} / (double){Arguments[1]})"),
            FunctionBuilder.CreateOperator("+", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} + {Arguments[1]})"),
            FunctionBuilder.CreateOperator("-", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} - {Arguments[1]})"),
            FunctionBuilder.CreateOperator("*", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} * {Arguments[1]})"),
            FunctionBuilder.CreateOperator("/", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} / {Arguments[1]})"),
            FunctionBuilder.CreateRuntimeFunction("pow", PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("pow", PrimitiveType.Real, PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("exp", PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("log", PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("mod", PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("div", PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateOperator("!", PrimitiveType.Boolean, Arguments => $"!{Arguments[0]}"),
            FunctionBuilder.CreateOperator("<", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} < {Arguments[1]})"),
            FunctionBuilder.CreateOperator(">", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} > {Arguments[1]})"),
            FunctionBuilder.CreateOperator("<=", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} <= {Arguments[1]})"),
            FunctionBuilder.CreateOperator(">=", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} >= {Arguments[1]})"),
            FunctionBuilder.CreateOperator("==", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} == {Arguments[1]})"),
            FunctionBuilder.CreateOperator("!=", PrimitiveType.Int, PrimitiveType.Int, Arguments => $"({Arguments[0]} != {Arguments[1]})"),
            FunctionBuilder.CreateOperator("<", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} < {Arguments[1]})"),
            FunctionBuilder.CreateOperator(">", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} > {Arguments[1]})"),
            FunctionBuilder.CreateOperator("<=", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} <= {Arguments[1]})"),
            FunctionBuilder.CreateOperator(">=", PrimitiveType.Real, PrimitiveType.Real, Arguments => $"({Arguments[0]} >= {Arguments[1]})"),
            FunctionBuilder.CreateOperator("==", PrimitiveType.Boolean, PrimitiveType.Boolean, Arguments => $"({Arguments[0]} == {Arguments[1]})"),
            FunctionBuilder.CreateOperator("!=", PrimitiveType.Boolean, PrimitiveType.Boolean, Arguments => $"({Arguments[0]} != {Arguments[1]})"),
            FunctionBuilder.CreateRuntimeFunction("round", PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("floor", PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("ceil", PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("round", PrimitiveType.Real, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("floor", PrimitiveType.Real, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("ceil", PrimitiveType.Real, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("min", PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("max", PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("min", PrimitiveType.Real, PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("max", PrimitiveType.Real, PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("abs", PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("abs", PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("rand"),
            FunctionBuilder.CreateRuntimeFunction("rand", PrimitiveType.Int, PrimitiveType.Int),
            FunctionBuilder.CreateRuntimeFunction("rand", PrimitiveType.Real, PrimitiveType.Real),
            FunctionBuilder.CreateRuntimeFunction("creal", PrimitiveType.Int)
        }).GroupBy(fb => fb.Name).ToDictionary(g => g.Key, g => g.ToList());
        private String BuildExpr(Expr e)
        {
            if (e.OnLiteral)
            {
                if (e.Literal.OnBooleanValue)
                {
                    var v = e.Literal.BooleanValue;
                    return v ? "true" : "false";
                }
                else if (e.Literal.OnIntValue)
                {
                    var v = e.Literal.IntValue;
                    return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (e.Literal.OnRealValue)
                {
                    var v = e.Literal.RealValue;
                    return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (e.OnVariable)
            {
                var Name = e.Variable.Name;
                return Name;
            }
            else if (e.OnFunction)
            {
                var Name = e.Function.Name;
                var ParameterTypes = e.Function.ParameterTypes;
                var Arguments = e.Function.Arguments.Select(p => BuildExpr(p)).ToList();
                if (FunctionBuilders.ContainsKey(Name))
                {
                    foreach (var fb in FunctionBuilders[Name])
                    {
                        if ((fb.ParameterTypes.Count == ParameterTypes.Count) && (fb.ParameterTypes.SequenceEqual(ParameterTypes)))
                        {
                            return fb.Build(Arguments);
                        }
                    }
                }
                return $"{Name}({String.Join(", ", Arguments)})";
            }
            else if (e.OnIf)
            {
                var c = BuildExpr(e.If.Condition);
                var l = BuildExpr(e.If.TruePart);
                var r = BuildExpr(e.If.FalsePart);
                return $"({c} ? {l} : {r})";
            }
            else if (e.OnAndAlso)
            {
                var l = BuildExpr(e.AndAlso.Left);
                var r = BuildExpr(e.AndAlso.Right);
                return $"({l} && {r})";
            }
            else if (e.OnOrElse)
            {
                var l = BuildExpr(e.OrElse.Left);
                var r = BuildExpr(e.OrElse.Right);
                return $"({l} || {r})";
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
