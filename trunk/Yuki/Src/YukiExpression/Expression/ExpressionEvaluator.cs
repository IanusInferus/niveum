//==========================================================================
//
//  File:        ExpressionEvaluator.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式求值工具
//  Version:     2013.03.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Yuki.ExpressionSchema;

namespace Yuki.Expression
{
    public interface IVariableProvider : IVariableTypeProvider
    {
        Delegate[] GetValue(String Name, PrimitiveType[] ParameterTypes, Delegate[] Parameters);
    }

    public class VariableProviderCombiner : IVariableProvider
    {
        private IVariableProvider[] Providers;

        public VariableProviderCombiner(params IVariableProvider[] Providers)
        {
            this.Providers = Providers;
        }

        public FunctionParameterAndReturnTypes[] GetOverloads(String Name)
        {
            return Providers.SelectMany(p => p.GetOverloads(Name)).ToArray();
        }

        public PrimitiveType[] GetMatched(String Name, PrimitiveType[] ParameterTypes)
        {
            return Providers.SelectMany(p => p.GetMatched(Name, ParameterTypes)).ToArray();
        }

        public Delegate[] GetValue(String Name, PrimitiveType[] ParameterTypes, Delegate[] Parameters)
        {
            return Providers.SelectMany(p => p.GetValue(Name, ParameterTypes, Parameters)).ToArray();
        }
    }

    public class ExpressionEvaluator
    {
        public static Delegate Compile(IVariableProvider VariableProvider, Expr Expr)
        {
            var ee = new ExpressionEvaluator(VariableProvider, Expr, new Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange>(), new Dictionary<Expr, PrimitiveType>());
            return ee.Compile();
        }
        public static Delegate Compile(IVariableProvider VariableProvider, ExpressionParserExprResult Expr)
        {
            var ee = new ExpressionEvaluator(VariableProvider, Expr.Body, Expr.Positions, Expr.TypeDict);
            return ee.Compile();
        }
        public static Func<T> Compile<T>(IVariableProvider VariableProvider, Expr Expr)
        {
            var ee = new ExpressionEvaluator(VariableProvider, Expr, new Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange>(), new Dictionary<Expr, PrimitiveType>());
            return ee.Compile<T>();
        }
        public static Func<T> Compile<T>(IVariableProvider VariableProvider, ExpressionParserExprResult Expr)
        {
            var ee = new ExpressionEvaluator(VariableProvider, Expr.Body, Expr.Positions, Expr.TypeDict);
            return ee.Compile<T>();
        }

        private IVariableProvider VariableProvider;
        private Expr Expr;
        private Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange> Positions;
        private Dictionary<Expr, PrimitiveType> TypeDict;
        private ExpressionEvaluator(IVariableProvider VariableProvider, Expr Expr, Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange> Positions, Dictionary<Expr, PrimitiveType> TypeDict)
        {
            this.VariableProvider = VariableProvider;
            this.Expr = Expr;
            this.Positions = Positions;
            this.TypeDict = TypeDict;
        }

        private Delegate Compile()
        {
            return BuildExpr(Expr);
        }

        private Func<T> Compile<T>()
        {
            return (Func<T>)(BuildExpr(Expr));
        }

        private Delegate BuildExpr(Expr e)
        {
            if (e.OnLiteral)
            {
                if (e.Literal.OnBooleanValue)
                {
                    var v = e.Literal.BooleanValue;
                    return (Func<Boolean>)(() => v);
                }
                else if (e.Literal.OnIntValue)
                {
                    var v = e.Literal.IntValue;
                    return (Func<int>)(() => v);
                }
                else if (e.Literal.OnRealValue)
                {
                    var v = e.Literal.RealValue;
                    return (Func<double>)(() => v);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (e.OnVariable)
            {
                var Name = e.Variable.Name;
                var f = VariableProvider.GetValue(Name, null, null);
                if (f.Length == 0)
                {
                    throw new InvalidOperationException(String.Format("VariableNotExist: {0}", Name));
                }
                else if (f.Length > 1)
                {
                    throw new InvalidOperationException(String.Format("MultipleVariableExist: {0}", Name));
                }
                return f.Single();
            }
            else if (e.OnFunction)
            {
                var Name = e.Function.Name;
                var ParameterFuncs = e.Function.Parameters.Select(p => BuildExpr(p)).ToArray();
                var f = VariableProvider.GetValue(Name, ParameterFuncs.Select(pf => GetReturnType(pf)).ToArray(), ParameterFuncs);
                if (f.Length == 0)
                {
                    throw new InvalidOperationException(String.Format("FunctionNotExist: {0}", Name));
                }
                else if (f.Length > 1)
                {
                    throw new InvalidOperationException(String.Format("MultipleFunctionExist: {0}", Name));
                }
                return f.Single();
            }
            else if (e.OnIf)
            {
                var l = BuildExpr(e.If.TruePart);
                var r = BuildExpr(e.If.FalsePart);
                if (l.GetType() != r.GetType())
                {
                    throw new InvalidOperationException();
                }
                var Condition = (Func<Boolean>)(BuildExpr(e.If.Condition));
                var t = l.GetType();
                if (t == typeof(Func<Boolean>))
                {
                    var Left = (Func<Boolean>)(l);
                    var Right = (Func<Boolean>)(r);
                    return (Func<Boolean>)(() => Condition() ? Left() : Right());
                }
                if (t == typeof(Func<int>))
                {
                    var Left = (Func<int>)(l);
                    var Right = (Func<int>)(r);
                    return (Func<int>)(() => Condition() ? Left() : Right());
                }
                if (t == typeof(Func<double>))
                {
                    var Left = (Func<double>)(l);
                    var Right = (Func<double>)(r);
                    return (Func<double>)(() => Condition() ? Left() : Right());
                }
                throw new InvalidOperationException();
            }
            else if (e.OnAndAlso)
            {
                var Left = (Func<Boolean>)(BuildExpr(e.AndAlso.Left));
                var Right = (Func<Boolean>)(BuildExpr(e.AndAlso.Right));
                return (Func<Boolean>)(() => Left() && Right());
            }
            else if (e.OnOrElse)
            {
                var Left = (Func<Boolean>)(BuildExpr(e.OrElse.Left));
                var Right = (Func<Boolean>)(BuildExpr(e.OrElse.Right));
                return (Func<Boolean>)(() => Left() || Right());
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static PrimitiveType GetReturnType(Delegate ParameterFunc)
        {
            if (ParameterFunc.GetType() == typeof(Func<Boolean>))
            {
                return PrimitiveType.Boolean;
            }
            if (ParameterFunc.GetType() == typeof(Func<int>))
            {
                return PrimitiveType.Int;
            }
            if (ParameterFunc.GetType() == typeof(Func<double>))
            {
                return PrimitiveType.Real;
            }
            throw new InvalidOperationException();
        }
    }
}
