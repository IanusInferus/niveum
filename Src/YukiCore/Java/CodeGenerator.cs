//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Java代码生成器
//  Version:     2016.04.17.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.Java
{
    public static class CodeGenerator
    {
        public static String CompileToJava(this Schema Schema, String ClassName, String PackageName)
        {
            var w = new Common.CodeGenerator.Writer(Schema, ClassName, PackageName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToJava(this Schema Schema, String ClassName)
        {
            return CompileToJava(Schema, ClassName, "");
        }
    }
}

namespace Yuki.ObjectSchema.Java.Common
{
    public static class CodeGenerator
    {
        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Schema Schema;
            private String ClassName;
            private String PackageName;

            static Writer()
            {
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Java);
            }

            public Writer(Schema Schema, String ClassName, String PackageName)
            {
                EnumDict = Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).ToDictionary(c => c.VersionedName(), c => c.Enum);
                this.Schema = Schema;
                this.ClassName = ClassName;
                this.PackageName = PackageName;

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("Header", Header).Substitute("ClassName", ClassName).Substitute("PackageName", PackageName == "" ? new String[] { } : new String[] { PackageName }).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
            }

            public String[] GetHeader()
            {
                return GetTemplate("Header");
            }

            public String[] GetPredefinedTypes()
            {
                return GetTemplate("PredefinedTypes");
            }
            public String[] GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", Name).Substitute("PlatformName", PlatformName);
            }
            public String[] GetPrimitives()
            {
                List<String> l = new List<String>();

                var Types = new List<TypeDef>(Schema.TypeRefs.Concat(Schema.Types));
                var Dict = Types.ToDictionary(t => t.VersionedName());
                if (!Dict.ContainsKey("Unit"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Unit", GenericParameters = new VariableDef[] { }, Description = "" }));
                }
                if (!Dict.ContainsKey("Boolean"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Boolean", GenericParameters = new VariableDef[] { }, Description = "" }));
                }
                if (!Dict.ContainsKey("Int"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Int", GenericParameters = new VariableDef[] { }, Description = "" }));
                }
                foreach (var p in Types.Where(c => c.OnPrimitive).Select(c => c.Primitive))
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(p.Name))
                    {
                        var Name = p.TypeFriendlyName();
                        var PlatformName = TemplateInfo.PrimitiveMappings[p.Name].PlatformName;
                        if (Name != PlatformName && p.GenericParameters.Count() == 0)
                        {
                            l.AddRange(GetPrimitive(Name, PlatformName));
                        }
                    }
                    else
                    {
                        throw new NotSupportedException(p.Name);
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
                if (!TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                {
                    return GetEscapedIdentifier(Type.TypeRef.TypeFriendlyName());
                }
                return TemplateInfo.PrimitiveMappings[Type.TypeRef.Name].PlatformName;
            }

            private Dictionary<String, EnumDef> EnumDict = new Dictionary<String, EnumDef>();
            public String GetTypeString(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                        {
                            var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Name].PlatformName;
                            return PlatformName;
                        }
                        else if (EnumDict.ContainsKey(Type.TypeRef.VersionedName()))
                        {
                            return GetTypeString(EnumDict[Type.TypeRef.VersionedName()].UnderlyingType);
                        }
                        return Type.TypeRef.TypeFriendlyName();
                    case TypeSpecTag.GenericParameterRef:
                        return Type.GenericParameterRef.Value;
                    case TypeSpecTag.Tuple:
                        {
                            return Type.TypeFriendlyName();
                        }
                    case TypeSpecTag.GenericTypeSpec:
                        {
                            if (Type.GenericTypeSpec.GenericParameterValues.Count() == 1 && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                            {
                                return "Opt" + Type.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName();
                            }
                            if (Type.GenericTypeSpec.GenericParameterValues.Count() > 0 && Type.GenericTypeSpec.GenericParameterValues.All(gpv => gpv.OnTypeSpec))
                            {
                                return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(p => MapToReferenceType(GetTypeString(p.TypeSpec))).ToArray()) + ">";
                            }
                            else
                            {
                                foreach (var t in Type.GenericTypeSpec.GenericParameterValues.Where(gpv => gpv.OnTypeSpec))
                                {
                                    GetTypeString(t.TypeSpec);
                                }

                                return Type.TypeFriendlyName();
                            }
                        }
                    default:
                        throw new InvalidOperationException();
                }
            }
            private static String MapToReferenceType(String TypeName)
            {
                switch (TypeName)
                {
                    case "boolean":
                        return "java.lang.Boolean";
                    case "byte":
                        return "java.lang.Byte";
                    case "short":
                        return "java.lang.Short";
                    case "int":
                        return "java.lang.Integer";
                    case "long":
                        return "java.lang.Long";
                    case "float":
                        return "java.lang.Float";
                    case "double":
                        return "java.lang.Double";
                    default:
                        return TypeName;
                }
            }
            public String GetGenericParameters(VariableDef[] GenericParameters)
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
            public String[] GetAlias(AliasDef a)
            {
                var Name = a.TypeFriendlyName() + GetGenericParameters(a.GenericParameters);
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
            public String[] GetTuple(String Name, TupleDef t)
            {
                var TupleElements = GetTupleElements(t.Types);
                return GetTemplate("Tuple").Substitute("Name", Name).Substitute("TupleElements", TupleElements);
            }
            public String[] GetField(VariableDef f)
            {
                var d = f.Description;
                if (f.Type.OnTypeRef && EnumDict.ContainsKey(f.Type.TypeRef.VersionedName()))
                {
                    if (d == "")
                    {
                        d = String.Format("类型: {0}", f.Type.TypeRef.TypeFriendlyName());
                    }
                    else
                    {
                        d = String.Format("{0}\r\n类型: {1}", d, f.Type.TypeRef.TypeFriendlyName());
                    }
                }
                return GetTemplate("Field").Substitute("Name", f.Name).Substitute("Type", GetTypeString(f.Type)).Substitute("XmlComment", GetXmlComment(d));
            }
            public String[] GetFields(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var f in Fields)
                {
                    l.AddRange(GetField(f));
                }
                return l.ToArray();
            }
            public String[] GetRecord(RecordDef r)
            {
                var Name = r.TypeFriendlyName() + GetGenericParameters(r.GenericParameters);
                var Fields = GetFields(r.Fields);
                return GetTemplate("Record").Substitute("Name", Name).Substitute("Fields", Fields).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetAlternativeLiterals(VariableDef[] Alternatives, TypeSpec UnderlyingType)
            {
                return GetLiterals(Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToArray(), UnderlyingType);
            }
            public String[] GetAlternative(VariableDef a)
            {
                var d = a.Description;
                if (a.Type.OnTypeRef && EnumDict.ContainsKey(a.Type.TypeRef.VersionedName()))
                {
                    if (d == "")
                    {
                        d = String.Format("类型: {0}", a.Type.TypeRef.VersionedName());
                    }
                    else
                    {
                        d = String.Format("{0}\r\n类型: {1}", d, a.Type.TypeRef.VersionedName());
                    }
                }
                return GetTemplate("Alternative").Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(d));
            }
            public String[] GetAlternatives(VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetAlternative(a));
                }
                return l.ToArray();
            }
            public String[] GetAlternativeCreate(TaggedUnionDef tu, VariableDef a)
            {
                var TaggedUnionName = tu.TypeFriendlyName() + GetGenericParameters(tu.GenericParameters);
                var TaggedUnionTagName = tu.TypeFriendlyName() + "Tag";
                if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                {
                    return GetTemplate("AlternativeCreateUnit").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
                }
                else
                {
                    return GetTemplate("AlternativeCreate").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
                }
            }
            public String[] GetAlternativeCreates(TaggedUnionDef tu)
            {
                List<String> l = new List<String>();
                foreach (var a in tu.Alternatives)
                {
                    l.AddRange(GetAlternativeCreate(tu, a));
                }
                return l.ToArray();
            }
            public String[] GetAlternativePredicate(TaggedUnionDef tu, VariableDef a)
            {
                var TaggedUnionName = tu.TypeFriendlyName() + GetGenericParameters(tu.GenericParameters);
                var TaggedUnionTagName = tu.TypeFriendlyName() + "Tag";
                return GetTemplate("AlternativePredicate").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
            }
            public String[] GetAlternativePredicates(TaggedUnionDef tu)
            {
                List<String> l = new List<String>();
                foreach (var a in tu.Alternatives)
                {
                    l.AddRange(GetAlternativePredicate(tu, a));
                }
                return l.ToArray();
            }
            public String[] GetTaggedUnion(TaggedUnionDef tu)
            {
                var Name = tu.TypeFriendlyName() + GetGenericParameters(tu.GenericParameters);
                var TagName = tu.TypeFriendlyName() + "Tag";
                var AlternativeLiterals = GetAlternativeLiterals(tu.Alternatives, TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }));
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("TagName", TagName).Substitute("AlternativeLiterals", AlternativeLiterals).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public String[] GetLiteral(LiteralDef lrl, TypeSpec UnderlyingType)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("UnderlyingType", GetEnumTypeString(UnderlyingType)).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(LiteralDef[] Literals, TypeSpec UnderlyingType)
            {
                List<String> l = new List<String>();
                foreach (var lrl in Literals)
                {
                    l.AddRange(GetLiteral(lrl, UnderlyingType));
                }
                return l.ToArray();
            }
            public String[] GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e.Literals, e.UnderlyingType);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingType", GetEnumTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public String[] GetClientCommand(ClientCommandDef c)
            {
                var l = new List<String>();
                l.AddRange(GetRecord(new RecordDef { Name = c.TypeFriendlyName() + "Request", Version = "", GenericParameters = new VariableDef[] { }, Fields = c.OutParameters, Description = c.Description }));
                l.AddRange(GetTaggedUnion(new TaggedUnionDef { Name = c.TypeFriendlyName() + "Reply", Version = "", GenericParameters = new VariableDef[] { }, Alternatives = c.InParameters, Description = c.Description }));
                return l.ToArray();
            }
            public String[] GetServerCommand(ServerCommandDef c)
            {
                return GetRecord(new RecordDef { Name = c.TypeFriendlyName() + "Event", Version = "", GenericParameters = new VariableDef[] { }, Fields = c.OutParameters, Description = c.Description });
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

            public String[] GetComplexTypes()
            {
                List<String> l = new List<String>();

                if (Schema.TypeRefs.Length == 0)
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
                    l.AddRange(GetTuple(t.TypeFriendlyName(), t.Tuple));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                if (GenericOptionalTypes.Length > 0)
                {
                    var GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new VariableDef[] { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new VariableDef[] { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                    foreach (var gts in GenericTypeSpecs)
                    {
                        if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.GenericParameterValues.Length == 1)
                        {
                            var ElementType = gts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                            var Name = "Opt" + ElementType.TypeFriendlyName();
                            var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                            l.AddRange(GetTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new VariableDef[] { }, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
                            l.Add("");
                        }
                    }
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
                return Value.UnifyNewLineToLf().Split('\n');
            }
            private static Regex rIdentifierPart = new Regex(@"[^\u0000-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007F]+");
            public static String GetEscapedIdentifier(String Identifier)
            {
                return rIdentifierPart.Replace(Identifier, m =>
                {
                    var IdentifierPart = m.Value;
                    if (TemplateInfo.Keywords.Contains(IdentifierPart))
                    {
                        return "@" + IdentifierPart;
                    }
                    else
                    {
                        return IdentifierPart;
                    }
                });
            }
            private static HashSet<String> TypeKeywords = new HashSet<String>(new String[] { "boolean", "byte", "short", "int", "long", "float", "double" });
            public static String GetEscapedType(String Identifier)
            {
                return rIdentifierPart.Replace(Identifier, m =>
                {
                    var IdentifierPart = m.Value;
                    if (TypeKeywords.Contains(IdentifierPart))
                    {
                        return IdentifierPart;
                    }
                    else if (TemplateInfo.Keywords.Contains(IdentifierPart))
                    {
                        return "@" + IdentifierPart;
                    }
                    else
                    {
                        return IdentifierPart;
                    }
                });
            }
            private static Regex rIdentifier = new Regex(@"(?<!\[\[)\[\[(?<Identifier>.*?)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            private static Regex rType = new Regex(@"(?<!\[\[)\[\[\((?<Type>.*?)\)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            public static String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return Lines.Select(Line => rType.Replace(Line, s => GetEscapedType(s.Result("${Type}")))).Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToArray();
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
