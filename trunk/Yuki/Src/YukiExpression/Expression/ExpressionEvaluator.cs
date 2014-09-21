//==========================================================================
//
//  File:        ExpressionEvaluator.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 表达式求值工具
//  Version:     2014.09.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Yuki.ExpressionSchema;

namespace Yuki.Expression
{
    public interface IVariableProvider<T> : IVariableTypeProvider
    {
        Delegate[] GetValue(String Name, PrimitiveType[] ParameterTypes, Delegate[] Parameters);
    }

    /// <summary>
    /// 本类是线程安全的。
    /// </summary>
    public class VariableProviderCombiner<T> : IVariableProvider<T>
    {
        private IVariableProvider<T>[] Providers;

        public VariableProviderCombiner(params IVariableProvider<T>[] Providers)
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

    public class ExpressionEvaluator<T>
    {
        public static Delegate Compile(IVariableProvider<T> VariableProvider, Expr Expr)
        {
            var ee = new ExpressionEvaluator<T>(VariableProvider, Expr, new Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange>(), new Dictionary<Expr, PrimitiveType>());
            return ee.Compile();
        }
        public static Delegate Compile(IVariableProvider<T> VariableProvider, ExpressionParserExprResult Expr)
        {
            var ee = new ExpressionEvaluator<T>(VariableProvider, Expr.Body, Expr.Positions, Expr.TypeDict);
            return ee.Compile();
        }
        public static Func<T, TReturn> Compile<TReturn>(IVariableProvider<T> VariableProvider, Expr Expr)
        {
            var ee = new ExpressionEvaluator<T>(VariableProvider, Expr, new Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange>(), new Dictionary<Expr, PrimitiveType>());
            return ee.Compile<TReturn>();
        }
        public static Func<T, TReturn> Compile<TReturn>(IVariableProvider<T> VariableProvider, ExpressionParserExprResult Expr)
        {
            var ee = new ExpressionEvaluator<T>(VariableProvider, Expr.Body, Expr.Positions, Expr.TypeDict);
            return ee.Compile<TReturn>();
        }

        private IVariableProvider<T> VariableProvider;
        private Expr Expr;
        private Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange> Positions;
        private Dictionary<Expr, PrimitiveType> TypeDict;
        private ExpressionEvaluator(IVariableProvider<T> VariableProvider, Expr Expr, Dictionary<Object, Firefly.Texting.TreeFormat.Syntax.TextRange> Positions, Dictionary<Expr, PrimitiveType> TypeDict)
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

        private Func<T, TReturn> Compile<TReturn>()
        {
            return (Func<T, TReturn>)(BuildExpr(Expr));
        }

        private Delegate BuildExpr(Expr e)
        {
            if (e.OnLiteral)
            {
                if (e.Literal.OnBooleanValue)
                {
                    var v = e.Literal.BooleanValue;
                    return (Func<T, Boolean>)(t => v);
                }
                else if (e.Literal.OnIntValue)
                {
                    var v = e.Literal.IntValue;
                    return (Func<T, int>)(t => v);
                }
                else if (e.Literal.OnRealValue)
                {
                    var v = e.Literal.RealValue;
                    return (Func<T, double>)(t => v);
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
                var Condition = (Func<T, Boolean>)(BuildExpr(e.If.Condition));
                var type = l.GetType();
                if (type == typeof(Func<T, Boolean>))
                {
                    var Left = (Func<T, Boolean>)(l);
                    var Right = (Func<T, Boolean>)(r);
                    return (Func<T, Boolean>)(t => Condition(t) ? Left(t) : Right(t));
                }
                if (type == typeof(Func<T, int>))
                {
                    var Left = (Func<T, int>)(l);
                    var Right = (Func<T, int>)(r);
                    return (Func<T, int>)(t => Condition(t) ? Left(t) : Right(t));
                }
                if (type == typeof(Func<T, double>))
                {
                    var Left = (Func<T, double>)(l);
                    var Right = (Func<T, double>)(r);
                    return (Func<T, double>)(t => Condition(t) ? Left(t) : Right(t));
                }
                throw new InvalidOperationException();
            }
            else if (e.OnAndAlso)
            {
                var Left = (Func<T, Boolean>)(BuildExpr(e.AndAlso.Left));
                var Right = (Func<T, Boolean>)(BuildExpr(e.AndAlso.Right));
                return (Func<T, Boolean>)(t => Left(t) && Right(t));
            }
            else if (e.OnOrElse)
            {
                var Left = (Func<T, Boolean>)(BuildExpr(e.OrElse.Left));
                var Right = (Func<T, Boolean>)(BuildExpr(e.OrElse.Right));
                return (Func<T, Boolean>)(t => Left(t) || Right(t));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static PrimitiveType GetReturnType(Delegate ParameterFunc)
        {
            if (ParameterFunc.GetType() == typeof(Func<T, Boolean>))
            {
                return PrimitiveType.Boolean;
            }
            if (ParameterFunc.GetType() == typeof(Func<T, int>))
            {
                return PrimitiveType.Int;
            }
            if (ParameterFunc.GetType() == typeof(Func<T, double>))
            {
                return PrimitiveType.Real;
            }
            throw new InvalidOperationException();
        }
    }
}
