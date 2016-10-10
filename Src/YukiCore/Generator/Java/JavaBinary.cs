//==========================================================================
//
//  File:        JavaBinary.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Java二进制代码生成器
//  Version:     2016.10.11.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yuki.ObjectSchema.JavaBinary
{
    public static class CodeGenerator
    {
        public static String CompileToJavaBinary(this Schema Schema, String ClassName, String PackageName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, ClassName, PackageName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToJavaBinary(this Schema Schema, String ClassName)
        {
            return CompileToJavaBinary(Schema, ClassName, "");
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
                l.AddRange(BinaryTranslator_Enum("OptionalTag", "Int", "int"));
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
                    throw new InvalidOperationException(String.Format("GenericTypeNotSupported: {0}", gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
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

            foreach (var c in Schema.Types)
            {
                if (c.OnPrimitive)
                {
                    if (c.VersionedName() == "Unit")
                    {
                        l.AddRange(Inner.Primitive_Unit());
                    }
                    else
                    {
                        continue;
                    }
                }
                else if (c.OnAlias)
                {
                    l.AddRange(Inner.Alias(c.Alias));
                }
                else if (c.OnRecord)
                {
                    l.AddRange(Inner.Record(c.Record));
                }
                else if (c.OnTaggedUnion)
                {
                    l.AddRange(Inner.TaggedUnion(c.TaggedUnion));
                }
                else if (c.OnEnum)
                {
                    l.AddRange(Inner.Enum(c.Enum));
                }
                else if (c.OnClientCommand)
                {
                    l.AddRange(Inner.ClientCommand(c.ClientCommand));
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(Inner.ServerCommand(c.ServerCommand));
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
                l.AddRange(Inner.Tuple(t));
                l.Add("");
            }

            var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
            if (GenericOptionalTypes.Count > 0)
            {
                var GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" } }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Description = "" };
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        var ElementType = gts.GenericTypeSpec.ParameterValues.Single();
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Attributes = a.Attributes, Description = a.Description }).ToList();
                        l.AddRange(Inner.TaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Attributes = GenericOptionalType.Attributes, Description = GenericOptionalType.Description }));
                        l.Add("");
                    }
                }
            }

            l.AddRange(Streams());
            l.Add("");

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
