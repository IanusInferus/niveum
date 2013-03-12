//==========================================================================
//
//  File:        ExpressionRuntime.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式系统库
//  Version:     2013.03.12.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Firefly;
using Firefly.Mapping.MetaProgramming;
using Yuki.ExpressionSchema;

namespace Yuki.Expression
{
    public static class ExpressionRuntime
    {
        public static int pow(int Left, int Right)
        {
            if (Right < 0) { throw new InvalidOperationException(); }
            int v = 1;
            int b = Left;
            for (int k = 0; k < 32; k += 1)
            {
                if ((Right & (1 << k)) != 0)
                {
                    v *= b;
                }
                b *= b;
            }
            return v;
        }

        public static double pow(double Left, double Right)
        {
            return Math.Pow(Left, Right);
        }

        public static int mod(int v, int m)
        {
            var r = v % m;
            if ((r < 0 && m > 0) || (r > 0 && m < 0)) { r += m; }
            return r;
        }

        public static int div(int Left, int Right)
        {
            var r = mod(Left, Right);
            return (Left - r) / Right;
        }

        public static int round(double v)
        {
            return Convert.ToInt32(Math.Round(v));
        }

        public static int floor(double v)
        {
            return Convert.ToInt32(Math.Floor(v));
        }

        public static int ceil(double v)
        {
            return Convert.ToInt32(Math.Ceiling(v));
        }

        public static double round(double v, int NumFractionDigit)
        {
            return Math.Round(v, NumFractionDigit);
        }

        public static double floor(double v, int NumFractionDigit)
        {
            return Math.Floor(v * pow(10.0, NumFractionDigit)) * pow(0.1, NumFractionDigit);
        }

        public static double ceil(double v, int NumFractionDigit)
        {
            return Math.Ceiling(v * pow(10.0, NumFractionDigit)) * pow(0.1, NumFractionDigit);
        }

        public static int min(int v1, int v2)
        {
            return v1 <= v2 ? v1 : v2;
        }

        public static int max(int v1, int v2)
        {
            return v1 <= v2 ? v2 : v1;
        }

        public static int clamp(int v, int LowerBound, int UpperBound)
        {
            if (v <= LowerBound)
            {
                return LowerBound;
            }
            if (v >= UpperBound)
            {
                return UpperBound;
            }
            return v;
        }

        public static double min(double v1, double v2)
        {
            return v1 <= v2 ? v1 : v2;
        }

        public static double max(double v1, double v2)
        {
            return v1 <= v2 ? v2 : v1;
        }

        public static double clamp(double v, double LowerBound, double UpperBound)
        {
            if (v <= LowerBound)
            {
                return LowerBound;
            }
            if (v >= UpperBound)
            {
                return UpperBound;
            }
            return v;
        }

        public static int abs(int v)
        {
            return Math.Abs(v);
        }

        public static double abs(double v)
        {
            return Math.Abs(v);
        }

        private static ThreadLocal<Random> _RNG = new ThreadLocal<Random>(() => new Random());
        private static Random RNG
        {
            get
            {
                return _RNG.Value;
            }
        }

        public static double rand()
        {
            return RNG.NextDouble();
        }

        public static int rand(int LowerBound, int UpperBoundExclusive)
        {
            return RNG.Next(LowerBound, UpperBoundExclusive);
        }

        public static double rand(double LowerBound, double UpperBoundExclusive)
        {
            return LowerBound + RNG.NextDouble() * (UpperBoundExclusive - LowerBound);
        }
    }

    public class ExpressionRuntimeProvider : IVariableTypeProvider, IVariableProvider
    {
        private class FunctionSignature
        {
            public String Name;
            public PrimitiveType[] Types;

            public FunctionSignature()
            {
            }
            public FunctionSignature(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, pt2, rt };
            }
            public FunctionSignature(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, rt };
            }
            public FunctionSignature(String Name, PrimitiveType pt0, PrimitiveType rt)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, rt };
            }
            public FunctionSignature(String Name, PrimitiveType rt)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { rt };
            }
        }

        private static class FunctionSignatureMap
        {
            public static Dictionary<String, List<FunctionSignature>> Map;

            static FunctionSignatureMap()
            {
                var l = new List<FunctionSignature>();

                //算术运算
                l.Add(new FunctionSignature("+", PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("-", PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("+", PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("-", PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("+", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("-", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("*", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("/", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Real));
                l.Add(new FunctionSignature("+", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("-", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("*", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("/", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("pow", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("pow", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("mod", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("div", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));

                //逻辑运算
                l.Add(new FunctionSignature("!", PrimitiveType.Boolean, PrimitiveType.Boolean));

                //关系运算
                l.Add(new FunctionSignature("<", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
                l.Add(new FunctionSignature(">", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("<=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
                l.Add(new FunctionSignature(">=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("==", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("!=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("<", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
                l.Add(new FunctionSignature(">", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("<=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
                l.Add(new FunctionSignature(">=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("==", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean));
                l.Add(new FunctionSignature("!=", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean));

                //取整运算
                l.Add(new FunctionSignature("round", PrimitiveType.Real, PrimitiveType.Int));
                l.Add(new FunctionSignature("floor", PrimitiveType.Real, PrimitiveType.Int));
                l.Add(new FunctionSignature("ceil", PrimitiveType.Real, PrimitiveType.Int));
                l.Add(new FunctionSignature("round", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real));
                l.Add(new FunctionSignature("floor", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real));
                l.Add(new FunctionSignature("ceil", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real));

                //范围限制运算
                l.Add(new FunctionSignature("min", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("max", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("min", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("max", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));

                //其他运算
                l.Add(new FunctionSignature("abs", PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("rand", PrimitiveType.Real));
                l.Add(new FunctionSignature("rand", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int));
                l.Add(new FunctionSignature("rand", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real));
                l.Add(new FunctionSignature("creal", PrimitiveType.Int, PrimitiveType.Real));

                Map = new Dictionary<String, List<FunctionSignature>>();
                foreach (var fs in l)
                {
                    if (Map.ContainsKey(fs.Name))
                    {
                        Map[fs.Name].Add(fs);
                    }
                    else
                    {
                        Map.Add(fs.Name, new List<FunctionSignature> { fs });
                    }
                }
            }
        }

        public PrimitiveType[][] GetOverloads(string Name)
        {
            if (FunctionSignatureMap.Map.ContainsKey(Name)) { return FunctionSignatureMap.Map[Name].Select(f => f.Types).ToArray(); }
            return new PrimitiveType[][] { };
        }

        public PrimitiveType[] GetMatched(string Name, PrimitiveType[] ParameterTypes)
        {
            if (FunctionSignatureMap.Map.ContainsKey(Name))
            {
                var Matched = FunctionSignatureMap.Map[Name].Where(f => f.Types.Take(f.Types.Length - 1).SequenceEqual(ParameterTypes)).Select(f => f.Types.Last()).ToArray();
                return Matched;
            }
            return new PrimitiveType[] { };
        }

        public Delegate[] GetValue<TVariableContext>(String Name, PrimitiveType[] ParameterTypes, Delegate[] ParameterFuncs)
        {
            //算术运算
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "+", PrimitiveType.Int))
            {
                var Operand = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => +Operand(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "-", PrimitiveType.Int))
            {
                var Operand = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => -Operand(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "+", PrimitiveType.Real))
            {
                var Operand = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => +Operand(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "-", PrimitiveType.Real))
            {
                var Operand = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => -Operand(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "+", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => Left(vc) + Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "-", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => Left(vc) - Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "*", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => Left(vc) * Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "/", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => (double)(Left(vc)) / (double)(Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "+", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => Left(vc) + Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "-", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => Left(vc) - Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "*", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => Left(vc) * Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "/", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => Left(vc) / Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "pow", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.pow(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "pow", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.pow(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "mod", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.mod(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "div", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.div(Left(vc), Right(vc))) };
            }

            //逻辑运算
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "!", PrimitiveType.Boolean))
            {
                var Operand = (Func<TVariableContext, Boolean>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => !Operand(vc)) };
            }

            //关系运算
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "<", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) < Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, ">", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) > Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "<=", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) <= Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, ">=", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) >= Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "==", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) == Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "!=", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) != Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "<", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) < Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, ">", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) > Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "<=", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) <= Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, ">=", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) >= Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "==", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) == Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "!=", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) != Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "==", PrimitiveType.Boolean, PrimitiveType.Boolean))
            {
                var Left = (Func<TVariableContext, Boolean>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, Boolean>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) == Right(vc)) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "!=", PrimitiveType.Boolean, PrimitiveType.Boolean))
            {
                var Left = (Func<TVariableContext, Boolean>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, Boolean>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, Boolean>)(vc => Left(vc) != Right(vc)) };
            }

            //取整运算
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "round", PrimitiveType.Real))
            {
                var Operand = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.round(Operand(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "floor", PrimitiveType.Real))
            {
                var Operand = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.floor(Operand(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "ceil", PrimitiveType.Real))
            {
                var Operand = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.ceil(Operand(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "round", PrimitiveType.Real, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.round(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "floor", PrimitiveType.Real, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.floor(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "ceil", PrimitiveType.Real, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.ceil(Left(vc), Right(vc))) };
            }

            //范围限制运算
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "min", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.min(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "max", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Left = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.max(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int))
            {
                var Arg0 = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Arg1 = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                var Arg2 = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.clamp(Arg0(vc), Arg1(vc), Arg2(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "min", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.min(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "max", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Left = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Right = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.max(Left(vc), Right(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real))
            {
                var Arg0 = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Arg1 = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                var Arg2 = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.clamp(Arg0(vc), Arg1(vc), Arg2(vc))) };
            }

            //其他运算
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "abs", PrimitiveType.Int))
            {
                var Operand = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.abs(Operand(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "abs", PrimitiveType.Real))
            {
                var Operand = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.abs(Operand(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "rand"))
            {
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.rand()) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "rand", PrimitiveType.Int, PrimitiveType.Int))
            {
                var Arg0 = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                var Arg1 = (Func<TVariableContext, int>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, int>)(vc => ExpressionRuntime.rand(Arg0(vc), Arg1(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "rand", PrimitiveType.Real, PrimitiveType.Real))
            {
                var Arg0 = (Func<TVariableContext, double>)(ParameterFuncs[0]);
                var Arg1 = (Func<TVariableContext, double>)(ParameterFuncs[1]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => ExpressionRuntime.rand(Arg0(vc), Arg1(vc))) };
            }
            if (MatchFunctionNameAndParameters(Name, ParameterFuncs, "creal", PrimitiveType.Int))
            {
                var Operand = (Func<TVariableContext, int>)(ParameterFuncs[0]);
                return new Delegate[] { (Func<TVariableContext, double>)(vc => Operand(vc)) };
            }

            return new Delegate[] { };
        }

        private static Boolean MatchFuncType(Delegate ParameterFunc, PrimitiveType t)
        {
            if (t == PrimitiveType.Boolean && ParameterFunc.ReturnType() == typeof(Boolean))
            {
                return true;
            }
            if (t == PrimitiveType.Int && ParameterFunc.ReturnType() == typeof(int))
            {
                return true;
            }
            if (t == PrimitiveType.Real && ParameterFunc.ReturnType() == typeof(double))
            {
                return true;
            }
            return false;
        }
        private static Boolean MatchFunctionNameAndParameters(String NameFunc, Delegate[] ParameterFuncs, String Name)
        {
            if (NameFunc != Name) { return false; }
            if (ParameterFuncs.Length != 0) { return false; }
            return true;
        }
        private static Boolean MatchFunctionNameAndParameters(String NameFunc, Delegate[] ParameterFuncs, String Name, PrimitiveType t1)
        {
            if (NameFunc != Name) { return false; }
            if (ParameterFuncs.Length != 1) { return false; }
            if (!MatchFuncType(ParameterFuncs[0], t1)) { return false; }
            return true;
        }
        private static Boolean MatchFunctionNameAndParameters(String NameFunc, Delegate[] ParameterFuncs, String Name, PrimitiveType t1, PrimitiveType t2)
        {
            if (NameFunc != Name) { return false; }
            if (ParameterFuncs.Length != 2) { return false; }
            if (!MatchFuncType(ParameterFuncs[0], t1)) { return false; }
            if (!MatchFuncType(ParameterFuncs[1], t2)) { return false; }
            return true;
        }
        private static Boolean MatchFunctionNameAndParameters(String NameFunc, Delegate[] ParameterFuncs, String Name, PrimitiveType t1, PrimitiveType t2, PrimitiveType t3)
        {
            if (NameFunc != Name) { return false; }
            if (ParameterFuncs.Length != 3) { return false; }
            if (!MatchFuncType(ParameterFuncs[0], t1)) { return false; }
            if (!MatchFuncType(ParameterFuncs[1], t2)) { return false; }
            if (!MatchFuncType(ParameterFuncs[2], t3)) { return false; }
            return true;
        }
        private static Boolean MatchFunctionNameAndParameters(String NameFunc, Delegate[] ParameterFuncs, String Name, params PrimitiveType[] ts)
        {
            if (NameFunc != Name) { return false; }
            if (ParameterFuncs.Length != ts.Length) { return false; }
            for (int k = 0; k < ParameterFuncs.Length; k += 1)
            {
                if (!MatchFuncType(ParameterFuncs[k], ts[k])) { return false; }
            }
            return true;
        }
    }
}
