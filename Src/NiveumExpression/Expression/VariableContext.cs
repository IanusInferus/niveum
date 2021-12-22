//==========================================================================
//
//  File:        VariableContext.cs
//  Location:    Niveum.Expression <Visual C#>
//  Description: 默认变量上下文
//  Version:     2021.12.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable
#pragma warning disable CS8618

using System;
using System.Collections.Generic;
using System.Linq;
using Niveum.ExpressionSchema;

namespace Niveum.Expression
{
    /// <summary>
    /// 本类的GetValue函数是线程安全的。
    /// </summary>
    public class VariableContext<T> : IVariableProvider<T>
    {
        private sealed class Variable
        {
            public Optional<List<Niveum.ExpressionSchema.VariableDef>> Parameters { get; init; }
            public PrimitiveType ReturnType { get; init; }
            public Func<VariableContext<T>, Delegate> Create { get; init; }
        }

        private Dictionary<String, List<Variable>> Dict = new Dictionary<String, List<Variable>>(); //只读时是线程安全的
        public VariableContext()
        {
        }

        private Optional<List<PrimitiveType>> GetParameterTypes(Variable d)
        {
            if (d.Parameters.OnNone) { return Optional<List<PrimitiveType>>.Empty; }
            var l = d.Parameters.Value.Select(p => p.Type).ToList();
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
        private Boolean NullableListEqual<E>(Optional<List<E>> Left, Optional<List<E>> Right)
        {
            if (Left.OnNone && Right.OnNone) { return true; }
            if (Left.OnNone || Right.OnNone) { return false; }
            return Left.Value.SequenceEqual(Right.Value);
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

        public List<PrimitiveType> GetMatched(String Name, Optional<List<PrimitiveType>> ParameterTypes)
        {
            var l = new List<PrimitiveType>();
            if (Dict.ContainsKey(Name))
            {
                foreach (var v in Dict[Name])
                {
                    var vParameterTypes = GetParameterTypes(v);
                    if (NullableListEqual(vParameterTypes, ParameterTypes))
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
                Replace(Name, Optional<List<VariableDef>>.Empty, PrimitiveType.Boolean, vc => (Func<T, Boolean>)(t => vv));
            }
            else if (type == typeof(int))
            {
                var vv = (int)(v);
                Replace(Name, Optional<List<VariableDef>>.Empty, PrimitiveType.Int, vc => (Func<T, int>)(t => vv));
            }
            else if (type == typeof(double))
            {
                var vv = (double)(v);
                Replace(Name, Optional<List<VariableDef>>.Empty, PrimitiveType.Real, vc => (Func<T, double>)(t => vv));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public void Replace(String Name, FunctionDef Definition)
        {
            Func<IVariableProvider<T>, Delegate> d = vc => ExpressionEvaluator<T>.Compile(new VariableProviderCombiner<T>(vc, new ExpressionRuntimeProvider<T>()), Definition.Body);
            Replace(Name, Definition.Parameters, Definition.ReturnValue, d);
        }
        public void Replace(String Name, Optional<List<VariableDef>> Parameters, PrimitiveType ReturnValue, Func<IVariableProvider<T>, Delegate> Create)
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
                    if (NullableListEqual(vParameterTypes, Parameters.OnNone ? Optional<List<PrimitiveType>>.Empty : Parameters.Value.Select(p => p.Type).ToList()))
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
        public void TryRemove(String Name, Optional<List<PrimitiveType>> ParameterTypes)
        {
            if (Dict.ContainsKey(Name))
            {
                var l = Dict[Name];
                var Removed = new List<Variable>();
                foreach (var v in l)
                {
                    var vParameterTypes = GetParameterTypes(v);
                    if (NullableListEqual(vParameterTypes, ParameterTypes))
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

        public List<Delegate> GetValue(String Name, Optional<List<PrimitiveType>> ParameterTypes, Optional<List<Delegate>> Parameters)
        {
            var l = new List<Delegate>();
            if (Dict.ContainsKey(Name))
            {
                foreach (var v in Dict[Name])
                {
                    var vParameterTypes = GetParameterTypes(v);
                    if (vParameterTypes.OnNone && ParameterTypes.OnNone)
                    {
                        var vc = new VariableContext<T>();
                        l.Add(v.Create(vc));
                    }
                    else if (vParameterTypes.OnSome && ParameterTypes.OnSome && vParameterTypes.Value.SequenceEqual(ParameterTypes.Value))
                    {
                        if (v.Parameters.Value.Select(p => p.Type).SequenceEqual(ParameterTypes.Value))
                        {
                            var vc = new VariableContext<T>();
                            for (int k = 0; k < v.Parameters.Value.Count; k += 1)
                            {
                                var p = v.Parameters.Value[k];
                                var pp = Parameters.Value[k];
                                vc.Replace(p.Name, Optional<List<VariableDef>>.Empty, p.Type, vvc => pp);
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
