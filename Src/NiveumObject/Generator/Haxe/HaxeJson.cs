//==========================================================================
//
//  File:        HaxeJson.cs
//  Location:    Niveum.Object <Visual C#>
//  Description: 对象类型结构C# JSON通讯代码生成器
//  Version:     2022.10.02.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.HaxeJson
{
    public static class CodeGenerator
    {
        public static Dictionary<String, String> CompileToHaxeJson(this Schema Schema, String PackageName)
        {
            var t = new Templates(Schema);
            var Files = t.GetPackageFiles(Schema, PackageName).ToDictionary(p => p.Key + ".hx", p => String.Join("\r\n", p.Value.Select(Line => Line.TrimEnd(' '))));
            return Files;
        }
    }

    public partial class Templates
    {
        private Haxe.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new Haxe.Templates(Schema);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }
        public String LowercaseCamelize(String PascalName)
        {
            return Inner.LowercaseCamelize(PascalName);
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
            var TypeDict = sc.TypeDefs.ToDictionary(t => t.VersionedName());
            var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
            var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

            foreach (var t in Tuples)
            {
                l.AddRange(JsonTranslator_Tuple(t, NamespaceName));
                l.Add("");
            }

            foreach (var gts in GenericTypeSpecs)
            {
                if (gts.GenericTypeSpec.TypeSpec.OnTypeRef)
                {
                    var t = TypeDict[gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()];
                    if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(JsonTranslator_Optional(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("List") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(JsonTranslator_List(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Set") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(JsonTranslator_Set(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Map") && gts.GenericTypeSpec.ParameterValues.Count == 2)
                    {
                        l.AddRange(JsonTranslator_Map(gts, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnAlias)
                    {
                        var a = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).Alias;
                        l.AddRange(JsonTranslator_Alias(gts.SimpleName(NamespaceName), GetTypeString(gts, NamespaceName), a.Type, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnRecord)
                    {
                        var r = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).Record;
                        l.AddRange(JsonTranslator_Record(gts.SimpleName(NamespaceName), GetTypeString(gts, NamespaceName), r.Fields, NamespaceName));
                        l.Add("");
                    }
                    else if (t.OnTaggedUnion)
                    {
                        var tu = t.MakeGenericType(new List<String> { }, gts.GenericTypeSpec.ParameterValues).TaggedUnion;
                        l.AddRange(JsonTranslator_TaggedUnion(gts.SimpleName(NamespaceName), GetTypeString(gts, NamespaceName), tu.Alternatives, NamespaceName));
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
        public Dictionary<String, IEnumerable<String>> GetPackageFiles(Schema Schema, String NamespaceName)
        {
            var NamespaceToClasses = new Dictionary<String, List<KeyValuePair<String, List<String>>>>();
            void AddClass(String ClassNamespaceName, String ClassName, IEnumerable<String> ClassContent)
            {
                if (!NamespaceToClasses.ContainsKey(ClassNamespaceName))
                {
                    NamespaceToClasses.Add(ClassNamespaceName, new List<KeyValuePair<String, List<String>>>());
                }
                NamespaceToClasses[ClassNamespaceName].Add(new KeyValuePair<String, List<String>>(ClassName, ClassContent.ToList()));
            }

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
            if (Commands.Count > 0)
            {
                var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                var Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).GetNonattributed().Hash();
                AddClass(NamespaceName, "IJsonSender", IJsonSender());
                AddClass(NamespaceName, "JsonSerializationClient", JsonSerializationClient(Hash, Commands, SchemaClosureGenerator, NamespaceName));
            }

            AddClass(NamespaceName, "JsonTranslator", JsonTranslator(Schema, NamespaceName));

            return NamespaceToClasses.SelectMany(p => p.Value.Select(v => new KeyValuePair<String, IEnumerable<String>>(String.Join("/", p.Key.Split('.').Where(NamespacePart => NamespacePart != "").Select(NamespacePart => LowercaseCamelize(NamespacePart)).Concat(new String[] { v.Key })), WrapModule(p.Key, Schema.Imports, v.Value)))).ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
