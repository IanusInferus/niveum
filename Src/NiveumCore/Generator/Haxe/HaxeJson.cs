//==========================================================================
//
//  File:        HaxeJson.cs
//  Location:    Niveum.Core <Visual C#>
//  Description: 对象类型结构C# JSON通讯代码生成器
//  Version:     2017.04.22.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace Niveum.ObjectSchema.HaxeJson
{
    public static class CodeGenerator
    {
        public static String CompileToHaxeJson(this Schema Schema, String PackageName)
        {
            var t = new Templates(Schema);
            var Lines = t.Main(Schema, PackageName).Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
        public static String CompileToHaxeJson(this Schema Schema)
        {
            return CompileToHaxeJson(Schema, "");
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
        public String GetTypeString(TypeSpec Type)
        {
            return Inner.GetTypeString(Type);
        }

        public List<String> GetJsonTranslatorSerializers(Schema Schema)
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
                    l.AddRange(JsonTranslator_Alias(c.Alias));
                }
                else if (c.OnRecord)
                {
                    l.AddRange(JsonTranslator_Record(c.Record));
                }
                else if (c.OnTaggedUnion)
                {
                    l.AddRange(JsonTranslator_TaggedUnion(c.TaggedUnion));
                }
                else if (c.OnEnum)
                {
                    l.AddRange(JsonTranslator_Enum(c.Enum));
                }
                else if (c.OnClientCommand)
                {
                    l.AddRange(JsonTranslator_ClientCommand(c.ClientCommand));
                }
                else if (c.OnServerCommand)
                {
                    l.AddRange(JsonTranslator_ServerCommand(c.ServerCommand));
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
                l.AddRange(JsonTranslator_Tuple(t));
                l.Add("");
            }

            foreach (var gts in GenericTypeSpecs)
            {
                if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(JsonTranslator_Optional(gts));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(JsonTranslator_List(gts));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                {
                    l.AddRange(JsonTranslator_Set(gts));
                    l.Add("");
                }
                else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.ParameterValues.Count == 2)
                {
                    l.AddRange(JsonTranslator_Map(gts));
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
        public List<String> GetTypes(Schema Schema)
        {
            var l = new List<String>();

            var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
            if (Commands.Count > 0)
            {
                var SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                var Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).GetNonattributed().Hash();
                l.AddRange(IJsonSender());
                l.Add("");
                l.AddRange(JsonSerializationClient(Hash, Commands, SchemaClosureGenerator));
                l.Add("");
            }

            l.AddRange(JsonTranslator(Schema));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }
    }
}
