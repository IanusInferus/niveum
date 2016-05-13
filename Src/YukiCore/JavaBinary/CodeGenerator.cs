//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Java二进制代码生成器
//  Version:     2016.05.13.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.JavaBinary
{
    public static class CodeGenerator
    {
        public static String CompileToJavaBinary(this Schema Schema, String ClassName, String PackageName)
        {
            var w = new Writer(Schema, ClassName, PackageName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToJavaBinary(this Schema Schema, String ClassName)
        {
            return CompileToJavaBinary(Schema, ClassName, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Java.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String ClassName;
            private String PackageName;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Java);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.JavaBinary);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String ClassName, String PackageName)
            {
                this.Schema = Schema;
                this.ClassName = ClassName;
                this.PackageName = PackageName;

                InnerWriter = new Java.Common.CodeGenerator.Writer(Schema, ClassName, PackageName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gp.Type.TypeRef.Name) && gp.Type.TypeRef.Name == "Type"))
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

                return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("Header", Header).Substitute("ClassName", ClassName).Substitute("PackageName", PackageName == "" ? new List<String> { } : new List<String> { PackageName }).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
            }

            public List<String> GetHeader()
            {
                return GetTemplate("Header");
            }

            public List<String> GetPredefinedTypes()
            {
                return InnerWriter.GetPredefinedTypes();
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public List<String> GetAlias(AliasDef a)
            {
                return InnerWriter.GetAlias(a);
            }
            public List<String> GetTuple(String Name, TupleDef t)
            {
                return InnerWriter.GetTuple(Name, t);
            }
            public List<String> GetRecord(RecordDef r)
            {
                return InnerWriter.GetRecord(r);
            }
            public List<String> GetTaggedUnion(TaggedUnionDef tu)
            {
                return InnerWriter.GetTaggedUnion(tu);
            }
            public List<String> GetEnum(EnumDef e)
            {
                return InnerWriter.GetEnum(e);
            }
            public List<String> GetClientCommand(ClientCommandDef c)
            {
                var l = new List<String>();
                l.AddRange(GetRecord(new RecordDef { Name = c.TypeFriendlyName() + "Request", Version = "", GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Description = c.Description }));
                l.AddRange(GetTaggedUnion(new TaggedUnionDef { Name = c.TypeFriendlyName() + "Reply", Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = c.InParameters, Description = c.Description }));
                return l;
            }
            public List<String> GetServerCommand(ServerCommandDef c)
            {
                return GetRecord(new RecordDef { Name = c.TypeFriendlyName() + "Event", Version = "", GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Description = c.Description });
            }
            public List<String> GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public List<String> GetBinaryTranslator(List<TypeDef> Types)
            {
                return GetTemplate("BinaryTranslator").Substitute("Serializers", GetBinaryTranslatorSerializers(Types));
            }

            public List<String> GetBinaryTranslatorSerializers(List<TypeDef> Types)
            {
                var l = new List<String>();

                var Dict = Types.ToDictionary(t => t.VersionedName());
                if (!Dict.ContainsKey("Unit"))
                {
                    l.AddRange(GetTemplate("BinaryTranslator_Primitive_Unit"));
                }
                if (!Dict.ContainsKey("Boolean"))
                {
                    l.AddRange(GetTemplate("BinaryTranslator_Primitive_Boolean"));
                }
                if (!Dict.ContainsKey("Int"))
                {
                    l.AddRange(GetTemplate("BinaryTranslator_Primitive_Int"));
                }
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
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                    l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", "OptionalTag").Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "int"));
                    l.Add("");
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gps.GenericTypeSpec.GenericParameterValues.Count == 1)
                    {
                        l.AddRange(GetBinaryTranslatorOptional(gps, GenericOptionalType));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gps.GenericTypeSpec.GenericParameterValues.Count == 1)
                    {
                        l.AddRange(GetBinaryTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gps.GenericTypeSpec.GenericParameterValues.Count == 1)
                    {
                        l.AddRange(GetBinaryTranslatorSet(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gps.GenericTypeSpec.GenericParameterValues.Count == 2)
                    {
                        l.AddRange(GetBinaryTranslatorMap(gps));
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
                l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", TagName).Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "int"));
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
                l.AddRange(GetTemplate("BinaryTranslator_Tuple").Substitute("TypeFriendlyName", t.TypeFriendlyName()).Substitute("TupleElementFroms", GetBinaryTranslatorTupleElementFroms(t.Tuple.Types)).Substitute("TupleElementTos", GetBinaryTranslatorTupleElementTos(t.Tuple.Types)));
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
                var ElementType = o.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o);
                var Name = TypeString;
                return GetTemplate("BinaryTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives));
            }
            public List<String> GetBinaryTranslatorList(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public List<String> GetBinaryTranslatorSet(TypeSpec l)
            {
                var et = l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                return GetTemplate("BinaryTranslator_Set").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", et.TypeFriendlyName()).Substitute("ElementTypeString", GetTypeString(et));
            }
            public List<String> GetBinaryTranslatorMap(TypeSpec l)
            {
                var gp = l.GenericTypeSpec.GenericParameterValues;
                if (gp.Count != 2)
                {
                    throw new ArgumentException();
                }
                var kt = gp[0].TypeSpec;
                return GetTemplate("BinaryTranslator_Map").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("KeyTypeFriendlyName", kt.TypeFriendlyName()).Substitute("ValueTypeFriendlyName", gp[1].TypeSpec.TypeFriendlyName()).Substitute("KeyTypeString", GetTypeString(kt));
            }

            public List<String> GetComplexTypes()
            {
                var l = new List<String>();

                if (Schema.TypeRefs.Count == 0)
                {
                    l.AddRange(GetPredefinedTypes());
                }

                foreach (var c in Schema.Types)
                {
                    if (c.GenericParameters().Count() != 0)
                    {
                        continue;
                    }
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
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetServerCommand(c.ServerCommand));
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
                    l.AddRange(GetTuple(t.TypeFriendlyName(), t.Tuple));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
                if (GenericOptionalTypes.Count > 0)
                {
                    var GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                    foreach (var gts in GenericTypeSpecs)
                    {
                        if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.GenericParameterValues.Count == 1)
                        {
                            var ElementType = gts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                            var Name = "Opt" + ElementType.TypeFriendlyName();
                            var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();
                            l.AddRange(GetTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
                            l.Add("");
                        }
                    }
                }

                l.AddRange(GetTemplate("Streams"));
                l.Add("");

                l.AddRange(GetBinaryTranslator(Schema.TypeRefs.Concat(Schema.Types).ToList()));
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
                return Java.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return Java.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Java.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return Java.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return Java.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
