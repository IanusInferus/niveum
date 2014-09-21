//==========================================================================
//
//  File:        ExpressionRuntime.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式系统库
//  Version:     2014.09.22.
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
            int n = 30;
            for (int k = 30; k >= 0; k -= 1)
            {
                if ((Right & (1 << k)) != 0)
                {
                    n = k;
                    break;
                }
            }
            int v = 1;
            int b = Left;
            for (int k = 0; k <= n; k += 1)
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

        public static double exp(double v)
        {
            return Math.Exp(v);
        }

        public static double log(double v)
        {
            return Math.Log(v);
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
            return Math.Round(v * pow(10.0, NumFractionDigit)) * pow(0.1, NumFractionDigit);
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

    /// <summary>
    /// 本类是线程安全的。
    /// </summary>
    public class ExpressionRuntimeProvider<T> : IVariableTypeProvider, IVariableProvider<T>
    {
        private class FunctionResolver
        {
            public String Name;
            public PrimitiveType[] ParameterTypes;
            public PrimitiveType ReturnType;
            public Delegate Create;

            public FunctionResolver()
            {
            }
            public FunctionResolver(String Name, PrimitiveType rt, Func<Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType rt, Func<Func<T, Boolean>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType rt, Func<Func<T, int>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType rt, Func<Func<T, double>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<T, Boolean>, Func<T, Boolean>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0, pt1 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<T, int>, Func<T, int>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0, pt1 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<T, double>, Func<T, double>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0, pt1 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<T, double>, Func<T, int>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0, pt1 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt, Func<Func<T, int>, Func<T, int>, Func<T, int>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0, pt1, pt2 };
                this.ReturnType = rt;
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt, Func<Func<T, double>, Func<T, double>, Func<T, double>, Delegate> Create)
            {
                this.Name = Name;
                this.ParameterTypes = new PrimitiveType[] { pt0, pt1, pt2 };
                this.ReturnType = rt;
                this.Create = Create;
            }
        }

        private static class FunctionSignatureMap
        {
            public static Dictionary<String, List<FunctionResolver>> Map; //只读时是线程安全的

            static FunctionSignatureMap()
            {
                var l = new List<FunctionResolver>();

                //算术运算
                l.Add(new FunctionResolver("+", PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Operand) => (Func<T, int>)(t => +Operand(t))));
                l.Add(new FunctionResolver("-", PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Operand) => (Func<T, int>)(t => -Operand(t))));
                l.Add(new FunctionResolver("+", PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Operand) => (Func<T, double>)(t => +Operand(t))));
                l.Add(new FunctionResolver("-", PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Operand) => (Func<T, double>)(t => -Operand(t))));
                l.Add(new FunctionResolver("+", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => Left(t) + Right(t))));
                l.Add(new FunctionResolver("-", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => Left(t) - Right(t))));
                l.Add(new FunctionResolver("*", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => Left(t) * Right(t))));
                l.Add(new FunctionResolver("/", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Real, (Func<T, int> Left, Func<T, int> Right) => (Func<T, double>)(t => (double)(Left(t)) / (double)(Right(t)))));
                l.Add(new FunctionResolver("+", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => Left(t) + Right(t))));
                l.Add(new FunctionResolver("-", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => Left(t) - Right(t))));
                l.Add(new FunctionResolver("*", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => Left(t) * Right(t))));
                l.Add(new FunctionResolver("/", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => Left(t) / Right(t))));
                l.Add(new FunctionResolver("pow", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => ExpressionRuntime.pow(Left(t), Right(t)))));
                l.Add(new FunctionResolver("pow", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => ExpressionRuntime.pow(Left(t), Right(t)))));
                l.Add(new FunctionResolver("exp", PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Operand) => (Func<T, double>)(t => ExpressionRuntime.exp(Operand(t)))));
                l.Add(new FunctionResolver("log", PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Operand) => (Func<T, double>)(t => ExpressionRuntime.log(Operand(t)))));
                l.Add(new FunctionResolver("mod", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => ExpressionRuntime.mod(Left(t), Right(t)))));
                l.Add(new FunctionResolver("div", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => ExpressionRuntime.div(Left(t), Right(t)))));

                //逻辑运算
                l.Add(new FunctionResolver("!", PrimitiveType.Boolean, PrimitiveType.Boolean, (Func<T, Boolean> Operand) => (Func<T, Boolean>)(t => !Operand(t))));

                //关系运算
                l.Add(new FunctionResolver("<", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<T, int> Left, Func<T, int> Right) => (Func<T, Boolean>)(t => Left(t) < Right(t))));
                l.Add(new FunctionResolver(">", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<T, int> Left, Func<T, int> Right) => (Func<T, Boolean>)(t => Left(t) > Right(t))));
                l.Add(new FunctionResolver("<=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<T, int> Left, Func<T, int> Right) => (Func<T, Boolean>)(t => Left(t) <= Right(t))));
                l.Add(new FunctionResolver(">=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<T, int> Left, Func<T, int> Right) => (Func<T, Boolean>)(t => Left(t) >= Right(t))));
                l.Add(new FunctionResolver("==", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<T, int> Left, Func<T, int> Right) => (Func<T, Boolean>)(t => Left(t) == Right(t))));
                l.Add(new FunctionResolver("!=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<T, int> Left, Func<T, int> Right) => (Func<T, Boolean>)(t => Left(t) != Right(t))));
                l.Add(new FunctionResolver("<", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<T, double> Left, Func<T, double> Right) => (Func<T, Boolean>)(t => Left(t) < Right(t))));
                l.Add(new FunctionResolver(">", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<T, double> Left, Func<T, double> Right) => (Func<T, Boolean>)(t => Left(t) > Right(t))));
                l.Add(new FunctionResolver("<=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<T, double> Left, Func<T, double> Right) => (Func<T, Boolean>)(t => Left(t) <= Right(t))));
                l.Add(new FunctionResolver(">=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<T, double> Left, Func<T, double> Right) => (Func<T, Boolean>)(t => Left(t) >= Right(t))));
                l.Add(new FunctionResolver("==", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean, (Func<T, Boolean> Left, Func<T, Boolean> Right) => (Func<T, Boolean>)(t => Left(t) == Right(t))));
                l.Add(new FunctionResolver("!=", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean, (Func<T, Boolean> Left, Func<T, Boolean> Right) => (Func<T, Boolean>)(t => Left(t) != Right(t))));

                //取整运算
                l.Add(new FunctionResolver("round", PrimitiveType.Real, PrimitiveType.Int, (Func<T, double> Operand) => (Func<T, int>)(t => ExpressionRuntime.round(Operand(t)))));
                l.Add(new FunctionResolver("floor", PrimitiveType.Real, PrimitiveType.Int, (Func<T, double> Operand) => (Func<T, int>)(t => ExpressionRuntime.floor(Operand(t)))));
                l.Add(new FunctionResolver("ceil", PrimitiveType.Real, PrimitiveType.Int, (Func<T, double> Operand) => (Func<T, int>)(t => ExpressionRuntime.ceil(Operand(t)))));
                l.Add(new FunctionResolver("round", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real, (Func<T, double> Left, Func<T, int> Right) => (Func<T, double>)(t => ExpressionRuntime.round(Left(t), Right(t)))));
                l.Add(new FunctionResolver("floor", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real, (Func<T, double> Left, Func<T, int> Right) => (Func<T, double>)(t => ExpressionRuntime.floor(Left(t), Right(t)))));
                l.Add(new FunctionResolver("ceil", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real, (Func<T, double> Left, Func<T, int> Right) => (Func<T, double>)(t => ExpressionRuntime.ceil(Left(t), Right(t)))));

                //范围限制运算
                l.Add(new FunctionResolver("min", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => ExpressionRuntime.min(Left(t), Right(t)))));
                l.Add(new FunctionResolver("max", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Left, Func<T, int> Right) => (Func<T, int>)(t => ExpressionRuntime.max(Left(t), Right(t)))));
                l.Add(new FunctionResolver("clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Arg0, Func<T, int> Arg1, Func<T, int> Arg2) => (Func<T, int>)(t => ExpressionRuntime.clamp(Arg0(t), Arg1(t), Arg2(t)))));
                l.Add(new FunctionResolver("min", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => ExpressionRuntime.min(Left(t), Right(t)))));
                l.Add(new FunctionResolver("max", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Left, Func<T, double> Right) => (Func<T, double>)(t => ExpressionRuntime.max(Left(t), Right(t)))));
                l.Add(new FunctionResolver("clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Arg0, Func<T, double> Arg1, Func<T, double> Arg2) => (Func<T, double>)(t => ExpressionRuntime.clamp(Arg0(t), Arg1(t), Arg2(t)))));

                //其他运算
                l.Add(new FunctionResolver("abs", PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Operand) => (Func<T, int>)(t => ExpressionRuntime.abs(Operand(t)))));
                l.Add(new FunctionResolver("abs", PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Operand) => (Func<T, double>)(t => ExpressionRuntime.abs(Operand(t)))));
                l.Add(new FunctionResolver("rand", PrimitiveType.Real, () => (Func<T, double>)(t => ExpressionRuntime.rand())));
                l.Add(new FunctionResolver("rand", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<T, int> Arg0, Func<T, int> Arg1) => (Func<T, int>)(t => ExpressionRuntime.rand(Arg0(t), Arg1(t)))));
                l.Add(new FunctionResolver("rand", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<T, double> Arg0, Func<T, double> Arg1) => (Func<T, double>)(t => ExpressionRuntime.rand(Arg0(t), Arg1(t)))));
                l.Add(new FunctionResolver("creal", PrimitiveType.Int, PrimitiveType.Real, (Func<T, int> Operand) => (Func<T, double>)(t => Operand(t))));

                Map = new Dictionary<String, List<FunctionResolver>>();
                foreach (var fs in l)
                {
                    if (Map.ContainsKey(fs.Name))
                    {
                        Map[fs.Name].Add(fs);
                    }
                    else
                    {
                        Map.Add(fs.Name, new List<FunctionResolver> { fs });
                    }
                }
            }
        }

        public FunctionParameterAndReturnTypes[] GetOverloads(string Name)
        {
            if (FunctionSignatureMap.Map.ContainsKey(Name)) { return FunctionSignatureMap.Map[Name].Select(f => new FunctionParameterAndReturnTypes { ParameterTypes = f.ParameterTypes, ReturnType = f.ReturnType }).ToArray(); }
            return new FunctionParameterAndReturnTypes[] { };
        }

        public PrimitiveType[] GetMatched(string Name, PrimitiveType[] ParameterTypes)
        {
            if (FunctionSignatureMap.Map.ContainsKey(Name))
            {
                var Matched = FunctionSignatureMap.Map[Name].Where(f => f.ParameterTypes.SequenceEqual(ParameterTypes)).Select(f => f.ReturnType).ToArray();
                return Matched;
            }
            return new PrimitiveType[] { };
        }

        public Delegate[] GetValue(String Name, PrimitiveType[] ParameterTypes, Delegate[] ParameterFuncs)
        {
            if (FunctionSignatureMap.Map.ContainsKey(Name))
            {
                var Matched = FunctionSignatureMap.Map[Name].Where(f => f.ParameterTypes.SequenceEqual(ParameterTypes)).Select(f => f.Create.StaticDynamicInvokeWithObjects<Delegate>(ParameterFuncs)).ToArray();
                return Matched;
            }
            return new Delegate[] { };
        }
    }
}
