﻿//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C# JSON通讯代码生成器
//  Version:     2012.04.17.
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
            var s = Schema.Reduce();
            var h = s.Hash();
            Writer w = new Writer() { Schema = s, NamespaceName = NamespaceName, Hash = h };
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

            public Schema Schema;
            public String NamespaceName;
            public UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpJson);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public String[] GetSchema()
            {
                InnerWriter = new CSharp.Common.CodeGenerator.Writer { Schema = Schema, NamespaceName = NamespaceName };

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

            public String[] GetJsonServer(CommandDef[] Commands)
            {
                return GetTemplate("JsonServer").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Commands", GetJsonServerCommands(Commands));
            }
            public String[] GetJsonServerCommands(CommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandDefTag.Client)
                    {
                        l.AddRange(GetTemplate("JsonServer_ClientCommand").Substitute("Name", c.Client.TypeFriendlyName()));
                    }
                    else if (c._Tag == CommandDefTag.Server)
                    {
                        l.AddRange(GetTemplate("JsonServer_ServerCommand").Substitute("Name", c.Server.TypeFriendlyName()));
                    }
                }
                return l.ToArray();
            }

            public String[] GetJsonClient(CommandDef[] Commands)
            {
                return GetTemplate("JsonClient").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetJsonClientClientCommands(Commands)).Substitute("ServerCommands", GetJsonClientServerCommands(Commands));
            }
            public String[] GetJsonClientClientCommands(CommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandDefTag.Client)
                    {
                        l.AddRange(GetTemplate("JsonClient_ClientCommand").Substitute("Name", c.Client.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.Client.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetJsonClientServerCommands(CommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandDefTag.Server)
                    {
                        l.AddRange(GetTemplate("JsonClient_ServerCommand").Substitute("Name", c.Server.TypeFriendlyName()));
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
                    GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
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

                List<CommandDef> cl = new List<CommandDef>();

                foreach (var c in Schema.Types)
                {
                    if (c.OnClientCommand && c.ClientCommand.Version == "")
                    {
                        cl.Add(CommandDef.CreateClient(c.ClientCommand));
                    }
                    else if (c.OnServerCommand && c.ServerCommand.Version == "")
                    {
                        cl.Add(CommandDef.CreateServer(c.ServerCommand));
                    }
                }

                var ca = cl.ToArray();
                
                l.AddRange(GetJsonServer(ca));
                l.Add("");
                l.AddRange(GetTemplate("IJsonSender"));
                l.Add("");
                l.AddRange(GetJsonClient(ca));
                l.Add("");

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
        private static String LowercaseCamelize(String PascalName)
        {
            return CSharp.Common.CodeGenerator.LowercaseCamelize(PascalName);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
