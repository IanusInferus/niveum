//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C++代码生成器
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
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;

namespace Yuki.ObjectSchema.Cpp
{
    public static class CodeGenerator
    {
        public static String CompileToCpp(this Schema Schema, String NamespaceName)
        {
            var w = new Common.CodeGenerator.Writer() { Schema = Schema, NamespaceName = NamespaceName };
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCpp(this Schema Schema)
        {
            return CompileToCpp(Schema, "");
        }

    }
}

namespace Yuki.ObjectSchema.Cpp.Common
{
    public static class CodeGenerator
    {
        public class TemplateInfo
        {
            public HashSet<String> Keywords;
            public Dictionary<String, PrimitiveMapping> PrimitiveMappings;
            public Dictionary<String, Template> Templates;

            public TemplateInfo(ObjectSchemaTemplate Template)
            {
                Keywords = new HashSet<String>(Template.Keywords, StringComparer.Ordinal);
                PrimitiveMappings = Template.PrimitiveMappings.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
                Templates = Template.Templates.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        public class Writer
        {
            private static TemplateInfo TemplateInfo;

            public Schema Schema;
            public String NamespaceName;

            static Writer()
            {
                var b = Properties.Resources.Cpp;
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
                TemplateInfo = new TemplateInfo(t);
            }

            public String[] GetSchema()
            {
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

            public String[] GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", GetEscapedIdentifier(Name)).Substitute("PlatformName", PlatformName);
            }
            public String[] GetPrimitives()
            {
                List<String> l = new List<String>();

                if (Schema.TypeRefs.Length == 0)
                {
                    l.AddRange(GetTemplate("PredefinedTypes"));
                }

                var Types = new List<TypeDef>(Schema.TypeRefs.Concat(Schema.Types));
                var Dict = Types.ToDictionary(t => t.Name());
                if (!Dict.ContainsKey("Boolean"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new Primitive { Name = "Boolean", GenericParameters = new Variable[] { }, Description = "" }));
                }
                foreach (var p in Types.Where(c => c.OnPrimitive).Select(c => c.Primitive))
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(p.Name))
                    {
                        var Name = p.Name;
                        var PlatformName = TemplateInfo.PrimitiveMappings[p.Name].PlatformName;
                        if (Name != PlatformName && p.GenericParameters.Count() == 0)
                        {
                            l.AddRange(GetPrimitive(Name, PlatformName));
                        }
                    }
                }
                return l.ToArray();
            }

            public String GetEnumTypeString(TypeSpec Type)
            {
                if (!Type.OnTypeRef)
                {
                    throw new InvalidOperationException();
                }
                return GetEscapedIdentifier(Type.TypeRef.Value);
            }

            public String GetTypeString(TypeSpec Type, Boolean ForceAsValue = false)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Value))
                        {
                            var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Value].PlatformName;
                            if (Type.TypeRef.Value == "List" || Type.TypeRef.Value == "Set" || Type.TypeRef.Value == "Map")
                            {
                                return PlatformName;
                            }
                            else
                            {
                                return Type.TypeRef.Value;
                            }
                        }
                        if (ForceAsValue)
                        {
                            return Type.TypeRef.Value;
                        }
                        return "std::shared_ptr<" + Type.TypeRef.Value + ">";
                    case TypeSpecTag.GenericParameterRef:
                        return Type.GenericParameterRef.Value;
                    case TypeSpecTag.Tuple:
                        {
                            if (ForceAsValue)
                            {
                                return Type.TypeFriendlyName();
                            }
                            return "std::shared_ptr<" + Type.TypeFriendlyName() + ">";
                        }
                    case TypeSpecTag.GenericTypeSpec:
                        {
                            if (Type.GenericTypeSpec.GenericParameterValues.Count() > 0 && Type.GenericTypeSpec.GenericParameterValues.All(gpv => gpv.OnTypeSpec))
                            {
                                var TypeString = GetTypeString(Type.GenericTypeSpec.TypeSpec, true) + "<" + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(p => GetTypeString(p.TypeSpec)).ToArray()) + ">";
                                if (ForceAsValue)
                                {
                                    return TypeString;
                                }
                                return "std::shared_ptr<" + TypeString + ">";
                            }
                            else
                            {
                                foreach (var t in Type.GenericTypeSpec.GenericParameterValues.Where(gpv => gpv.OnTypeSpec))
                                {
                                    GetTypeString(t.TypeSpec);
                                }

                                if (ForceAsValue)
                                {
                                    return Type.TypeFriendlyName();
                                }
                                return "std::shared_ptr<" + Type.TypeFriendlyName() + ">";
                            }
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
            public String[] GetGenericParameterLine(Variable[] GenericParameters)
            {
                if (GenericParameters.Length == 0) { return new String[] { }; }
                return new String[] { "template<" + String.Join(", ", GenericParameters.Select(gp => "typename " + gp.Name).ToArray()) + ">" };
            }
            public String GetGenericParameters(Variable[] GenericParameters)
            {
                if (GenericParameters.Length == 0)
                {
                    return "";
                }
                else
                {
                    return "<" + String.Join(", ", GenericParameters.Select(gp => gp.Name).ToArray()) + ">";
                }
            }
            public String[] GetTypePredefinition(String Name, String MetaType, String[] GenericParameterLine)
            {
                return GetTemplate("TypePredefinition").Substitute("Name", Name).Substitute("MetaType", MetaType).Substitute("GenericParameterLine", GenericParameterLine);
            }
            public String[] GetTypePredefinition(TypeDef t)
            {
                var Name = t.Name();
                var GenericParameterLine = GetGenericParameterLine(t.GenericParameters());
                String MetaType = "class";
                if (t.OnPrimitive)
                {
                    return new String[] { };
                }
                else if (t.OnAlias || t.OnRecord || t.OnTaggedUnion || t.OnClientCommand || t.OnServerCommand)
                {
                    MetaType = "class";
                }
                else if (t.OnEnum)
                {
                    MetaType = "enum";
                }
                return GetTypePredefinition(Name, MetaType, GenericParameterLine);
            }
            public String[] GetAlias(Alias a)
            {
                var Name = a.Name + GetGenericParameters(a.GenericParameters);
                return GetTemplate("Alias").Substitute("Name", Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
            }
            public String[] GetTupleElement(Int64 NameIndex, TypeSpec Type)
            {
                return GetTemplate("TupleElement").Substitute("NameIndex", NameIndex.ToInvariantString()).Substitute("Type", GetTypeString(Type));
            }
            public String[] GetTupleElements(TypeSpec[] Types)
            {
                List<String> l = new List<String>();
                var n = 0;
                foreach (var e in Types)
                {
                    l.AddRange(GetTupleElement(n, e));
                    n += 1;
                }
                return l.ToArray();
            }
            public String[] GetTuple(String Name, Tuple t)
            {
                var TupleElements = GetTupleElements(t.Types);
                return GetTemplate("Tuple").Substitute("Name", Name).Substitute("TupleElements", TupleElements);
            }
            public String[] GetField(Variable f)
            {
                return GetTemplate("Field").Substitute("Name", f.Name).Substitute("Type", GetTypeString(f.Type)).Substitute("XmlComment", GetXmlComment(f.Description));
            }
            public String[] GetFields(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var f in Fields)
                {
                    l.AddRange(GetField(f));
                }
                return l.ToArray();
            }
            public String[] GetRecord(Record r)
            {
                var Name = r.Name + GetGenericParameters(r.GenericParameters);
                var Fields = GetFields(r.Fields);
                return GetTemplate("Record").Substitute("Name", Name).Substitute("Fields", Fields).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetAlternativeLiterals(String TagName, Variable[] Alternatives)
            {
                return GetLiterals(TagName, Alternatives.Select((a, i) => new Literal { Name = a.Name, Value = i, Description = a.Description }).ToArray());
            }
            public String[] GetAlternative(Variable a)
            {
                return GetTemplate("Alternative").Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
            }
            public String[] GetAlternatives(Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetAlternative(a));
                }
                return l.ToArray();
            }
            public String[] GetAlternativeCreate(TaggedUnion tu, Variable a)
            {
                var TaggedUnionName = tu.Name + GetGenericParameters(tu.GenericParameters);
                var TaggedUnionTagName = tu.Name + "Tag";
                if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Value.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                {
                    return GetTemplate("AlternativeCreateUnit").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
                }
                else
                {
                    return GetTemplate("AlternativeCreate").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
                }
            }
            public String[] GetAlternativeCreates(TaggedUnion tu)
            {
                List<String> l = new List<String>();
                foreach (var a in tu.Alternatives)
                {
                    l.AddRange(GetAlternativeCreate(tu, a));
                }
                return l.ToArray();
            }
            public String[] GetAlternativePredicate(TaggedUnion tu, Variable a)
            {
                var TaggedUnionName = tu.Name + GetGenericParameters(tu.GenericParameters);
                var TaggedUnionTagName = tu.Name + "Tag";
                return GetTemplate("AlternativePredicate").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
            }
            public String[] GetAlternativePredicates(TaggedUnion tu)
            {
                List<String> l = new List<String>();
                foreach (var a in tu.Alternatives)
                {
                    l.AddRange(GetAlternativePredicate(tu, a));
                }
                return l.ToArray();
            }
            public String[] GetTaggedUnion(TaggedUnion tu)
            {
                var Name = tu.Name;
                var GenericParameterLine = GetGenericParameterLine(tu.GenericParameters);
                var TagName = tu.Name + "Tag";
                var AlternativeLiterals = GetAlternativeLiterals(TagName, tu.Alternatives);
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("GenericParameterLine", GenericParameterLine).Substitute("TagName", TagName).Substitute("AlternativeLiterals", AlternativeLiterals).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public String[] GetLiteral(String EnumName, Literal lrl)
            {
                return GetTemplate("Literal").Substitute("Name", EnumName + "_" + lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLastLiteral(String EnumName, Literal lrl)
            {
                return GetTemplate("LastLiteral").Substitute("Name", EnumName + "_" + lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(String EnumName, Literal[] Literals)
            {
                List<String> l = new List<String>();
                foreach (var lrl in Literals.Reverse().Skip(1).Reverse())
                {
                    l.AddRange(GetLiteral(EnumName, lrl));
                }
                foreach (var lrl in Literals.Reverse().Take(1))
                {
                    l.AddRange(GetLastLiteral(EnumName, lrl));
                }
                return l.ToArray();
            }
            public String[] GetEnum(Enum e)
            {
                var Literals = GetLiterals(e.Name, e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.Name).Substitute("UnderlyingType", GetEnumTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public String[] GetClientCommand(ClientCommand c)
            {
                var l = new List<String>();
                l.AddRange(GetRecord(new Record { Name = c.Name + "Request", GenericParameters = { }, Fields = c.OutParameters, Description = c.Description }));
                l.AddRange(GetTaggedUnion(new TaggedUnion { Name = c.Name + "Reply", GenericParameters = { }, Alternatives = c.InParameters, Description = c.Description }));
                return l.ToArray();
            }
            public String[] GetServerCommand(ServerCommand c)
            {
                return GetRecord(new Record { Name = c.Name + "Event", GenericParameters = { }, Fields = c.OutParameters, Description = c.Description });
            }
            public String[] GetXmlComment(String Description)
            {
                if (Description == "") { return new String[] { }; }

                var d = Description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

                var Lines = d.UnifyNewLineToLf().Split('\n');
                if (Lines.Length == 1)
                {
                    return GetTemplate("SingleLineXmlComment").Substitute("Description", d);
                }
                else
                {
                    return GetTemplate("MultiLineXmlComment").Substitute("Description", Lines);
                }
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

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
            private static Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007F]+");
            public String GetEscapedIdentifier(String Identifier)
            {
                return rIdentifierPart.Replace(Identifier, m =>
                {
                    var IdentifierPart = m.Value;
                    if (TemplateInfo.Keywords.Contains(IdentifierPart))
                    {
                        return "_" + IdentifierPart;
                    }
                    else
                    {
                        return IdentifierPart;
                    }
                });
            }
            private Regex rIdentifier = new Regex(@"(?<!\[\[)\[\[(?<Identifier>.*?)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            public String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return Lines.Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToArray();
            }
        }

        public static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + LowercaseCamelize(Parameter) + "}";
            var LowercaseValue = LowercaseCamelize(Value);

            List<String> l = new List<String>();
            foreach (var Line in Lines)
            {
                var NewLine = Line;

                if (Line.Contains(ParameterString))
                {
                    NewLine = NewLine.Replace(ParameterString, Value);
                }

                if (Line.Contains(LowercaseParameterString))
                {
                    NewLine = NewLine.Replace(LowercaseParameterString, LowercaseValue);
                }

                l.Add(NewLine);
            }
            return l.ToArray();
        }
        public static String LowercaseCamelize(String PascalName)
        {
            var l = new List<Char>();
            foreach (var c in PascalName)
            {
                if (Char.IsLower(c))
                {
                    break;
                }

                l.Add(Char.ToLower(c));
            }

            return new String(l.ToArray()) + new String(PascalName.Skip(l.Count).ToArray());
        }
        public static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            List<String> l = new List<String>();
            foreach (var Line in Lines)
            {
                var ParameterString = "${" + Parameter + "}";
                if (Line.Contains(ParameterString))
                {
                    foreach (var vLine in Value)
                    {
                        l.Add(Line.Replace(ParameterString, vLine));
                    }
                }
                else
                {
                    l.Add(Line);
                }
            }
            return l.ToArray();
        }
    }
}
