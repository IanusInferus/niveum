//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#通讯代码生成器
//  Version:     2012.02.24.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Streaming;
using Firefly.Mapping.MetaSchema;
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema.CSharpCommunication
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpCommunication(this Schema Schema, String NamespaceName)
        {
            var s = Schema.Reduce();
            var h = s.Hash();
            Writer w = new Writer() { Schema = s, NamespaceName = NamespaceName, Hash = h };
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpCommunication(this Schema Schema)
        {
            return CompileToCSharpCommunication(Schema, "");
        }

        public class Writer
        {
            private static CSharp.Common.CodeGenerator.TemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            public Schema Schema;
            public String NamespaceName;
            public UInt64 Hash;

            static Writer()
            {
                var b = Properties.Resources.CSharpCommunication;
                XElement x;
                using (ByteArrayStream s = new ByteArrayStream(b))
                {
                    using (var sr = Txt.CreateTextReader(s.AsNewReading(), TextEncoding.Default, true))
                    {
                        x = TreeFile.ReadFile(sr);
                    }
                }

                XmlSerializer xs = new XmlSerializer();
                var t = xs.Read<ObjectSchemaTemplate>(x);
                TemplateInfo = new CSharp.Common.CodeGenerator.TemplateInfo(t);
            }

            public String[] GetSchema()
            {
                InnerWriter = new CSharp.Common.CodeGenerator.Writer { Schema = Schema, NamespaceName = NamespaceName };

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gp.Type.TypeRef.Value) && TemplateInfo.PrimitiveMappings[gp.Type.TypeRef.Value].PlatformName == "System.Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.Name()));
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
            public String[] GetAlias(Alias a)
            {
                return InnerWriter.GetAlias(a);
            }
            public String[] GetTuple(String Name, Tuple t)
            {
                return InnerWriter.GetTuple(Name, t);
            }
            public String[] GetRecord(Record r)
            {
                return InnerWriter.GetRecord(r);
            }
            public String[] GetTaggedUnion(TaggedUnion tu)
            {
                return InnerWriter.GetTaggedUnion(tu);
            }
            public String[] GetEnum(Enum e)
            {
                return InnerWriter.GetEnum(e);
            }
            public String[] GetClientCommand(ClientCommand c)
            {
                var l = new List<String>();
                l.AddRange(GetRecord(new Record { Name = c.Name + "Request", GenericParameters = new Variable[] { }, Fields = c.OutParameters, Description = c.Description }));
                l.AddRange(GetTaggedUnion(new TaggedUnion { Name = c.Name + "Reply", GenericParameters = new Variable[] { }, Alternatives = c.InParameters, Description = c.Description }));
                return l.ToArray();
            }
            public String[] GetServerCommand(ServerCommand c)
            {
                return GetRecord(new Record { Name = c.Name + "Event", GenericParameters = new Variable[] { }, Fields = c.OutParameters, Description = c.Description });
            }
            public String[] GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public enum CommandTag
            {
                Client = 0,
                Server = 1
            }
            [TaggedUnion]
            public class Command
            {
                [Tag]
                public CommandTag _Tag;
                public ClientCommand Client;
                public ServerCommand Server;
            }

            public String[] GetIServerImplementation(Command[] Commands)
            {
                return GetTemplate("IServerImplementation").Substitute("Commands", GetIServerImplementationCommands(Commands));
            }
            public String[] GetIServerImplementationCommands(Command[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandTag.Client)
                    {
                        l.AddRange(GetTemplate("IServerImplementation_ClientCommand").Substitute("Name", c.Client.Name).Substitute("XmlComment", GetXmlComment(c.Client.Description)));
                    }
                    else if (c._Tag == CommandTag.Server)
                    {
                        l.AddRange(GetTemplate("IServerImplementation_ServerCommand").Substitute("Name", c.Server.Name).Substitute("XmlComment", GetXmlComment(c.Server.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetServer(Command[] Commands)
            {
                return GetTemplate("Server").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Commands", GetServerCommands(Commands));
            }
            public String[] GetServerCommands(Command[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandTag.Client)
                    {
                        l.AddRange(GetTemplate("Server_ClientCommand").Substitute("Name", c.Client.Name));
                    }
                    else if (c._Tag == CommandTag.Server)
                    {
                        l.AddRange(GetTemplate("Server_ServerCommand").Substitute("Name", c.Server.Name));
                    }
                }
                return l.ToArray();
            }

            public String[] GetIClientImplementation(Command[] Commands)
            {
                return GetTemplate("IClientImplementation").Substitute("Commands", GetIClientImplementationCommands(Commands));
            }
            public String[] GetIClientImplementationCommands(Command[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandTag.Server)
                    {
                        l.AddRange(GetTemplate("IClientImplementation_ServerCommand").Substitute("Name", c.Server.Name).Substitute("XmlComment", GetXmlComment(c.Server.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetClient(Command[] Commands)
            {
                return GetTemplate("Client").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetClientClientCommands(Commands)).Substitute("ServerCommands", GetClientServerCommands(Commands));
            }
            public String[] GetClientClientCommands(Command[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandTag.Client)
                    {
                        l.AddRange(GetTemplate("Client_ClientCommand").Substitute("Name", c.Client.Name).Substitute("XmlComment", GetXmlComment(c.Client.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetClientServerCommands(Command[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandTag.Server)
                    {
                        l.AddRange(GetTemplate("Client_ServerCommand").Substitute("Name", c.Server.Name));
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
                TaggedUnion GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                    l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", "OptionalTag"));
                    l.Add("");
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gps.GenericTypeSpec.TypeSpec.TypeRef.Value) && TemplateInfo.PrimitiveMappings[gps.GenericTypeSpec.TypeSpec.TypeRef.Value].PlatformName == "System.Collections.Generic.List")
                    {
                        l.AddRange(GetJsonTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef == "Optional")
                    {
                        l.AddRange(GetJsonTranslatorOptional(gps, GenericOptionalType));
                        l.Add("");
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gps.GenericTypeSpec.TypeSpec.TypeRef.Value));
                    }
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlias(Alias a)
            {
                return GetTemplate("JsonTranslator_Alias").Substitute("Name", a.Name).Substitute("ValueTypeFriendlyName", a.Type.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorRecord(Record a)
            {
                return GetJsonTranslatorRecord(a.Name, a.Fields);
            }
            public String[] GetJsonTranslatorRecord(String Name, Variable[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetJsonTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetJsonTranslatorFieldTos(Fields)));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldFroms(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldTos(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorTaggedUnion(TaggedUnion tu)
            {
                return GetJsonTranslatorTaggedUnion(tu.Name, tu.Alternatives);
            }
            public String[] GetJsonTranslatorTaggedUnion(String Name, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", Name + "Tag"));
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives)));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeFroms(String TaggedUnionName, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeTos(String TaggedUnionName, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorEnum(Enum e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.Name);
            }
            public String[] GetJsonTranslatorClientCommand(ClientCommand c)
            {
                List<String> l = new List<String>();
                l.AddRange(GetJsonTranslatorRecord(c.Name + "Request", c.OutParameters));
                l.AddRange(GetJsonTranslatorTaggedUnion(c.Name + "Reply", c.InParameters));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorServerCommand(ServerCommand c)
            {
                List<String> l = new List<String>();
                return GetJsonTranslatorRecord(c.Name + "Event", c.OutParameters);
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
            public String[] GetJsonTranslatorOptional(TypeSpec o, TaggedUnion GenericOptionalType)
            {
                var ElementType = o.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new Variable { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o);
                var Name = "Optional";
                return GetTemplate("JsonTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives));
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                List<Command> cl = new List<Command>();

                foreach (var c in Schema.Types)
                {
                    if (c.OnPrimitive)
                    {
                        continue;
                    }
                    else if (c.OnAlias)
                    {
                        l.AddRange(GetAlias(c.Alias));
                    }
                    else if (c.OnRecord)
                    {
                        l.AddRange(GetRecord(c.Record));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        l.AddRange(GetTaggedUnion(c.TaggedUnion));
                    }
                    else if (c.OnEnum)
                    {
                        l.AddRange(GetEnum(c.Enum));
                    }
                    else if (c.OnClientCommand)
                    {
                        l.AddRange(GetClientCommand(c.ClientCommand));
                        cl.Add(new Command { _Tag = CommandTag.Client, Client = c.ClientCommand });
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetServerCommand(c.ServerCommand));
                        cl.Add(new Command { _Tag = CommandTag.Server, Server = c.ServerCommand });
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    l.Add("");
                }

                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Schema.Types);
                var Tuples = ltf.GetTuples();
                foreach (var t in Tuples)
                {
                    l.AddRange(GetTuple(t.TypeFriendlyName(), t.Tuple));
                    l.Add("");
                }

                var ca = cl.ToArray();
                
                l.AddRange(GetIServerImplementation(ca));
                l.Add("");
                l.AddRange(GetServer(ca));
                l.Add("");

                l.AddRange(GetTemplate("ISender"));
                l.Add("");
                l.AddRange(GetIClientImplementation(ca));
                l.Add("");
                l.AddRange(GetClient(ca));
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
