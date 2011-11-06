//==========================================================================
//
//  File:        ObjectSchemaExtensions.cs
//  Location:    Yuki.SchemaManipulator <Visual C#>
//  Description: 对象类型结构扩展
//  Version:     2011.11.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Firefly;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema
{
    public static class ObjectSchemaExtensions
    {
        public static IEnumerable<KeyValuePair<String, TypeDef>> GetMap(this Schema s)
        {
            return s.TypeRefs.Concat(s.Types).Select(t => CollectionOperations.CreatePair(t.Name(), t));
        }

        public static UInt64 Hash(this Schema s)
        {
            var Types = s.GetMap().OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Value).ToArray();
            var bs = new BinarySerializer();
            bs.PutWriter((String str, IWritableStream ws) => ws.Write(TextEncoding.UTF8.GetBytes(str)));
            var sha = new SHA1CryptoServiceProvider();
            Byte[] result;

            using (var ms = Streams.CreateMemoryStream())
            {
                bs.Write(Types, ms);
                ms.Position = 0;

                result = sha.ComputeHash(ms.ToUnsafeStream());
            }

            using (var ms = Streams.CreateMemoryStream())
            {
                ms.Write(result.Reverse().Take(8).Reverse().ToArray());
                ms.Position = 0;

                return ms.ReadUInt64B();
            }
        }

        public static Schema Reduce(this Schema s)
        {
            var Types = s.GetMap().ToDictionary(t => t.Key, t => t.Value, StringComparer.OrdinalIgnoreCase);

            var m = new Marker { Types = Types };
            foreach (var t in s.Types)
            {
                switch (t._Tag)
                {
                    case TypeDefTag.ClientCommand:
                        m.Mark(t);
                        break;
                    case TypeDefTag.ServerCommand:
                        m.Mark(t);
                        break;
                }
            }

            var MarkedNames = new HashSet<String>(Types.Where(p => m.Marked.Contains(p.Value)).Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

            return new Schema { Types = s.Types.Where(t => m.Marked.Contains(t)).ToArray(), TypeRefs = s.TypeRefs.Where(t => m.Marked.Contains(t)).ToArray(), Imports = s.Imports, TypePaths = s.TypePaths.Where(tp => MarkedNames.Contains(tp.Name)).ToArray() };
        }

        private class Marker
        {
            public Dictionary<String, TypeDef> Types;
            public HashSet<TypeDef> Marked = new HashSet<TypeDef>();
            public void Mark(TypeDef t)
            {
                if (Marked.Contains(t)) { return; }
                Marked.Add(t);
                switch (t._Tag)
                {
                    case TypeDefTag.Primitive:
                        foreach (var gp in t.Primitive.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        break;
                    case TypeDefTag.Alias:
                        foreach (var gp in t.Alias.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        Mark(t.Alias.Type);
                        break;
                    case TypeDefTag.Record:
                        foreach (var gp in t.Record.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        foreach (var f in t.Record.Fields)
                        {
                            Mark(f.Type);
                        }
                        break;
                    case TypeDefTag.TaggedUnion:
                        foreach (var gp in t.TaggedUnion.GenericParameters)
                        {
                            Mark(gp.Type);
                        }
                        foreach (var a in t.TaggedUnion.Alternatives)
                        {
                            Mark(a.Type);
                        }
                        break;
                    case TypeDefTag.Enum:
                        Mark(t.Enum.UnderlyingType);
                        break;
                    case TypeDefTag.ClientCommand:
                        foreach (var p in t.ClientCommand.InParameters)
                        {
                            Mark(p.Type);
                        }
                        foreach (var p in t.ClientCommand.OutParameters)
                        {
                            Mark(p.Type);
                        }
                        break;
                    case TypeDefTag.ServerCommand:
                        foreach (var p in t.ServerCommand.OutParameters)
                        {
                            Mark(p.Type);
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            public void Mark(TypeSpec t)
            {
                switch (t._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        Mark(Types[t.TypeRef.Value]);
                        break;
                    case TypeSpecTag.GenericParameterRef:
                        break;
                    case TypeSpecTag.Tuple:
                        foreach (var ts in t.Tuple.Types)
                        {
                            Mark(ts);
                        }
                        break;
                    case TypeSpecTag.GenericTypeSpec:
                        Mark(t.GenericTypeSpec.TypeSpec);
                        foreach (var gpv in t.GenericTypeSpec.GenericParameterValues)
                        {
                            if (gpv.OnTypeSpec)
                            {
                                Mark(gpv.TypeSpec);
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public static void Verify(this Schema s)
        {
            VerifyDuplicatedNames(s);
        }

        public static void VerifyDuplicatedNames(this Schema s)
        {
            CheckDuplicatedNames(s.TypePaths, tp => tp.Name, tp => String.Format("DuplicatedName {0}: at {1}", tp.Name, tp.Path));

            var PathDict = s.TypePaths.ToDictionary(tp => tp.Name, tp => tp.Path);

            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                switch (t._Tag)
                {
                    case TypeDefTag.Record:
                        {
                            var r = t.Record;
                            CheckDuplicatedNames(r.Fields, rf => rf.Name, rf => String.Format("DuplicatedField {0}: record {1}, at {2}", rf.Name, r.Name, PathDict[r.Name]));
                        }
                        break;
                    case TypeDefTag.TaggedUnion:
                        {
                            var tu = t.TaggedUnion;
                            CheckDuplicatedNames(tu.Alternatives, tua => tua.Name, tua => String.Format("DuplicatedAlternative {0}: tagged union {1}, at {2}", tua.Name, tu.Name, PathDict[tu.Name]));
                        }
                        break;
                    case TypeDefTag.Enum:
                        {
                            var e = t.Enum;
                            CheckDuplicatedNames(e.Literals, el => el.Name, el => String.Format("DuplicatedLiteral {0}: enum {1}, at {2}", el.Name, e.Name, PathDict[e.Name]));
                        }
                        break;
                    case TypeDefTag.ClientCommand:
                        {
                            var cc = t.ClientCommand;
                            CheckDuplicatedNames(cc.OutParameters, op => op.Name, op => String.Format("DuplicatedOutParameter {0}: client command {1}, at {2}", op.Name, cc.Name, PathDict[cc.Name]));
                            CheckDuplicatedNames(cc.InParameters, op => op.Name, op => String.Format("DuplicatedInParameter {0}: client command {1}, at {2}", op.Name, cc.Name, PathDict[cc.Name]));
                        }
                        break;
                    case TypeDefTag.ServerCommand:
                        {
                            var sc = t.ServerCommand;
                            CheckDuplicatedNames(sc.OutParameters, op => op.Name, op => String.Format("DuplicatedOutParameter {0}: server command {1}, at {2}", op.Name, sc.Name, PathDict[sc.Name]));
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private static void CheckDuplicatedNames<T>(IEnumerable<T> Values, Func<T, String> NameSelector, Func<T, String> ErrorMessageSelector)
        {
            var TypeNames = Values.Select(NameSelector).Distinct(StringComparer.OrdinalIgnoreCase);
            var DuplicatedNames = new HashSet<String>(Values.GroupBy(NameSelector, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);

            if (DuplicatedNames.Count > 0)
            {
                var l = new List<String>();
                foreach (var tp in Values.Where(p => DuplicatedNames.Contains(NameSelector(p))))
                {
                    l.Add(ErrorMessageSelector(tp));
                }
                var Message = String.Concat(l.Select(Line => Line + Environment.NewLine));
                throw new AggregateException(Message);
            }
        }

        public static String Name(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.Name;
                case TypeDefTag.Alias:
                    return t.Alias.Name;
                case TypeDefTag.Record:
                    return t.Record.Name;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.Name;
                case TypeDefTag.Enum:
                    return t.Enum.Name;
                case TypeDefTag.ClientCommand:
                    return t.ClientCommand.Name;
                case TypeDefTag.ServerCommand:
                    return t.ServerCommand.Name;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static String Description(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.Description;
                case TypeDefTag.Alias:
                    return t.Alias.Description;
                case TypeDefTag.Record:
                    return t.Record.Description;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.Description;
                case TypeDefTag.Enum:
                    return t.Enum.Description;
                case TypeDefTag.ClientCommand:
                    return t.ClientCommand.Description;
                case TypeDefTag.ServerCommand:
                    return t.ServerCommand.Description;
                default:
                    throw new InvalidOperationException();
            }
        }

        public static Variable[] GenericParameters(this TypeDef t)
        {
            switch (t._Tag)
            {
                case TypeDefTag.Primitive:
                    return t.Primitive.GenericParameters;
                case TypeDefTag.Alias:
                    return t.Alias.GenericParameters;
                case TypeDefTag.Record:
                    return t.Record.GenericParameters;
                case TypeDefTag.TaggedUnion:
                    return t.TaggedUnion.GenericParameters;
                case TypeDefTag.Enum:
                    return new Variable[] { };
                case TypeDefTag.ClientCommand:
                    return new Variable[] { };
                case TypeDefTag.ServerCommand:
                    return new Variable[] { };
                default:
                    throw new InvalidOperationException();
            }
        }

        public static String TypeFriendlyName(this TypeSpec Type)
        {
            return TypeFriendlyName(Type, gpr => gpr.Value);
        }
        public static String TypeFriendlyName(this TypeSpec Type, Func<GenericParameterRef, String> EvaluateGenericParameterRef)
        {
            return TypeFriendlyName(Type, EvaluateGenericParameterRef, TypeFriendlyName);
        }
        public static String TypeFriendlyName(this TypeSpec Type, Func<GenericParameterRef, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<GenericParameterRef, String>, String> Kernel)
        {
            switch (Type._Tag)
            {
                case TypeSpecTag.TypeRef:
                    return Type.TypeRef.Value;
                case TypeSpecTag.GenericParameterRef:
                    return EvaluateGenericParameterRef(Type.GenericParameterRef);
                case TypeSpecTag.Tuple:
                    return "TupleOf" + String.Join("And", Type.Tuple.Types.Select(t => Kernel(t, EvaluateGenericParameterRef)).ToArray());
                case TypeSpecTag.GenericTypeSpec:
                    return Kernel(Type.GenericTypeSpec.TypeSpec, EvaluateGenericParameterRef) + "Of" + String.Join("And", Type.GenericTypeSpec.GenericParameterValues.Select(t => TypeFriendlyName(t, EvaluateGenericParameterRef, Kernel)).ToArray());
                default:
                    throw new InvalidOperationException();
            }
        }
        private static Regex rNonRegularChars = new Regex(@"[\u0000-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007F]");
        public static String TypeFriendlyName(this GenericParameterValue Value, Func<GenericParameterRef, String> EvaluateGenericParameterRef, Func<TypeSpec, Func<GenericParameterRef, String>, String> Kernel)
        {
            switch (Value._Tag)
            {
                case GenericParameterValueTag.Literal:
                    var l = Value.Literal;
                    return rNonRegularChars.Replace(l, "_");
                case GenericParameterValueTag.TypeSpec:
                    return Kernel(Value.TypeSpec, EvaluateGenericParameterRef);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
