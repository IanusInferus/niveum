//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Haxe JSON通讯代码生成器
//  Version:     2016.05.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.HaxeJson
{
    public static class CodeGenerator
    {
        public static String CompileToHaxeJson(this Schema Schema, String PackageName)
        {
            var w = new Writer(Schema, PackageName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToHaxeJson(this Schema Schema)
        {
            return CompileToHaxeJson(Schema, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Haxe.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private ISchemaClosureGenerator SchemaClosureGenerator;
            private String PackageName;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Haxe);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.HaxeJson);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.PackageName = NamespaceName;
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).Hash();

                InnerWriter = new Haxe.Common.CodeGenerator.Writer(Schema, PackageName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public List<String> GetSchema()
            {
                var Types = GetTypes(Schema);

                if (PackageName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Types", Types)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", new List<String> { }).Substitute("Imports", Schema.Imports).Substitute("Types", Types)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public List<String> GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public List<String> GetJsonSerializationClient(List<TypeDef> Commands)
            {
                return GetTemplate("JsonSerializationClient").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ApplicationCommands", GetJsonSerializationClientApplicationCommands(Commands)).Substitute("ServerCommands", GetJsonSerializationClientServerCommands(Commands));
            }
            public List<String> GetJsonSerializationClientApplicationCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonSerializationClient_ApplicationClientCommand").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("JsonSerializationClient_ApplicationServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()));
                    }
                }
                return l;
            }
            public List<String> GetJsonSerializationClientServerCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonSerializationClient_ServerCommand").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l;
            }

            public List<String> GetJsonTranslator(List<TypeDef> Types)
            {
                return GetTemplate("JsonTranslator").Substitute("Serializers", GetJsonTranslatorSerializers(Types));
            }

            public List<String> GetJsonTranslatorSerializers(List<TypeDef> Types)
            {
                var l = new List<String>();

                foreach (var c in Types)
                {
                    if (c.GenericParameters().Count() != 0)
                    {
                        continue;
                    }
                    if (c.OnPrimitive)
                    {
                        switch (c.Primitive.Name)
                        {
                            case "Unit":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Unit"));
                                break;
                            case "Boolean":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Boolean"));
                                break;
                            case "String":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_String"));
                                break;
                            case "Int":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Int"));
                                break;
                            case "Real":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Real"));
                                break;
                            case "Byte":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Byte"));
                                break;
                            case "UInt8":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_UInt8"));
                                break;
                            case "UInt16":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_UInt16"));
                                break;
                            case "UInt32":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_UInt32"));
                                break;
                            case "UInt64":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_UInt64"));
                                break;
                            case "Int8":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Int8"));
                                break;
                            case "Int16":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Int16"));
                                break;
                            case "Int32":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Int32"));
                                break;
                            case "Int64":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Int64"));
                                break;
                            case "Float32":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Float32"));
                                break;
                            case "Float64":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Float64"));
                                break;
                            case "Type":
                                l.AddRange(GetTemplate("JsonTranslator_Primitive_Type"));
                                break;
                            default:
                                throw new InvalidOperationException();
                        }
                    }
                    else if (c.OnAlias)
                    {
                        l.AddRange(GetJsonTranslatorAlias(c.Alias));
                    }
                    else if (c.OnRecord)
                    {
                        l.AddRange(GetJsonTranslatorRecord(c.Record));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        l.AddRange(GetJsonTranslatorTaggedUnion(c.TaggedUnion));
                    }
                    else if (c.OnEnum)
                    {
                        l.AddRange(GetJsonTranslatorEnum(c.Enum));
                    }
                    else if (c.OnClientCommand)
                    {
                        l.AddRange(GetJsonTranslatorClientCommand(c.ClientCommand));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetJsonTranslatorServerCommand(c.ServerCommand));
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
                    l.AddRange(GetJsonTranslatorTuple(t));
                    l.Add("");
                }

                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetJsonTranslatorOptional(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetJsonTranslatorList(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetJsonTranslatorSet(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.ParameterValues.Count == 2)
                    {
                        l.AddRange(GetJsonTranslatorMap(gts));
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
            public List<String> GetJsonTranslatorAlias(AliasDef a)
            {
                return GetTemplate("JsonTranslator_Alias").Substitute("Name", a.TypeFriendlyName()).Substitute("ValueTypeFriendlyName", a.Type.TypeFriendlyName());
            }
            public List<String> GetJsonTranslatorRecord(RecordDef r)
            {
                return GetJsonTranslatorRecord(r.TypeFriendlyName(), r.Fields);
            }
            public List<String> GetJsonTranslatorRecord(String Name, List<VariableDef> Fields)
            {
                var l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetJsonTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetJsonTranslatorFieldTos(Fields)));
                return l;
            }
            public List<String> GetJsonTranslatorFieldFroms(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorFieldTos(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorTaggedUnion(TaggedUnionDef tu)
            {
                return GetJsonTranslatorTaggedUnion(tu.TypeFriendlyName(), tu.Alternatives);
            }
            public List<String> GetJsonTranslatorTaggedUnion(String Name, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives)));
                return l;
            }
            public List<String> GetJsonTranslatorAlternativeFroms(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                    {
                        l.AddRange(GetTemplate("JsonTranslator_AlternativeFromUnit").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name));
                    }
                    else
                    {
                        l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                    }
                }
                return l;
            }
            public List<String> GetJsonTranslatorAlternativeTos(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                    {
                        l.AddRange(GetTemplate("JsonTranslator_AlternativeToUnit").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name));
                    }
                    else
                    {
                        l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                    }
                }
                return l;
            }
            public List<String> GetJsonTranslatorEnum(EnumDef e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingType", GetTypeString(e.UnderlyingType));
            }
            public List<String> GetJsonTranslatorClientCommand(ClientCommandDef c)
            {
                var l = new List<String>();
                l.AddRange(GetJsonTranslatorRecord(c.TypeFriendlyName() + "Request", c.OutParameters));
                l.AddRange(GetJsonTranslatorTaggedUnion(c.TypeFriendlyName() + "Reply", c.InParameters));
                return l;
            }
            public List<String> GetJsonTranslatorServerCommand(ServerCommandDef c)
            {
                var l = new List<String>();
                return GetJsonTranslatorRecord(c.TypeFriendlyName() + "Event", c.OutParameters);
            }
            public List<String> GetJsonTranslatorTuple(TypeSpec t)
            {
                var l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Tuple").Substitute("TypeFriendlyName", t.TypeFriendlyName()).Substitute("TupleElementFroms", GetJsonTranslatorTupleElementFroms(t.Tuple)).Substitute("TupleElementTos", GetJsonTranslatorTupleElementTos(t.Tuple)));
                return l;
            }
            public List<String> GetJsonTranslatorTupleElementFroms(List<TypeSpec> Types)
            {
                var l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("JsonTranslator_TupleElementFrom").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l;
            }
            public List<String> GetJsonTranslatorTupleElementTos(List<TypeSpec> Types)
            {
                var l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("JsonTranslator_TupleElementTo").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l;
            }
            public List<String> GetJsonTranslatorOptional(TypeSpec o)
            {
                var ElementType = o.GenericTypeSpec.ParameterValues.Single();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o);
                var ElementTypeFriendlyName = ElementType.TypeFriendlyName();
                var ElementTypeString = GetTypeString(ElementType);
                return GetTemplate("JsonTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("ElementTypeFriendlyName", ElementTypeFriendlyName).Substitute("ElementTypeString", ElementTypeString);
            }
            public List<String> GetJsonTranslatorList(TypeSpec c)
            {
                return GetTemplate("JsonTranslator_List").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName());
            }
            public List<String> GetJsonTranslatorSet(TypeSpec c)
            {
                return GetTemplate("JsonTranslator_Set").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName());
            }
            public List<String> GetJsonTranslatorMap(TypeSpec c)
            {
                var KeyTypeFriendlyName = c.GenericTypeSpec.ParameterValues[0].TypeFriendlyName();
                var ValueTypeFriendlyName = c.GenericTypeSpec.ParameterValues[1].TypeFriendlyName();
                return GetTemplate("JsonTranslator_Map").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("KeyTypeFriendlyName", KeyTypeFriendlyName).Substitute("ValueTypeFriendlyName", ValueTypeFriendlyName);
            }

            public List<String> GetTypes(Schema Schema)
            {
                var l = new List<String>();

                var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
                if (Commands.Count > 0)
                {
                    l.AddRange(GetTemplate("IJsonSender"));
                    l.Add("");
                    l.AddRange(GetJsonSerializationClient(Commands));
                    l.Add("");
                }

                l.AddRange(GetJsonTranslator(Schema.TypeRefs.Concat(Schema.Types).ToList()));
                l.Add("");

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l;
            }

            public List<String> GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static List<String> GetLines(String Value)
            {
                return Haxe.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return Haxe.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Haxe.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return Haxe.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return Haxe.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
