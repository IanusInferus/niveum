//==========================================================================
//
//  File:        CSharp.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#二进制通讯代码生成器
//  Version:     2016.08.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.CSharpBinary
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpBinary(this Schema Schema, String NamespaceName, Boolean WithFirefly)
        {
            var t = new Templates(WithFirefly);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSharpBinary(this Schema Schema)
        {
            return CompileToCSharpBinary(Schema, "", true);
        }
    }

    public partial class Templates
    {
        private CSharp.Templates Inner = new CSharp.Templates();
        private Boolean WithFirefly;
        public Templates(Boolean WithFirefly)
        {
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
        public String GetTypeString(TypeSpec Type)
        {
            return Inner.GetTypeString(Type);
        }

        public List<String> GetPrimitives(Schema Schema)
        {
            return Inner.GetPrimitives(Schema);
        }
        public List<String> GetBinaryTranslatorSerializers(Schema Schema)
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
                    if (PrimitiveTranslators.ContainsKey(c.Primitive.Name))
                    {
                        l.AddRange(PrimitiveTranslators[c.Primitive.Name]());
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (c.OnAlias)
                {
                    l.AddRange(BinaryTranslator_Alias(c.Alias));
                }
                else if (c.OnRecord)
                {
                    l.AddRange(BinaryTranslator_Record(c.Record));
                }
                else if (c.OnTaggedUnion)
                {
                    l.AddRange(BinaryTranslator_TaggedUnion(c.TaggedUnion));
                }
                else if (c.OnEnum)
                {
                    l.AddRange(BinaryTranslator_Enum(c.Enum));
                }
                else if (c.OnClientCommand)
                {
                    l.AddRange(BinaryTranslator_ClientCommand(c.ClientCommand));
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(BinaryTranslator_ServerCommand(c.ServerCommand));
                }
                else
                {
                    throw new InvalidOperationException();
                }
                l.Add("");
            }

            var scg = Schema.GetSchemaClosureGenerator();
            var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
            var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
            var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

            foreach (var t in Tuples)
            {
                l.AddRange(BinaryTranslator_Tuple(t));
                l.Add("");
            }

            var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
            TaggedUnionDef GenericOptionalType = null;
            if (GenericOptionalTypes.Count > 0)
            {
                GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                l.AddRange(BinaryTranslator_Enum("OptionalTag", "Int", "Int"));
                l.Add("");
            }
            foreach (var gts in GenericTypeSpecs)
            {
                if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(BinaryTranslator_Optional(gts, GenericOptionalType));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(BinaryTranslator_List(gts));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(BinaryTranslator_Set(gts));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.ParameterValues.Count == 2)
                {
                    l.AddRange(BinaryTranslator_Map(gts));
                    l.Add("");
                }
                else
                {
                    throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
                }
            }

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
        public List<String> GetComplexTypes(Schema Schema)
        {
            var l = new List<String>();

            List<TypeDef> cl = new List<TypeDef>();

            foreach (var c in Schema.Types)
            {
                if (c.OnClientCommand)
                {
                    cl.Add(c);
                }
                else if (c.OnServerCommand)
                {
                    cl.Add(c);
                }
            }

            if (cl.Count > 0)
            {
                var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                var Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).Hash();
                l.AddRange(BinarySerializationServer(Hash, cl, SchemaClosureGenerator));
                l.Add("");
                l.AddRange(IBinarySender());
                l.Add("");
                l.AddRange(BinarySerializationClient(Hash, cl, SchemaClosureGenerator));
                l.Add("");
            }

            if (!WithFirefly)
            {
                l.AddRange(Streams());
                l.Add("");
            }

            l.AddRange(BinaryTranslator(Schema));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
    }
}
