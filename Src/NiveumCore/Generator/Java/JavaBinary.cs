//==========================================================================
//
//  File:        JavaBinary.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构Java二进制代码生成器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.JavaBinary
{
    public static class CodeGenerator
    {
        public static Dictionary<String, String> CompileToJavaBinary(this Schema Schema, String PackageName)
        {
            var t = new Templates(Schema);
            var Files = t.GetPackageFiles(Schema, PackageName).ToDictionary(p => p.Key + ".java", p => String.Join("\r\n", p.Value.Select(Line => Line.TrimEnd(' '))));
            return Files;
        }
    }

    public partial class Templates
    {
        private Java.Templates Inner;
        public Templates(Schema Schema)
        {
            this.Inner = new Java.Templates(Schema);
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
            var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
            var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

            foreach (var t in Tuples)
            {
                l.AddRange(BinaryTranslator_Tuple(t, NamespaceName));
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
                l.AddRange(BinaryTranslator_Enum("OptionalTag", "OptionalTag", "Int", "int", NamespaceName));
                l.Add("");
            }
            foreach (var gts in GenericTypeSpecs)
            {
                if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Optional") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(BinaryTranslator_Optional(gts, GenericOptionalType, NamespaceName));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("List") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(BinaryTranslator_List(gts, NamespaceName));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Set") && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(BinaryTranslator_Set(gts, NamespaceName));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.NameMatches("Map") && gts.GenericTypeSpec.ParameterValues.Count == 2)
                {
                    l.AddRange(BinaryTranslator_Map(gts, NamespaceName));
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

            AddClass(NamespaceName, "IReadableStream", IReadableStream());
            AddClass(NamespaceName, "IWritableStream", IWritableStream());

            AddClass(NamespaceName, "BinaryTranslator", BinaryTranslator(Schema, NamespaceName));

            var NamespaceParts = NamespaceName.Split('.');
            var Imports = new List<String>();
            for (int k = 1; k < NamespaceParts.Length; k += 1)
            {
                Imports.Add(String.Join(".", NamespaceParts.Take(k).Select(p => LowercaseCamelize(p))) + ".*");
            }
            return NamespaceToClasses.SelectMany(p => p.Value.Select(v => new KeyValuePair<String, IEnumerable<String>>(String.Join("/", p.Key.Split('.').Where(NamespacePart => NamespacePart != "").Select(NamespacePart => LowercaseCamelize(NamespacePart)).Concat(new String[] { v.Key })), WrapModule(p.Key, Imports.Concat(Schema.Imports).ToList(), v.Value)))).ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
