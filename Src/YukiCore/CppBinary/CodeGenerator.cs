//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C++二进制代码生成器
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

namespace Yuki.ObjectSchema.CppBinary
{
    public static class CodeGenerator
    {
        public static String CompileToCppBinary(this Schema Schema, String NamespaceName)
        {
            Writer w = new Writer() { Schema = Schema, NamespaceName = NamespaceName };
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCppBinary(this Schema Schema)
        {
            return CompileToCppBinary(Schema, "");
        }

        public class Writer
        {
            private static Cpp.Common.CodeGenerator.TemplateInfo TemplateInfo;

            private Cpp.Common.CodeGenerator.Writer InnerWriter;

            public Schema Schema;
            public String NamespaceName;

            static Writer()
            {
                var b = Properties.Resources.CppBinary;
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
                TemplateInfo = new Cpp.Common.CodeGenerator.TemplateInfo(t);
            }

            public String[] GetSchema()
            {
                InnerWriter = new Cpp.Common.CodeGenerator.Writer { Schema = Schema, NamespaceName = NamespaceName };

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gp.Type.TypeRef.Value) && gp.Type.TypeRef.Value == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.Name()));
                    }
                }

                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes(Schema);
                var Contents = ComplexTypes;
                foreach (var nn in NamespaceName.Split('.').Reverse())
                {
                    Contents = GetTemplate("Namespace").Substitute("NamespaceName", nn).Substitute("Contents", Contents);
                }
                return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("Contents", Contents));
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
            public String[] GetTypePredefinition(String Name, String MetaType, String[] GenericParameterLine)
            {
                return InnerWriter.GetTypePredefinition(Name, MetaType, GenericParameterLine);
            }
            public String[] GetTypePredefinition(TypeDef t)
            {
                return InnerWriter.GetTypePredefinition(t);
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

            public String[] GetBinaryTranslator(TypeDef[] Types)
            {
                return GetTemplate("BinaryTranslator").Substitute("Serializers", GetBinaryTranslatorSerializers(Types));
            }

            public String[] GetBinaryTranslatorSerializers(TypeDef[] Types)
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

                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Types);
                var Tuples = ltf.GetTuples();
                var GenericTypeSpecs = ltf.GetGenericTypeSpecs();

                foreach (var t in Tuples)
                {
                    l.AddRange(GetBinaryTranslatorTuple(t));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                TaggedUnion GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                    l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", "OptionalTag").Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "Int"));
                    l.Add("");
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Value == "List")
                    {
                        l.AddRange(GetBinaryTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef == "Optional")
                    {
                        l.AddRange(GetBinaryTranslatorOptional(gps, GenericOptionalType));
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
            public String[] GetBinaryTranslatorAlias(Alias a)
            {
                return GetTemplate("BinaryTranslator_Alias").Substitute("Name", a.Name).Substitute("ValueTypeFriendlyName", a.Type.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorRecord(Record a)
            {
                return GetBinaryTranslatorRecord(a.Name, a.Fields);
            }
            public String[] GetBinaryTranslatorRecord(String Name, Variable[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetBinaryTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetBinaryTranslatorFieldTos(Fields)));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorFieldFroms(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorFieldTos(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorTaggedUnion(TaggedUnion tu)
            {
                return GetBinaryTranslatorTaggedUnion(tu.Name, tu.Alternatives);
            }
            public String[] GetBinaryTranslatorTaggedUnion(String Name, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", Name + "Tag").Substitute("UnderlyingTypeFriendlyName", "Int").Substitute("UnderlyingType", "Int"));
                l.AddRange(GetTemplate("BinaryTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives)));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorAlternativeFroms(String TaggedUnionName, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorAlternativeTos(String TaggedUnionName, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("BinaryTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorEnum(Enum e)
            {
                return GetTemplate("BinaryTranslator_Enum").Substitute("Name", e.Name).Substitute("UnderlyingTypeFriendlyName", e.UnderlyingType.TypeFriendlyName()).Substitute("UnderlyingType", GetTypeString(e.UnderlyingType));
            }
            public String[] GetBinaryTranslatorClientCommand(ClientCommand c)
            {
                List<String> l = new List<String>();
                l.AddRange(GetBinaryTranslatorRecord(c.Name + "Request", c.OutParameters));
                l.AddRange(GetBinaryTranslatorTaggedUnion(c.Name + "Reply", c.InParameters));
                return l.ToArray();
            }
            public String[] GetBinaryTranslatorServerCommand(ServerCommand c)
            {
                List<String> l = new List<String>();
                return GetBinaryTranslatorRecord(c.Name + "Event", c.OutParameters);
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
            public String[] GetBinaryTranslatorList(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l, true)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorOptional(TypeSpec o, TaggedUnion GenericOptionalType)
            {
                var ElementType = o.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                var Alternatives = GenericOptionalType.Alternatives.Select(a => new Variable { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();

                var TypeFriendlyName = o.TypeFriendlyName();
                var TypeString = GetTypeString(o, true);
                var Name = "Optional";
                return GetTemplate("BinaryTranslator_Optional").Substitute("TypeFriendlyName", TypeFriendlyName).Substitute("TypeString", TypeString).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives));
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                if (Schema.TypeRefs.Length == 0)
                {
                    l.AddRange(InnerWriter.GetTemplate("PredefinedTypes"));
                }

                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Schema.Types);
                var Tuples = ltf.GetTuples();

                foreach (var c in Schema.Types)
                {
                    if (c.OnPrimitive)
                    {
                        continue;
                    }

                    l.AddRange(GetTypePredefinition(c));
                }
                foreach (var t in Tuples)
                {
                    l.AddRange(GetTypePredefinition(t.TypeFriendlyName(), "class", new String[] { }));
                }
                l.Add("");

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

                foreach (var t in Tuples)
                {
                    l.AddRange(GetTuple(t.TypeFriendlyName(), t.Tuple));
                    l.Add("");
                }

                l.AddRange(GetTemplate("Streams"));
                l.Add("");

                l.AddRange(GetBinaryTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray()));
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
            return Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String LowercaseCamelize(String PascalName)
        {
            return Cpp.Common.CodeGenerator.LowercaseCamelize(PascalName);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
