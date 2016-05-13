//==========================================================================
//
//  File:        VariableContext.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 默认变量上下文
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yuki.ExpressionSchema;

namespace Yuki.Expression
{
    /// <summary>
    /// 本类的GetValue函数是线程安全的。
    /// </summary>
    public class VariableContext<T> : IVariableProvider<T>
    {
        private class Variable
        {
            public Yuki.ExpressionSchema.VariableDef[] Parameters;
            public PrimitiveType ReturnType;
            public Func<VariableContext<T>, Delegate> Create;
        }

        private Dictionary<String, List<Variable>> Dict = new Dictionary<String, List<Variable>>(); //只读时是线程安全的
        public VariableContext()
        {
        }

        private List<PrimitiveType> GetParameterTypes(Variable d)
        {
            if (d.Parameters == null) { return null; }
            var l = d.Parameters.Select(p => p.Type).ToList();
            return l;
        }
        private PrimitiveType GetReturnType(Variable d)
        {
            return d.ReturnType;
        }
        private FunctionParameterAndReturnTypes GetParameterAndReturnTypes(Variable d)
        {
            return new FunctionParameterAndReturnTypes { ParameterTypes = GetParameterTypes(d), ReturnType = GetReturnType(d) };
        }
        private Boolean NullableSequenceEqual<E>(IEnumerable<E> Left, IEnumerable<E> Right)
        {
            if (Left == null && Right == null) { return true; }
            if (Left == null || Right == null) { return false; }
            return Left.SequenceEqual(Right);
        }

        public List<FunctionParameterAndReturnTypes> GetOverloads(String Name)
        {
            var l = new List<FunctionParameterAndReturnTypes>();
            if (Dict.ContainsKey(Name))
            {
                foreach (var v in Dict[Name])
                {
                    l.Add(GetParameterAndReturnTypes(v));
                }
            }
            return l;
        }

        public List<PrimitiveType> GetMatched(String Name, List<PrimitiveType> ParameterTypes)
        {
            var l = new List<PrimitiveType>();
            if (Dict.ContainsKey(Name))
            {
                foreach (var v in Dict[Name])
                {
                    var vParameterTypes = GetParameterTypes(v);
                    if (NullableSequenceEqual(vParameterTypes, ParameterTypes))
                    {
                        l.Add(GetReturnType(v));
                    }
                }
            }
            return l;
        }

        public void Replace(String Name, Object v)
        {
            var type = v.GetType();
            if (type == typeof(Boolean))
            {
                var vv = (Boolean)(v);
                Replace(Name, null, PrimitiveType.Boolean, vc => (Func<T, Boolean>)(t => vv));
            }
            else if (type == typeof(int))
            {
                var vv = (int)(v);
                Replace(Name, null, PrimitiveType.Int, vc => (Func<T, int>)(t => vv));
            }
            else if (type == typeof(double))
            {
                var vv = (double)(v);
                Replace(Name, null, PrimitiveType.Real, vc => (Func<T, double>)(t => vv));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public void Replace(String Name, FunctionDef Definition)
        {
            Func<IVariableProvider<T>, Delegate> d = vc => ExpressionEvaluator<T>.Compile(new VariableProviderCombiner<T>(vc, new ExpressionRuntimeProvider<T>()), Definition.Body);
            Replace(Name, Definition.Parameters.ToArray(), Definition.ReturnValue, d);
        }
        public void Replace(String Name, Yuki.ExpressionSchema.VariableDef[] Parameters, PrimitiveType ReturnValue, Func<IVariableProvider<T>, Delegate> Create)
        {
            var nv = new Variable
            {
                Parameters = Parameters,
                ReturnType = ReturnValue,
                Create = Create
            };
            if (Dict.ContainsKey(Name))
            {
                var l = Dict[Name];
                for (int k = 0; k < l.Count; k += 1)
                {
                    var v = l[k];
                    var vParameterTypes = GetParameterTypes(v);
                    if (NullableSequenceEqual(vParameterTypes, Parameters == null ? null : Parameters.Select(p => p.Type)))
                    {
                        l[k] = nv;
                        return;
                    }
                }
                l.Add(nv);
            }
            else
            {
                var l = new List<Variable>();
                l.Add(nv);
                Dict.Add(Name, l);
            }
        }
        public void TryRemove(String Name, PrimitiveType[] ParameterTypes)
        {
            if (Dict.ContainsKey(Name))
            {
                var l = Dict[Name];
                var Removed = new List<Variable>();
                foreach (var v in l)
                {
                    var vParameterTypes = GetParameterTypes(v);
                    if (NullableSequenceEqual(vParameterTypes, ParameterTypes))
                    {
                        Removed.Add(v);
                    }
                }
                l = l.Except(Removed).ToList();
                if (l.Count == 0)
                {
                    Dict.Remove(Name);
                }
                else
                {
                    Dict[Name] = l;
                }
            }
        }

        public List<Delegate> GetValue(String Name, List<PrimitiveType> ParameterTypes, List<Delegate> Parameters)
        {
            var l = new List<Delegate>();
            if (Dict.ContainsKey(Name))
            {
                foreach (var v in Dict[Name])
                {
                    var vParameterTypes = GetParameterTypes(v);
                    if (vParameterTypes == null && ParameterTypes == null)
                    {
                        l.Add(v.Create(null));
                    }
                    else if (vParameterTypes != null && ParameterTypes != null && vParameterTypes.SequenceEqual(ParameterTypes))
                    {
                        if (v.Parameters.Select(p => p.Type).SequenceEqual(ParameterTypes))
                        {
                            var vc = new VariableContext<T>();
                            for (int k = 0; k < v.Parameters.Length; k += 1)
                            {
                                var p = v.Parameters[k];
                                var pp = Parameters[k];
                                vc.Replace(p.Name, null, p.Type, vvc => pp);
                            }
                            l.Add(v.Create(vc));
                        }
                    }
                }
            }
            return l;
        }
    }
}
