//==========================================================================
//
//  File:        TupleAndGenericTypeSpecFetcher.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 元组和泛型特化获取器
//  Version:     2012.02.24.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yuki.ObjectSchema
{
    public class TupleAndGenericTypeSpecFetcher
    {
        private List<TypeSpec> Tuples = new List<TypeSpec>();
        private HashSet<String> TupleSet = new HashSet<String>();
        private List<TypeSpec> GenericTypeSpecs = new List<TypeSpec>();
        private HashSet<String> GenericTypeSpecSet = new HashSet<String>();
        private String GetTypeString(TypeSpec Type)
        {
            switch (Type._Tag)
            {
                case TypeSpecTag.TypeRef:
                    return Type.TypeRef.Value;
                case TypeSpecTag.GenericParameterRef:
                    return Type.GenericParameterRef.Value;
                case TypeSpecTag.Tuple:
                    {
                        var n = "Tuple<" + String.Join(", ", Type.Tuple.Types.Select(t => GetTypeString(t)).ToArray()) + ">";
                        if (!TupleSet.Contains(n))
                        {
                            TupleSet.Add(n);
                            Tuples.Add(Type);
                        }
                        return n;
                    }
                case TypeSpecTag.GenericTypeSpec:
                    {
                        var n = GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(p => GetTypeString(p)).ToArray()) + ">";
                        if (!GenericTypeSpecSet.Contains(n))
                        {
                            GenericTypeSpecSet.Add(n);
                            GenericTypeSpecs.Add(Type);
                        }
                        return n;
                    }
                default:
                    throw new InvalidOperationException();
            }
        }
        private String GetTypeString(GenericParameterValue Value)
        {
            if (Value.OnLiteral)
            {
                return Value.Literal.Replace(@"\", @"\\").Replace("<", @"\<").Replace(">", @"\>").Replace(",", @"\,");
            }
            else if (Value.OnTypeSpec)
            {
                return GetTypeString(Value.TypeSpec);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        public void PushTypeSpec(TypeSpec Type)
        {
            GetTypeString(Type);
        }
        public void PushTypeDef(TypeDef c)
        {
            foreach (var gp in c.GenericParameters())
            {
                PushTypeSpec(gp.Type);
            }
            if (c._Tag == TypeDefTag.Primitive)
            {
            }
            else if (c._Tag == TypeDefTag.Alias)
            {
                PushTypeSpec(c.Alias.Type);
            }
            else if (c._Tag == TypeDefTag.Record)
            {
                foreach (var f in c.Record.Fields)
                {
                    PushTypeSpec(f.Type);
                }
            }
            else if (c._Tag == TypeDefTag.TaggedUnion)
            {
                foreach (var a in c.TaggedUnion.Alternatives)
                {
                    PushTypeSpec(a.Type);
                }
            }
            else if (c._Tag == TypeDefTag.Enum)
            {
            }
            else if (c._Tag == TypeDefTag.ClientCommand)
            {
                foreach (var p in c.ClientCommand.InParameters)
                {
                    PushTypeSpec(p.Type);
                }
                foreach (var p in c.ClientCommand.OutParameters)
                {
                    PushTypeSpec(p.Type);
                }
            }
            else if (c._Tag == TypeDefTag.ServerCommand)
            {
                foreach (var p in c.ServerCommand.OutParameters)
                {
                    PushTypeSpec(p.Type);
                }
            }
        }
        public void PushTypeSpecs(IEnumerable<TypeSpec> Types)
        {
            foreach (var t in Types)
            {
                PushTypeSpec(t);
            }
        }
        public void PushTypeDefs(IEnumerable<TypeDef> Types)
        {
            foreach (var t in Types)
            {
                PushTypeDef(t);
            }
        }
        public TypeSpec[] GetTuples()
        {
            return Tuples.ToArray();
        }
        public TypeSpec[] GetGenericTypeSpecs()
        {
            return GenericTypeSpecs.ToArray();
        }
    }
}
