//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#二进制通讯代码生成器
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

namespace Yuki.ObjectSchema.CSharpBinary
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpBinary(this Schema Schema, String NamespaceName, HashSet<String> AsyncCommands, Boolean WithFirefly)
        {
            var w = new Writer(Schema, NamespaceName, AsyncCommands, WithFirefly);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpBinary(this Schema Schema)
        {
            return CompileToCSharpBinary(Schema, "", new HashSet<String> { }, true);
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private ISchemaClosureGenerator SchemaClosureGenerator;
            private String NamespaceName;
            private HashSet<String> AsyncCommands;
            private Boolean WithFirefly;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpBinary);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName, HashSet<String> AsyncCommands, Boolean WithFirefly)
            {
                this.Schema = Schema;
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.NamespaceName = NamespaceName;
                this.AsyncCommands = AsyncCommands;
                this.WithFirefly = WithFirefly;
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).Hash();

                InnerWriter = new CSharp.Common.CodeGenerator.Writer(Schema, NamespaceName, AsyncCommands, WithFirefly);

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
                if (WithFirefly)
                {
                    return GetTemplate("Header_WithFirefly");
                }
                else
                {
                    return GetTemplate("Header");
                }
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

            public List<String> GetBinarySerializationServer(List<TypeDef> Commands)
            {
                return GetTemplate("BinarySerializationServer").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetBinarySerializationServerClientCommands(Commands)).Substitute("ServerCommands", GetBinarySerializationServerServerCommands(Commands));
            }
            public List<String> GetBinarySerializationServerClientCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        if (WithFirefly)
                        {
                            if (AsyncCommands.Contains(c.ClientCommand.Name))
                            {
                                l.AddRange(GetTemplate("BinarySerializationServer_ClientCommandAsync_WithFirefly").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                            }
                            else
                            {
                                l.AddRange(GetTemplate("BinarySerializationServer_ClientCommand_WithFirefly").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                            }
                        }
                        else
                        {
                            if (AsyncCommands.Contains(c.ClientCommand.Name))
                            {
                                l.AddRange(GetTemplate("BinarySerializationServer_ClientCommandAsync").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                            }
                            else
                            {
                                l.AddRange(GetTemplate("BinarySerializationServer_ClientCommand").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                            }
                        }
                    }
                }
                return l;
            }
            public List<String> GetBinarySerializationServerServerCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        if (WithFirefly)
                        {
                            l.AddRange(GetTemplate("BinarySerializationServer_ServerCommand_WithFirefly").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        else
                        {
                            l.AddRange(GetTemplate("BinarySerializationServer_ServerCommand").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                    }
                }
                return l;
            }

            public List<String> GetBinarySerializationClient(List<TypeDef> Commands)
            {
                return GetTemplate("BinarySerializationClient").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ApplicationCommands", GetBinarySerializationClientApplicationCommands(Commands)).Substitute("ServerCommands", GetBinarySerializationClientServerCommands(Commands));
            }
            public List<String> GetBinarySerializationClientApplicationCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        if (WithFirefly)
                        {
                            l.AddRange(GetTemplate("BinarySerializationClient_ApplicationClientCommand_WithFirefly").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        else
                        {
                            l.AddRange(GetTemplate("BinarySerializationClient_ApplicationClientCommand").Substitute("CommandName", c.ClientCommand.Name).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("BinarySerializationClient_ApplicationServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()));
                    }
                }
                return l;
            }
            public List<String> GetBinarySerializationClientServerCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        if (WithFirefly)
                        {
                            l.AddRange(GetTemplate("BinarySerializationClient_ServerCommand_WithFirefly").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                        else
                        {
                            l.AddRange(GetTemplate("BinarySerializationClient_ServerCommand").Substitute("CommandName", c.ServerCommand.Name).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                        }
                    }
                }
                return l;
            }

            public List<String> GetBinaryTranslator()
            {
                if (WithFirefly)
                {
                    return GetTemplate("BinaryTranslator_WithFirefly");
                }
                else
                {
                    return GetTemplate("BinaryTranslator").Substitute("Serializers", GetBinaryTranslatorSerializers());
                }
            }

            public List<String> GetBinaryTranslatorSerializers()
            {
                var l = new List<String>();

                foreach (var c in Schema.TypeRefs.Concat(Schema.Types))
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
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Unit"));
                                break;
                            case "Boolean":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Boolean"));
                                break;
                            case "String":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_String"));
                                break;
                            case "Int":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Int"));
                                break;
                            case "Real":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Real"));
                                break;
                            case "Byte":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Byte"));
                                break;
                            case "UInt8":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_UInt8"));
                                break;
                            case "UInt16":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_UInt16"));
                                break;
                            case "UInt32":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_UInt32"));
                                break;
                            case "UInt64":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_UInt64"));
                                break;
                            case "Int8":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Int8"));
                                break;
                            case "Int16":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Int16"));
                                break;
                            case "Int32":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Int32"));
                                break;
                            case "Int64":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Int64"));
                                break;
                            case "Float32":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Float32"));
                                break;
                            case "Float64":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Float64"));
                                break;
                            case "Type":
                                l.AddRange(GetTemplate("BinaryTranslator_Primitive_Type"));
                                break;
                            default:
                                throw new InvalidOperationException();
                        }
                    }
                    else if (c.OnAlias)
                    {
                        l.AddRange(GetBinaryTranslatorAlias(c.Alias));
                    }
                    else if (c.OnRecord)
                    {
                        l.AddRange(GetBinaryTranslatorRecord(c.Record));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        l.AddRange(GetBinaryTranslatorTaggedUnion(c.TaggedUnion));
                    }
                    else if (c.OnEnum)
                    {
                        l.AddRange(GetBinaryTranslatorEnum(c.Enum));
                    }
                    else if (c.OnClientCommand)
                    {
                        l.AddRange(GetBinaryTranslatorClientCommand(c.ClientCommand));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetBinaryTranslatorServerCommand(c.ServerCommand));
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
                    l.AddRange(GetBinaryTranslatorTuple(t));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Count > 0)
                {
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Description = "" } }, Description = "" };
                    l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", "OptionalTag").Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "Int"));
                    l.Add("");
                }
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetBinaryTranslatorOptional(gts, GenericOptionalType));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetBinaryTranslatorList(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        l.AddRange(GetBinaryTranslatorSet(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.ParameterValues.Count == 2)
                    {
                        l.AddRange(GetBinaryTranslatorMap(gts));
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
            public List<String> GetBinaryTranslatorAlias(AliasDef a)
            {
                return GetTemplate("BinaryTranslator_Alias").Substitute("Name", a.TypeFriendlyName()).Substitute("ValueTypeFriendlyName", a.Type.TypeFriendlyName());
            }
            public List<String> GetBinaryTranslatorRecord(RecordDef r)
            {
                return GetBinaryTranslatorRecord(r.TypeFriendlyName(), r.Fields);
            }
            public List<String> GetBinaryTranslatorRecord(String Name, List<VariableDef> Fields)
            {
                var l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetBinaryTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetBinaryTranslatorFieldTos(Fields)));
                return l;
            }
            public List<String> GetBinaryTranslatorFieldFroms(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetBinaryTranslatorFieldTos(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetBinaryTranslatorTaggedUnion(TaggedUnionDef tu)
            {
                return GetBinaryTranslatorTaggedUnion(tu.TypeFriendlyName(), tu.Alternatives);
            }
            public List<String> GetBinaryTranslatorTaggedUnion(String Name, List<VariableDef> Alternatives)
            {
                var TagName = Name + "Tag";
                var l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", TagName).Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "Int"));
                l.AddRange(GetTemplate("BinaryTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives)));
                return l;
            }
            public List<String> GetBinaryTranslatorAlternativeFroms(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetBinaryTranslatorAlternativeTos(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetBinaryTranslatorEnum(EnumDef e)
            {
                return GetTemplate("BinaryTranslator_Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingTypeFriendlyName", e.UnderlyingType.TypeFriendlyName()).Substitute("UnderlyingType", GetTypeString(e.UnderlyingType));
            }
            public List<String> GetBinaryTranslatorClientCommand(ClientCommandDef c)
            {
                var l = new List<String>();
                l.AddRange(GetBinaryTranslatorRecord(c.TypeFriendlyName() + "Request", c.OutParameters));
                l.AddRange(GetBinaryTranslatorTaggedUnion(c.TypeFriendlyName() + "Reply", c.InParameters));
                return l;
            }
            public List<String> GetBinaryTranslatorServerCommand(ServerCommandDef c)
            {
                var l = new List<String>();
                return GetBinaryTranslatorRecord(c.TypeFriendlyName() + "Event", c.OutParameters);
            }
            public List<String> GetBinaryTranslatorTuple(TypeSpec t)
            {
                var l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Tuple").Substitute("TypeFriendlyName", t.TypeFriendlyName()).Substitute("TupleElementFroms", GetBinaryTranslatorTupleElementFroms(t.Tuple)).Substitute("TupleElementTos", GetBinaryTranslatorTupleElementTos(t.Tuple)));
                return l;
            }
            public List<String> GetBinaryTranslatorTupleElementFroms(List<TypeSpec> Types)
            {
                var l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_TupleElementFrom").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l;
            }
            public List<String> GetBinaryTranslatorTupleElementTos(List<TypeSpec> Types)
            {
                var l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_TupleElementTo").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l;
            }
            public List<String> GetBinaryTranslatorOptional(TypeSpec o, TaggedUnionDef GenericOptionalType)
            {
                var ElementType = o.GenericTypeSpec.ParameterValues.Single();
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o);
                var Name = "Optional";
                return GetTemplate("BinaryTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives));
            }
            public List<String> GetBinaryTranslatorList(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName());
            }
            public List<String> GetBinaryTranslatorSet(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_Set").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName());
            }
            public List<String> GetBinaryTranslatorMap(TypeSpec l)
            {
                var gp = l.GenericTypeSpec.ParameterValues;
                if (gp.Count != 2)
                {
                    throw new ArgumentException();
                }
                return GetTemplate("BinaryTranslator_Map").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("KeyTypeFriendlyName", gp[0].TypeFriendlyName()).Substitute("ValueTypeFriendlyName", gp[1].TypeFriendlyName());
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
                    l.AddRange(GetBinarySerializationServer(cl));
                    l.Add("");
                    l.AddRange(GetTemplate("IBinarySender"));
                    l.Add("");
                    l.AddRange(GetBinarySerializationClient(cl));
                    l.Add("");
                }

                if (!WithFirefly)
                {
                    l.AddRange(GetTemplate("Streams"));
                    l.Add("");
                }

                l.AddRange(GetBinaryTranslator());
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
