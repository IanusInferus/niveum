//==========================================================================
//
//  File:        ExpressionRuntime.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式系统库
//  Version:     2013.03.13.
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

    public class ExpressionRuntimeProvider : IVariableTypeProvider, IVariableProvider
    {
        private class FunctionResolver
        {
            public String Name;
            public PrimitiveType[] Types;
            public Delegate Create;

            public FunctionResolver()
            {
            }
            public FunctionResolver(String Name, PrimitiveType rt, Func<Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType rt, Func<Func<Boolean>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType rt, Func<Func<int>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType rt, Func<Func<double>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<Boolean>, Func<Boolean>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<int>, Func<int>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<double>, Func<double>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType rt, Func<Func<double>, Func<int>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt, Func<Func<int>, Func<int>, Func<int>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, pt2, rt };
                this.Create = Create;
            }
            public FunctionResolver(String Name, PrimitiveType pt0, PrimitiveType pt1, PrimitiveType pt2, PrimitiveType rt, Func<Func<double>, Func<double>, Func<double>, Delegate> Create)
            {
                this.Name = Name;
                this.Types = new PrimitiveType[] { pt0, pt1, pt2, rt };
                this.Create = Create;
            }
        }

        private static class FunctionSignatureMap
        {
            public static Dictionary<String, List<FunctionResolver>> Map;

            static FunctionSignatureMap()
            {
                var l = new List<FunctionResolver>();

                //算术运算
                l.Add(new FunctionResolver("+", PrimitiveType.Int, PrimitiveType.Int, (Func<int> Operand) => (Func<int>)(() => +Operand())));
                l.Add(new FunctionResolver("-", PrimitiveType.Int, PrimitiveType.Int, (Func<int> Operand) => (Func<int>)(() => -Operand())));
                l.Add(new FunctionResolver("+", PrimitiveType.Real, PrimitiveType.Real, (Func<double> Operand) => (Func<double>)(() => +Operand())));
                l.Add(new FunctionResolver("-", PrimitiveType.Real, PrimitiveType.Real, (Func<double> Operand) => (Func<double>)(() => -Operand())));
                l.Add(new FunctionResolver("+", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => Left() + Right())));
                l.Add(new FunctionResolver("-", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => Left() - Right())));
                l.Add(new FunctionResolver("*", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => Left() * Right())));
                l.Add(new FunctionResolver("/", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Real, (Func<int> Left, Func<int> Right) => (Func<double>)(() => (double)(Left()) / (double)(Right()))));
                l.Add(new FunctionResolver("+", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => Left() + Right())));
                l.Add(new FunctionResolver("-", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => Left() - Right())));
                l.Add(new FunctionResolver("*", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => Left() * Right())));
                l.Add(new FunctionResolver("/", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => Left() / Right())));
                l.Add(new FunctionResolver("pow", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => ExpressionRuntime.pow(Left(), Right()))));
                l.Add(new FunctionResolver("pow", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => ExpressionRuntime.pow(Left(), Right()))));
                l.Add(new FunctionResolver("mod", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => ExpressionRuntime.mod(Left(), Right()))));
                l.Add(new FunctionResolver("div", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => ExpressionRuntime.div(Left(), Right()))));

                //逻辑运算
                l.Add(new FunctionResolver("!", PrimitiveType.Boolean, PrimitiveType.Boolean, (Func<Boolean> Operand) => (Func<Boolean>)(() => !Operand())));

                //关系运算
                l.Add(new FunctionResolver("<", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<int> Left, Func<int> Right) => (Func<Boolean>)(() => Left() < Right())));
                l.Add(new FunctionResolver(">", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<int> Left, Func<int> Right) => (Func<Boolean>)(() => Left() > Right())));
                l.Add(new FunctionResolver("<=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<int> Left, Func<int> Right) => (Func<Boolean>)(() => Left() <= Right())));
                l.Add(new FunctionResolver(">=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<int> Left, Func<int> Right) => (Func<Boolean>)(() => Left() >= Right())));
                l.Add(new FunctionResolver("==", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<int> Left, Func<int> Right) => (Func<Boolean>)(() => Left() == Right())));
                l.Add(new FunctionResolver("!=", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Boolean, (Func<int> Left, Func<int> Right) => (Func<Boolean>)(() => Left() != Right())));
                l.Add(new FunctionResolver("<", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<double> Left, Func<double> Right) => (Func<Boolean>)(() => Left() < Right())));
                l.Add(new FunctionResolver(">", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<double> Left, Func<double> Right) => (Func<Boolean>)(() => Left() > Right())));
                l.Add(new FunctionResolver("<=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<double> Left, Func<double> Right) => (Func<Boolean>)(() => Left() <= Right())));
                l.Add(new FunctionResolver(">=", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Boolean, (Func<double> Left, Func<double> Right) => (Func<Boolean>)(() => Left() >= Right())));
                l.Add(new FunctionResolver("==", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean, (Func<Boolean> Left, Func<Boolean> Right) => (Func<Boolean>)(() => Left() == Right())));
                l.Add(new FunctionResolver("!=", PrimitiveType.Boolean, PrimitiveType.Boolean, PrimitiveType.Boolean, (Func<Boolean> Left, Func<Boolean> Right) => (Func<Boolean>)(() => Left() != Right())));

                //取整运算
                l.Add(new FunctionResolver("round", PrimitiveType.Real, PrimitiveType.Int, (Func<double> Operand) => (Func<int>)(() => ExpressionRuntime.round(Operand()))));
                l.Add(new FunctionResolver("floor", PrimitiveType.Real, PrimitiveType.Int, (Func<double> Operand) => (Func<int>)(() => ExpressionRuntime.floor(Operand()))));
                l.Add(new FunctionResolver("ceil", PrimitiveType.Real, PrimitiveType.Int, (Func<double> Operand) => (Func<int>)(() => ExpressionRuntime.ceil(Operand()))));
                l.Add(new FunctionResolver("round", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real, (Func<double> Left, Func<int> Right) => (Func<double>)(() => ExpressionRuntime.round(Left(), Right()))));
                l.Add(new FunctionResolver("floor", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real, (Func<double> Left, Func<int> Right) => (Func<double>)(() => ExpressionRuntime.floor(Left(), Right()))));
                l.Add(new FunctionResolver("ceil", PrimitiveType.Real, PrimitiveType.Int, PrimitiveType.Real, (Func<double> Left, Func<int> Right) => (Func<double>)(() => ExpressionRuntime.ceil(Left(), Right()))));

                //范围限制运算
                l.Add(new FunctionResolver("min", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => ExpressionRuntime.min(Left(), Right()))));
                l.Add(new FunctionResolver("max", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Left, Func<int> Right) => (Func<int>)(() => ExpressionRuntime.max(Left(), Right()))));
                l.Add(new FunctionResolver("clamp", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Arg0, Func<int> Arg1, Func<int> Arg2) => (Func<int>)(() => ExpressionRuntime.clamp(Arg0(), Arg1(), Arg2()))));
                l.Add(new FunctionResolver("min", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => ExpressionRuntime.min(Left(), Right()))));
                l.Add(new FunctionResolver("max", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Left, Func<double> Right) => (Func<double>)(() => ExpressionRuntime.max(Left(), Right()))));
                l.Add(new FunctionResolver("clamp", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Arg0, Func<double> Arg1, Func<double> Arg2) => (Func<double>)(() => ExpressionRuntime.clamp(Arg0(), Arg1(), Arg2()))));

                //其他运算
                l.Add(new FunctionResolver("abs", PrimitiveType.Int, PrimitiveType.Int, (Func<int> Operand) => (Func<int>)(() => ExpressionRuntime.abs(Operand()))));
                l.Add(new FunctionResolver("abs", PrimitiveType.Real, PrimitiveType.Real, (Func<double> Operand) => (Func<double>)(() => ExpressionRuntime.abs(Operand()))));
                l.Add(new FunctionResolver("rand", PrimitiveType.Real, () => (Func<double>)(() => ExpressionRuntime.rand())));
                l.Add(new FunctionResolver("rand", PrimitiveType.Int, PrimitiveType.Int, PrimitiveType.Int, (Func<int> Arg0, Func<int> Arg1) => (Func<int>)(() => ExpressionRuntime.rand(Arg0(), Arg1()))));
                l.Add(new FunctionResolver("rand", PrimitiveType.Real, PrimitiveType.Real, PrimitiveType.Real, (Func<double> Arg0, Func<double> Arg1) => (Func<double>)(() => ExpressionRuntime.rand(Arg0(), Arg1()))));
                l.Add(new FunctionResolver("creal", PrimitiveType.Int, PrimitiveType.Real, (Func<int> Operand) => (Func<double>)(() => Operand())));

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

        public Delegate[] GetValue(String Name, PrimitiveType[] ParameterTypes, Delegate[] ParameterFuncs)
        {
            if (FunctionSignatureMap.Map.ContainsKey(Name))
            {
                var Matched = FunctionSignatureMap.Map[Name].Where(f => f.Types.Take(f.Types.Length - 1).SequenceEqual(ParameterTypes)).Select(f => f.Create.StaticDynamicInvokeWithObjects<Delegate>(ParameterFuncs)).ToArray();
                return Matched;
            }
            return new Delegate[] { };
        }
    }
}
