//==========================================================================
//
//  File:        CSharpJson.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C# JSON通讯代码生成器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.CSharpJson
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpJson(this Schema Schema, String NamespaceName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, NamespaceName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToCSharpJson(this Schema Schema)
        {
            return CompileToCSharpJson(Schema, "");
        }
    }

    public partial class Templates
    {
        private CSharp.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new CSharp.Templates(Schema);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }
        public String LowercaseCamelize(String PascalName)
        {
            var l = new List<Char>();
            foreach (var c in PascalName)
            {
                if (Char.IsLower(c))
                {
                    break;
                }

                l.Add(Char.ToLower(c));
            }

            return new String(l.ToArray()) + PascalName.Substring(l.Count);
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
        public List<String> GetJsonTranslatorSerializers(Schema Schema, String NamespaceName)
        {
            var l = new List<String>();

            var PrimitiveTranslators = new Dictionary<String, Func<IEnumerable<String>>>
            {
                { "Unit", JsonTranslator_Primitive_Unit },
                { "Boolean", JsonTranslator_Primitive_Boolean },
                { "String", JsonTranslator_Primitive_String },
                { "Int", JsonTranslator_Primitive_Int },
                { "Real", JsonTranslator_Primitive_Real },
                { "Byte", JsonTranslator_Primitive_Byte },
                { "UInt8", JsonTranslator_Primitive_UInt8 },
                { "UInt16", JsonTranslator_Primitive_UInt16 },
                { "UInt32", JsonTranslator_Primitive_UInt32 },
                { "UInt64", JsonTranslator_Primitive_UInt64 },
                { "Int8", JsonTranslator_Primitive_Int8 },
                { "Int16", JsonTranslator_Primitive_Int16 },
                { "Int32", JsonTranslator_Primitive_Int32 },
                { "Int64", JsonTranslator_Primitive_Int64 },
                { "Float32", JsonTranslator_Primitive_Float32 },
                { "Float64", JsonTranslator_Primitive_Float64 },
                { "Type", JsonTranslator_Primitive_Type }
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
                    l.AddRange(JsonTranslator_Alias(c.Alias, NamespaceName));
                }
                else if (c.OnRecord)
                {
                    l.AddRange(JsonTranslator_Record(c.Record, NamespaceName));
                }
                else if (c.OnTaggedUnion)
                {
                    l.AddRange(JsonTranslator_TaggedUnion(c.TaggedUnion, NamespaceName));
                }
                else if (c.OnEnum)
                {
                    l.AddRange(JsonTranslator_Enum(c.Enum, NamespaceName));
                }
                else if (c.OnClientCommand)
                {
                    l.AddRange(JsonTranslator_ClientCommand(c.ClientCommand, NamespaceName));
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(JsonTranslator_ServerCommand(c.ServerCommand, NamespaceName));
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
                l.AddRange(JsonTranslator_Tuple(t, NamespaceName));
                l.Add("");
            }

            var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.NameMatches("Optional")).ToList();
            TaggedUnionDef GenericOptionalType = null;
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
                l.AddRange(JsonTranslator_Enum("OptionalTag", "OptionalTag", NamespaceName));
                l.Add("");
            }
            foreach (var gts in GenericTypeSpecs)
            {
                if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(JsonTranslator_Optional(gts, GenericOptionalType, NamespaceName));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("List") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(JsonTranslator_List(gts, NamespaceName));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Set") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(JsonTranslator_Set(gts, NamespaceName));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Map") && gts.GenericTypeSpec.ParameterValues.Count == 2)
                {
                    l.AddRange(JsonTranslator_Map(gts, NamespaceName));
                    l.Add("");
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
                AddClass(NamespaceName, JsonSerializationServer(Hash, Commands, SchemaClosureGenerator, NamespaceName));
                AddClass(NamespaceName, IJsonSender());
                AddClass(NamespaceName, JsonSerializationClient(Hash, Commands, SchemaClosureGenerator, NamespaceName));
                AddClass(NamespaceName, JsonLogAspectWrapper(Commands, NamespaceName));
            }

            AddClass(NamespaceName, JsonTranslator(Schema, NamespaceName));

            var Classes = NamespaceToClasses.Select(p => Inner.WrapNamespace(p.Key, p.Value.Join(new String[] { "" })));

            return (new List<List<String>> { Primitives }).Concat(Classes).Join(new String[] { "" }).ToList();
        }
    }
}
