//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C++二进制代码生成器
//  Version:     2015.02.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.CppBinary
{
    public static class CodeGenerator
    {
        public static String CompileToCppBinary(this Schema Schema, String NamespaceName)
        {
            Writer w = new Writer(Schema, NamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCppBinary(this Schema Schema)
        {
            return CompileToCppBinary(Schema, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Cpp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private ISchemaClosureGenerator SchemaClosureGenerator;
            private String NamespaceName;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Cpp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CppBinary);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.NamespaceName = NamespaceName;
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { }).Hash();

                InnerWriter = new Cpp.Common.CodeGenerator.Writer(Schema, NamespaceName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gp.Type.TypeRef.Name) && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var Includes = Schema.Imports.Where(i => IsInclude(i)).ToArray();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();
                return EvaluateEscapedIdentifiers(GetMain(Header, Includes, Primitives, WrapContents(NamespaceName, ComplexTypes))).Select(Line => Line.TrimEnd(' ')).ToArray();
            }

            public String[] GetMain(String[] Header, String[] Includes, String[] Primitives, String[] ComplexTypes)
            {
                return InnerWriter.GetMain(Header, Includes, Primitives, new String[] { }, new String[] { }, ComplexTypes);
            }

            public String[] WrapContents(String Namespace, String[] Contents)
            {
                return InnerWriter.WrapContents(Namespace, Contents);
            }

            public Boolean IsInclude(String s)
            {
                return InnerWriter.IsInclude(s);
            }

            public String[] GetHeader()
            {
                return GetTemplate("Header");
            }

            public String[] GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(TypeSpec Type, Boolean ForceAsValue = false)
            {
                return InnerWriter.GetTypeString(Type, ForceAsValue);
            }
            public String[] GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public String[] GetBinarySerializationServer(TypeDef[] Commands)
            {
                return GetTemplate("BinarySerializationServer").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetBinarySerializationServerClientCommands(Commands)).Substitute("ServerCommands", GetBinarySerializationServerServerCommands(Commands));
            }
            public String[] GetBinarySerializationServerClientCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinarySerializationServer_ClientCommand").Substitute("CommandName", c.ClientCommand.Name.Escape()).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetBinarySerializationServerServerCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinarySerializationServer_ServerCommand").Substitute("CommandName", c.ServerCommand.Name.Escape()).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetBinarySerializationClient(TypeDef[] Commands)
            {
                return GetTemplate("BinarySerializationClient").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ApplicationCommands", GetBinarySerializationClientApplicationCommands(Commands)).Substitute("ServerCommands", GetBinarySerializationClientServerCommands(Commands));
            }
            public String[] GetBinarySerializationClientApplicationCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinarySerializationClient_ApplicationClientCommand").Substitute("CommandName", c.ClientCommand.Name.Escape()).Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).Substitute("XmlComment", GetXmlComment(c.ClientCommand.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetBinarySerializationClientServerCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinarySerializationClient_ServerCommand").Substitute("CommandName", c.ServerCommand.Name.Escape()).Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetBinaryTranslator()
            {
                return GetTemplate("BinaryTranslator").Substitute("Serializers", GetBinaryTranslatorSerializers());
            }

            public String[] GetBinaryTranslatorSerializers()
            {
                List<String> l = new List<String>();

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
                var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new TypeSpec[] { });
                var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
                var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

                foreach (var t in Tuples)
                {
                    l.AddRange(GetBinaryTranslatorTuple(t));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new VariableDef[] { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new VariableDef[] { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                    l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", "OptionalTag").Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "Int"));
                    l.Add("");
                }
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetBinaryTranslatorOptional(gts, GenericOptionalType));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gts.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetBinaryTranslatorList(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gts.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetBinaryTranslatorSet(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.GenericParameterValues.Length == 2)
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

                return l.ToArray();
            }
            public String[] GetBinaryTranslatorAlias(AliasDef a)
            {
                return GetTemplate("BinaryTranslator_Alias").Substitute("Name", a.TypeFriendlyName()).Substitute("ValueTypeFriendlyName", a.Type.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorRecord(RecordDef r)
            {
                return GetBinaryTranslatorRecord(r.TypeFriendlyName(), r.Fields);
            }
            public String[] GetBinaryTranslatorRecord(String Name, VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetBinaryTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetBinaryTranslatorFieldTos(Fields)));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorFieldFroms(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorFieldTos(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorTaggedUnion(TaggedUnionDef tu)
            {
                return GetBinaryTranslatorTaggedUnion(tu.TypeFriendlyName(), tu.Alternatives);
            }
            public String[] GetBinaryTranslatorTaggedUnion(String Name, VariableDef[] Alternatives)
            {
                var TagName = Name + "Tag";
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", TagName).Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "Int"));
                l.AddRange(GetTemplate("BinaryTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives)));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorAlternativeFroms(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorAlternativeTos(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorEnum(EnumDef e)
            {
                return GetTemplate("BinaryTranslator_Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingTypeFriendlyName", e.UnderlyingType.TypeFriendlyName()).Substitute("UnderlyingType", GetTypeString(e.UnderlyingType));
            }
            public String[] GetBinaryTranslatorClientCommand(ClientCommandDef c)
            {
                List<String> l = new List<String>();
                l.AddRange(GetBinaryTranslatorRecord(c.TypeFriendlyName() + "Request", c.OutParameters));
                l.AddRange(GetBinaryTranslatorTaggedUnion(c.TypeFriendlyName() + "Reply", c.InParameters));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorServerCommand(ServerCommandDef c)
            {
                List<String> l = new List<String>();
                return GetBinaryTranslatorRecord(c.TypeFriendlyName() + "Event", c.OutParameters);
            }
            public String[] GetBinaryTranslatorTuple(TypeSpec t)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Tuple").Substitute("TypeFriendlyName", t.TypeFriendlyName()).Substitute("TupleElementFroms", GetBinaryTranslatorTupleElementFroms(t.Tuple.Types)).Substitute("TupleElementTos", GetBinaryTranslatorTupleElementTos(t.Tuple.Types)));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorTupleElementFroms(TypeSpec[] Types)
            {
                List<String> l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_TupleElementFrom").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorTupleElementTos(TypeSpec[] Types)
            {
                List<String> l = new List<String>();
                int k = 0;
                foreach (var t in Types)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_TupleElementTo").Substitute("NameIndex", Convert.ToString(k)).Substitute("TypeFriendlyName", t.TypeFriendlyName()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorOptionalAlternativeFroms(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_OptionalAlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorOptionalAlternativeTos(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_OptionalAlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorOptional(TypeSpec o, TaggedUnionDef GenericOptionalType)
            {
                var ElementType = o.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o, true);
                var Name = "Optional";
                return GetTemplate("BinaryTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetBinaryTranslatorOptionalAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorOptionalAlternativeTos(Name, Alternatives));
            }
            public String[] GetBinaryTranslatorList(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l, true)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorSet(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_Set").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l, true)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorMap(TypeSpec l)
            {
                var gp = l.GenericTypeSpec.GenericParameterValues.ToArray();
                if (gp.Length != 2)
                {
                    throw new ArgumentException();
                }
                return GetTemplate("BinaryTranslator_Map").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l, true)).Substitute("KeyTypeFriendlyName", gp[0].TypeSpec.TypeFriendlyName()).Substitute("ValueTypeFriendlyName", gp[1].TypeSpec.TypeFriendlyName());
            }

            public String[] GetComplexTypes()
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

                l.AddRange(GetTemplate("Streams"));
                l.Add("");

                l.AddRange(GetBinaryTranslator());
                l.Add("");

                if (cl.Count > 0)
                {
                    var ca = cl.ToArray();

                    l.AddRange(GetBinarySerializationServer(ca));
                    l.Add("");
                    l.AddRange(GetTemplate("IBinarySender"));
                    l.Add("");
                    l.AddRange(GetBinarySerializationClient(ca));
                    l.Add("");
                }

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
            public static String[] GetLines(String Value)
            {
                return Cpp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return Cpp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return Cpp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
