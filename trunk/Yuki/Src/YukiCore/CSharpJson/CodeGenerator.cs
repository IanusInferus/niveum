//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C# JSON通讯代码生成器
//  Version:     2012.12.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.CSharpJson
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpJson(this Schema Schema, String NamespaceName)
        {
            Writer w = new Writer(Schema, NamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpJson(this Schema Schema)
        {
            return CompileToCSharpJson(Schema, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String NamespaceName;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpJson);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.NamespaceName = NamespaceName;
                this.Hash = Schema.Hash();
            }

            public String[] GetSchema()
            {
                InnerWriter = new CSharp.Common.CodeGenerator.Writer(Schema, NamespaceName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gp.Type.TypeRef.Name) && TemplateInfo.PrimitiveMappings[gp.Type.TypeRef.Name].PlatformName == "System.Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }

                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes(Schema);

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes));
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes));
                }
            }

            public String[] GetHeader()
            {
                return GetTemplate("Header");
            }

            public String[] GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public String[] GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public String[] GetJsonServer(TypeDef[] Commands)
            {
                return GetTemplate("JsonServer").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Commands", GetJsonServerCommands(Commands));
            }
            public String[] GetJsonServerCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        if (c.ClientCommand.Version == "")
                        {
                            l.AddRange(GetTemplate("JsonServer_ClientCommandWithoutHash").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                        }
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonServer_ClientCommand").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonServer_ServerCommand").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetJsonClient(TypeDef[] Commands)
            {
                return GetTemplate("JsonClient").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetJsonClientClientCommands(Commands)).Substitute("ServerCommands", GetJsonClientServerCommands(Commands));
            }
            public String[] GetJsonClientClientCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonClient_ClientCommand").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).Substitute("XmlComment", GetXmlComment(c.ClientCommand.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetJsonClientServerCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonClient_ServerCommand").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetJsonLogAspectWrapper(TypeDef[] Commands)
            {
                return GetTemplate("JsonLogAspectWrapper").Substitute("ServerCommandHooks", GetJsonLogAspectWrapperServerCommandHooks(Commands.Where(c => c.OnServerCommand).ToArray())).Substitute("Commands", GetJsonLogAspectWrapperCommands(Commands));
            }
            public String[] GetJsonLogAspectWrapperServerCommandHooks(TypeDef[] ServerCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    l.AddRange(GetTemplate("JsonLogAspectWrapper_ServerCommandHook").Substitute("Name", c.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonLogAspectWrapperCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        l.AddRange(GetTemplate("JsonLogAspectWrapper_ClientCommandHook").Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("JsonLogAspectWrapper_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()));
                    }
                }
                return l.ToArray();
            }

            public String[] GetJsonTranslator(TypeDef[] Types)
            {
                return GetTemplate("JsonTranslator").Substitute("Serializers", GetJsonTranslatorSerializers(Types));
            }

            public String[] GetJsonTranslatorSerializers(TypeDef[] Types)
            {
                List<String> l = new List<String>();

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

                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Types);
                var Tuples = ltf.GetTuples();
                var GenericTypeSpecs = ltf.GetGenericTypeSpecs();

                foreach (var t in Tuples)
                {
                    l.AddRange(GetJsonTranslatorTuple(t));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new VariableDef[] { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new VariableDef[] { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                    l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", "OptionalTag"));
                    l.Add("");
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gps.GenericTypeSpec.TypeSpec.TypeRef.Name) && TemplateInfo.PrimitiveMappings[gps.GenericTypeSpec.TypeSpec.TypeRef.Name].PlatformName == "System.Collections.Generic.List")
                    {
                        l.AddRange(GetJsonTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                    {
                        l.AddRange(GetJsonTranslatorOptional(gps, GenericOptionalType));
                        l.Add("");
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gps.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
                    }
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlias(AliasDef a)
            {
                return GetTemplate("JsonTranslator_Alias").Substitute("Name", a.TypeFriendlyName()).Substitute("ValueTypeFriendlyName", a.Type.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorRecord(RecordDef a)
            {
                return GetJsonTranslatorRecord(a.TypeFriendlyName(), a.Fields);
            }
            public String[] GetJsonTranslatorRecord(String Name, VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetJsonTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetJsonTranslatorFieldTos(Fields)));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldFroms(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldTos(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorTaggedUnion(TaggedUnionDef tu)
            {
                return GetJsonTranslatorTaggedUnion(tu.TypeFriendlyName(), tu.Alternatives);
            }
            public String[] GetJsonTranslatorTaggedUnion(String Name, VariableDef[] Alternatives)
            {
                var TagName = Name + "Tag";
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", TagName));
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives)));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeFroms(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeTos(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorEnum(EnumDef e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorClientCommand(ClientCommandDef c)
            {
                List<String> l = new List<String>();
                l.AddRange(GetJsonTranslatorRecord(c.TypeFriendlyName() + "Request", c.OutParameters));
                l.AddRange(GetJsonTranslatorTaggedUnion(c.TypeFriendlyName() + "Reply", c.InParameters));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorServerCommand(ServerCommandDef c)
            {
                List<String> l = new List<String>();
                return GetJsonTranslatorRecord(c.TypeFriendlyName() + "Event", c.OutParameters);
            }
            public String[] GetJsonTranslatorTuple(TypeSpec t)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Tuple").Substitute("TypeFriendlyName", t.TypeFriendlyName()).Substitute("TupleElementFroms", GetJsonTranslatorTupleElementFroms(t.Tuple.Types)).Substitute("TupleElementTos", GetJsonTranslatorTupleElementTos(t.Tuple.Types)));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorTupleElementFroms(TypeSpec[] Types)
            {
                List<String> l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("JsonTranslator_TupleElementFrom").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorTupleElementTos(TypeSpec[] Types)
            {
                List<String> l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("JsonTranslator_TupleElementTo").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorList(TypeSpec l)
            {
                return GetTemplate("JsonTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorOptional(TypeSpec o, TaggedUnionDef GenericOptionalType)
            {
                var ElementType = o.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o);
                var Name = "Optional";
                return GetTemplate("JsonTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives));
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

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
                    var ca = cl.ToArray();

                    l.AddRange(GetJsonServer(ca));
                    l.Add("");
                    l.AddRange(GetTemplate("IJsonSender"));
                    l.Add("");
                    l.AddRange(GetJsonClient(ca));
                    l.Add("");
                }

                var oca = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToArray();
                if (oca.Length > 0)
                {
                    l.AddRange(GetJsonLogAspectWrapper(oca));
                    l.Add("");
                }

                l.AddRange(GetJsonTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray()));
                l.Add("");

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public String[] GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n');
            }
            public String GetEscapedIdentifier(String Identifier)
            {
                return InnerWriter.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return InnerWriter.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
