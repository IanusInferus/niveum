﻿//==========================================================================
//
//  File:        CSharpBinary.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构C#二进制通讯代码生成器
//  Version:     2022.11.01.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.CSharpBinary
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpBinary(this Schema Schema, String NamespaceName, Boolean WithFirefly, Boolean EnableNullableDeclaration)
        {
            var t = new Templates(Schema, WithFirefly);
            var Lines = t.Main(Schema, NamespaceName, EnableNullableDeclaration).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSharpBinary(this Schema Schema)
        {
            return CompileToCSharpBinary(Schema, "", true, false);
        }
    }

    public partial class Templates
    {
        private CSharp.Templates Inner;
        private Boolean WithFirefly;
        public Templates(Schema Schema, Boolean WithFirefly)
        {
            this.Inner = new CSharp.Templates(Schema);
            this.WithFirefly = WithFirefly;
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
        public TypeRef GetSuffixedTypeRef(List<String> Name, String Version, String Suffix)
        {
            return Inner.GetSuffixedTypeRef(Name, Version, Suffix);
        }
        public String GetSuffixedTypeString(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            return Inner.GetSuffixedTypeString(Name, Version, Suffix, NamespaceName);
        }
        public String GetSuffixedTypeName(List<String> Name, String Version, String Suffix, String NamespaceName)
        {
            return Inner.GetSuffixedTypeName(Name, Version, Suffix, NamespaceName);
        }

        public List<String> GetPrimitives(Schema Schema)
        {
            return Inner.GetPrimitives(Schema);
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
                else if (c.OnClientCommand)
                {
                    l.AddRange(BinaryTranslator_ClientCommand(c.ClientCommand, NamespaceName));
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(BinaryTranslator_ServerCommand(c.ServerCommand, NamespaceName));
                }
                else
                {
                    throw new InvalidOperationException();
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

            var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.NameMatches("Optional")).ToList();
            var GenericOptionalType = Optional<TaggedUnionDef>.Empty;
            if (GenericOptionalTypes.Count > 0)
            {
                GenericOptionalType = new TaggedUnionDef
                {
                    Name = new List<String> { "TaggedUnion" },
                    Version = "",
                    GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String> { "Type" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } },
                    Alternatives = new List<VariableDef>
                    {
                        new VariableDef { Name = "None", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = new List<String> { "Unit" }, Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" },
                        new VariableDef { Name = "Some", Type = TypeSpec.CreateGenericParameterRef("T"), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }
                    },
                    Attributes = new List<KeyValuePair<String, List<String>>> { },
                    Description = ""
                };
                l.AddRange(BinaryTranslator_Enum("OptionalTag", "OptionalTag", "Int", "Int", NamespaceName));
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
                        l.AddRange(BinaryTranslator_Optional(gts, GenericOptionalType.Value, NamespaceName));
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
                        l.AddRange(BinaryTranslator_Alias(gts.SimpleName(NamespaceName), GetTypeString(gts, NamespaceName), a.Type, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnRecord)
                    {
                        var r = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).Record;
                        l.AddRange(BinaryTranslator_Record(gts.SimpleName(NamespaceName), GetTypeString(gts, NamespaceName), r.Fields, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnTaggedUnion)
                    {
                        var tu = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).TaggedUnion;
                        var TagName = GetSuffixedTypeName(t.TaggedUnion.Name, t.TaggedUnion.Version, "Tag", NamespaceName);
                        var TagTypeString = GetSuffixedTypeString(t.TaggedUnion.Name, t.TaggedUnion.Version, "Tag", NamespaceName);
                        l.AddRange(BinaryTranslator_TaggedUnion(gts.SimpleName(NamespaceName), GetTypeString(gts, NamespaceName), TagName, TagTypeString, tu.Alternatives, NamespaceName));
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
            var Primitives = GetPrimitives(Schema);

            var NamespaceToClasses = new Dictionary<String, List<List<String>>>();
            void AddClass(String ClassNamespaceName, IEnumerable<String> ClassContent)
            {
                if (!NamespaceToClasses.ContainsKey(ClassNamespaceName))
                {
                    NamespaceToClasses.Add(ClassNamespaceName, new List<List<String>>());
                }
                NamespaceToClasses[ClassNamespaceName].Add(ClassContent.ToList());
            }

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToList();
            if (Commands.Count > 0)
            {
                var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                var Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).GetNonattributed().Hash();
                AddClass(NamespaceName, BinarySerializationServer(Hash, Commands, SchemaClosureGenerator, NamespaceName));
                AddClass(NamespaceName, IBinarySender());
                AddClass(NamespaceName, BinarySerializationClient(Hash, Commands, SchemaClosureGenerator, NamespaceName));
            }

            if (!WithFirefly)
            {
                AddClass(NamespaceName, Streams());
            }

            AddClass(NamespaceName, BinaryTranslator(Schema, NamespaceName));

            var Classes = NamespaceToClasses.Select(p => Inner.WrapNamespace(p.Key, p.Value.Join(new String[] { "" })));

            return (new List<List<String>> { Primitives }).Concat(Classes).Join(new String[] { "" }).ToList();
        }
    }
}
