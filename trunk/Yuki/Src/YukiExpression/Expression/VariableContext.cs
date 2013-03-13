//==========================================================================
//
//  File:        VariableContext.cs
//  Location:    Yuki.Expression <Visual C#>
//  Description: 默认变量上下文
//  Version:     2013.03.13.
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
    public class VariableContext : IVariableProvider
    {
        private class Variable
        {
            public Yuki.ExpressionSchema.VariableDef[] Parameters;
            public PrimitiveType ReturnType;
            public Func<VariableContext, Delegate> Create;
        }

        private Dictionary<String, List<Variable>> Dict = new Dictionary<String, List<Variable>>();
        public VariableContext()
        {
        }

        private PrimitiveType[] GetParameterTypes(Variable d)
        {
            if (d.Parameters == null) { return null; }
            var l = d.Parameters.Select(p => p.Type).ToArray();
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
        private Boolean NullableSequenceEqual<T>(IEnumerable<T> Left, IEnumerable<T> Right)
        {
            if (Left == null && Right == null) { return true; }
            if (Left == null || Right == null) { return false; }
            return Left.SequenceEqual(Right);
        }

        public FunctionParameterAndReturnTypes[] GetOverloads(String Name)
        {
            var l = new List<FunctionParameterAndReturnTypes>();
            if (Dict.ContainsKey(Name))
            {
                foreach (var v in Dict[Name])
                {
                    l.Add(GetParameterAndReturnTypes(v));
                }
            }
            return l.ToArray();
        }

        public PrimitiveType[] GetMatched(String Name, PrimitiveType[] ParameterTypes)
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
            return l.ToArray();
        }

        public void Replace(String Name, Object v)
        {
            var t = v.GetType();
            if (t == typeof(Boolean))
            {
                var vv = (Boolean)(v);
                Replace(Name, null, PrimitiveType.Boolean, vc => (Func<Boolean>)(() => vv));
            }
            else if (t == typeof(int))
            {
                var vv = (int)(v);
                Replace(Name, null, PrimitiveType.Int, vc => (Func<int>)(() => vv));
            }
            else if (t == typeof(double))
            {
                var vv = (double)(v);
                Replace(Name, null, PrimitiveType.Real, vc => (Func<double>)(() => vv));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public void Replace(String Name, FunctionDef Definition)
        {
            Func<VariableContext, Delegate> d = vc => ExpressionEvaluator.Compile(new VariableProviderCombiner(vc, new ExpressionRuntimeProvider()), Definition.Body);
            Replace(Name, Definition.Parameters.ToArray(), Definition.ReturnValue, d);
        }
        public void Replace(String Name, Yuki.ExpressionSchema.VariableDef[] Parameters, PrimitiveType ReturnValue, Func<VariableContext, Delegate> Create)
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

        public Delegate[] GetValue(String Name, PrimitiveType[] ParameterTypes, Delegate[] Parameters)
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
                            var vc = new VariableContext();
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
            return l.ToArray();
        }
    }
}
