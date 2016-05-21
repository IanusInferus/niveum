//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C# JSON通讯代码生成器
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

namespace Yuki.ObjectSchema.CSharpJson
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpJson(this Schema Schema, String NamespaceName, HashSet<String> AsyncCommands)
        {
            var w = new Writer(Schema, NamespaceName, AsyncCommands);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpJson(this Schema Schema)
        {
            return CompileToCSharpJson(Schema, "", new HashSet<String> { });
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private ISchemaClosureGenerator SchemaClosureGenerator;
            private String NamespaceName;
            private HashSet<String> AsyncCommands;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpJson);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName, HashSet<String> AsyncCommands)
            {
                this.Schema = Schema;
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.NamespaceName = NamespaceName;
                this.AsyncCommands = AsyncCommands;
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).Hash();

                InnerWriter = new CSharp.Common.CodeGenerator.Writer(Schema, NamespaceName, AsyncCommands, false);

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
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
            }

            public List<String> GetHeader()
            {
                return GetTemplate("Header");
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public List<String> GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public List<String> GetJsonSerializationServer(List<TypeDef> Commands)
            {
                return GetTemplate("JsonSerializationServer").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetJsonSerializationServerClientCommands(Commands)).Substitute("ServerCommands", GetJsonSerializationServerServerCommands(Commands));
            }
            public List<String> GetJsonSerializationServerClientCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        if (c.ClientCommand.Version == "")
                        {
                            if (AsyncCommands.Contains(c.ClientCommand.Name))
                            {
                                l.AddRange(GetTemplate("JsonSerializationServer_ClientCommandAsyncWithoutHash").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                            }
                            else
                            {
                                l.AddRange(GetTemplate("JsonSerializationServer_ClientCommandWithoutHash").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                            }
                        }
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        if (AsyncCommands.Contains(c.ClientCommand.Name))
                        {
                            l.AddRange(GetTemplate("JsonSerializationServer_ClientCommandAsync").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        else
                        {
                            l.AddRange(GetTemplate("JsonSerializationServer_ClientCommand").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                    }
                }
                return l;
            }
            public List<String> GetJsonSerializationServerServerCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonSerializationServer_ServerCommand").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l;
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

            public List<String> GetJsonLogAspectWrapper(List<TypeDef> Commands)
            {
                return GetTemplate("JsonLogAspectWrapper").Substitute("ServerCommandHooks", GetJsonLogAspectWrapperServerCommandHooks(Commands.Where(c => c.OnServerCommand).ToList())).Substitute("Commands", GetJsonLogAspectWrapperCommands(Commands));
            }
            public List<String> GetJsonLogAspectWrapperServerCommandHooks(List<TypeDef> ServerCommands)
            {
                var l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    l.AddRange(GetTemplate("JsonLogAspectWrapper_ServerCommandHook").Substitute("Name", c.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonLogAspectWrapperCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        if (AsyncCommands.Contains(c.ClientCommand.Name))
                        {
                            l.AddRange(GetTemplate("JsonLogAspectWrapper_ClientCommandAsyncHook").Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                        }
                        else
                        {
                            l.AddRange(GetTemplate("JsonLogAspectWrapper_ClientCommandHook").Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                        }
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("JsonLogAspectWrapper_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()));
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

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Count > 0)
                {
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Description = "" } }, Description = "" };
                    l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", "OptionalTag"));
                    l.Add("");
                }
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetJsonTranslatorOptional(gts, GenericOptionalType));
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
                        throw new InvalidOperationException(String.Format("GenericTypeNotSupported: {0}", gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
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
                var TagName = Name + "Tag";
                var l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", TagName));
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives)));
                return l;
            }
            public List<String> GetJsonTranslatorAlternativeFroms(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorAlternativeTos(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorEnum(EnumDef e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.TypeFriendlyName());
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
            public List<String> GetJsonTranslatorOptional(TypeSpec o, TaggedUnionDef GenericOptionalType)
            {
                var ElementType = o.GenericTypeSpec.ParameterValues.Single();
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o);
                var Name = "Optional";
                return GetTemplate("JsonTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives));
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

            public List<String> GetComplexTypes()
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
                    l.AddRange(GetJsonSerializationServer(cl));
                    l.Add("");
                    l.AddRange(GetTemplate("IJsonSender"));
                    l.Add("");
                    l.AddRange(GetJsonSerializationClient(cl));
                    l.Add("");
                }

                var ocl = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToList();
                if (ocl.Count > 0)
                {
                    l.AddRange(GetJsonLogAspectWrapper(ocl));
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
                return CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
