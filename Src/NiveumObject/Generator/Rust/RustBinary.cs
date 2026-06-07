//==========================================================================
//
//  File:        RustBinary.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构Rust二进制通讯代码生成器
//  Version:     2026.06.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.RustBinary
{
    public static class CodeGenerator
    {
        public static String CompileToRustBinary(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\n", Lines);
        }
        public static String CompileToRustBinary(this Schema Schema)
        {
            return CompileToRustBinary(Schema, "");
        }
    }

    public partial class Templates
    {
        private Rust.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new Rust.Templates(Schema);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }
        public String GetEscapedStringLiteral(String s)
        {
            return Inner.GetEscapedStringLiteral(s);
        }
        public String GetTypeString(TypeSpec Type, String NamespaceName)
        {
            return Inner.GetTypeString(Type, NamespaceName);
        }

        public List<String> GetBinaryTranslatorSerializers(Schema Schema, String NamespaceName)
        {
            var l = new List<String>();

            var PrimitiveTranslators = new Dictionary<String, Func<IEnumerable<String>>>
            {
                { "Unit", BinaryTranslator_Primitive_Unit },
                { "Boolean", BinaryTranslator_Primitive_Boolean },
                { "String", BinaryTranslator_Primitive_String },
                { "Int", BinaryTranslator_Primitive_Int },
                { "Real", BinaryTranslator_Primitive_Real },
                { "Byte", BinaryTranslator_Primitive_Byte },
                { "UInt8", BinaryTranslator_Primitive_UInt8 },
                { "UInt16", BinaryTranslator_Primitive_UInt16 },
                { "UInt32", BinaryTranslator_Primitive_UInt32 },
                { "UInt64", BinaryTranslator_Primitive_UInt64 },
                { "Int8", BinaryTranslator_Primitive_Int8 },
                { "Int16", BinaryTranslator_Primitive_Int16 },
                { "Int32", BinaryTranslator_Primitive_Int32 },
                { "Int64", BinaryTranslator_Primitive_Int64 },
                { "Float32", BinaryTranslator_Primitive_Float32 },
                { "Float64", BinaryTranslator_Primitive_Float64 },
                { "Type", BinaryTranslator_Primitive_Type }
            };

            foreach (var c in Schema.TypeRefs.Concat(Schema.Types))
            {
                if (c.GenericParameters().Count() != 0)
                {
                    continue;
                }
                if (c.OnPrimitive)
                {
                    if (PrimitiveTranslators.ContainsKey(c.Primitive.VersionedName()))
                    {
                        l.AddRange(PrimitiveTranslators[c.Primitive.VersionedName()]());
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (c.OnAlias)
                {
                    l.AddRange(BinaryTranslator_Alias(c.Alias, NamespaceName));
                }
                else if (c.OnRecord)
                {
                    l.AddRange(BinaryTranslator_Record(c.Record, NamespaceName));
                }
                else if (c.OnTaggedUnion)
                {
                    l.AddRange(BinaryTranslator_TaggedUnion(c.TaggedUnion, NamespaceName));
                }
                else if (c.OnEnum)
                {
                    l.AddRange(BinaryTranslator_Enum(c.Enum, NamespaceName));
                }
                else
                {
                    continue;
                }
                l.Add("");
            }

            var scg = Schema.GetSchemaClosureGenerator();
            var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
            var TypeDict = sc.TypeDefs.ToDictionary(t => t.VersionedName());
            var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
            var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

            foreach (var t in Tuples)
            {
                l.AddRange(BinaryTranslator_Tuple(t, NamespaceName));
                l.Add("");
            }

            foreach (var gts in GenericTypeSpecs)
            {
                if (gts.GenericTypeSpec.TypeSpec.OnTypeRef)
                {
                    var t = TypeDict[gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()];
                    if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional") && (gts.GenericTypeSpec.ParameterValues.Count == 1))
                    {
                        if (gts.GenericTypeSpec.ParameterValues.Any(pv => pv.IsGeneric())) { continue; }
                        l.AddRange(BinaryTranslator_Optional(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("List") && (gts.GenericTypeSpec.ParameterValues.Count == 1))
                    {
                        if (gts.GenericTypeSpec.ParameterValues.Any(pv => pv.IsGeneric())) { continue; }
                        l.AddRange(BinaryTranslator_List(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Set") && (gts.GenericTypeSpec.ParameterValues.Count == 1))
                    {
                        if (gts.GenericTypeSpec.ParameterValues.Any(pv => pv.IsGeneric())) { continue; }
                        l.AddRange(BinaryTranslator_Set(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Map") && (gts.GenericTypeSpec.ParameterValues.Count == 2))
                    {
                        if (gts.GenericTypeSpec.ParameterValues.Any(pv => pv.IsGeneric())) { continue; }
                        l.AddRange(BinaryTranslator_Map(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnAlias)
                    {
                        var a = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).Alias;
                        var SimpleName = gts.SimpleName(NamespaceName);
                        l.AddRange(BinaryTranslator_Alias(SimpleName, GetTypeString(gts, NamespaceName), a.Type, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnRecord)
                    {
                        var r = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).Record;
                        var SimpleName = gts.SimpleName(NamespaceName);
                        l.AddRange(BinaryTranslator_Record(SimpleName, GetTypeString(gts, NamespaceName), r.Fields, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnTaggedUnion)
                    {
                        var tu = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).TaggedUnion;
                        var SimpleName = gts.SimpleName(NamespaceName);
                        l.AddRange(BinaryTranslator_TaggedUnion(SimpleName, GetTypeString(gts, NamespaceName), tu.Alternatives, NamespaceName, tu.VersionedName()));
                        l.Add("");
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("GenericTypeNotSupported: {0}", gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
                    }
                }
                else
                {
                    throw new InvalidOperationException(String.Format("GenericTypeNotSupported: {0}", gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
                }
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public List<String> GetTypes(Schema Schema, String NamespaceName)
        {
            var NamespaceToClasses = new List<KeyValuePair<String, List<List<String>>>>();
            void AddClass(String ClassNamespaceName, IEnumerable<String> ClassContent)
            {
                if ((NamespaceToClasses.Count > 0) && (NamespaceToClasses[NamespaceToClasses.Count - 1].Key == ClassNamespaceName))
                {
                    NamespaceToClasses[NamespaceToClasses.Count - 1].Value.Add(ClassContent.ToList());
                }
                else
                {
                    NamespaceToClasses.Add(new KeyValuePair<String, List<List<String>>>(ClassNamespaceName, new List<List<String>> { ClassContent.ToList() }));
                }
            }

            AddClass(NamespaceName, Streams());
            AddClass(NamespaceName, BinaryTranslator(Schema, NamespaceName));

            var Classes = NamespaceToClasses.Select(p => WrapNamespace(p.Key, p.Value.Join(new String[] { "" })));

            return Classes.Join(new String[] { "" }).ToList();
        }

        public IEnumerable<String> WrapNamespace(String Namespace, IEnumerable<String> Contents)
        {
            return Contents;
        }

        public String WrapVariant(String varName, TypeSpec type, String enclosingVersionedName, String namespaceName)
        {
            var ts = Inner.GetBoxedTypeString(type, namespaceName, enclosingVersionedName);
            if (ts.StartsWith("Box<"))
            {
                return "Box::new(" + varName + ")";
            }
            return varName;
        }
        public String DerefVariant(String varName, TypeSpec type, String enclosingVersionedName, String namespaceName)
        {
            var ts = Inner.GetBoxedTypeString(type, namespaceName, enclosingVersionedName);
            if (ts.StartsWith("Box<"))
            {
                return "&*" + varName;
            }
            return varName;
        }

        public String GetGenericNew(String typeStr)
        {
            var idx = typeStr.IndexOf('<');
            if (idx == -1)
            {
                return typeStr + "::new()";
            }
            return typeStr.Insert(idx, "::") + "::new()";
        }

        public Boolean IsInclude(String s)
        {
            if (s.StartsWith("<") && s.EndsWith(">")) { return true; }
            if (s.StartsWith(@"""") && s.EndsWith(@"""")) { return true; }
            return false;
        }
    }
}
