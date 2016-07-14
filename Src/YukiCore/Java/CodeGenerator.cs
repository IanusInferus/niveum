//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Java代码生成器
//  Version:     2016.07.14.
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
                return GetTemplate("PredefinedTypes");
            }
            public List<String> GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", Name).Substitute("PlatformName", PlatformName);
            }
            public List<String> GetPrimitives()
            {
                var l = new List<String>();

                var Types = new List<TypeDef>(Schema.TypeRefs.Concat(Schema.Types));
                var Dict = Types.ToDictionary(t => t.VersionedName());
                if (!Dict.ContainsKey("Unit"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Unit", GenericParameters = new List<VariableDef> { }, Description = "" }));
                }
                if (!Dict.ContainsKey("Boolean"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Boolean", GenericParameters = new List<VariableDef> { }, Description = "" }));
                }
                if (!Dict.ContainsKey("Int"))
                {
                    Types.Add(TypeDef.CreatePrimitive(new PrimitiveDef { Name = "Int", GenericParameters = new List<VariableDef> { }, Description = "" }));
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
                }
                return l;
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
                if (Type.OnTypeRef)
                {
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
                }
                else if (Type.OnGenericParameterRef)
                {
                    return Type.GenericParameterRef;
                }
                else if (Type.OnTuple)
                {
                    return Type.TypeFriendlyName();
                }
                else if (Type.OnGenericTypeSpec)
                {
                    if (Type.GenericTypeSpec.ParameterValues.Count() == 1 && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                    {
                        return "Opt" + Type.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName();
                    }
                    if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                    {
                        return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => MapToReferenceType(GetTypeString(p)))) + ">";
                    }
                    else
                    {
                        return Type.TypeFriendlyName();
                    }
                }
                else
                {
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
            public String GetGenericParameters(List<VariableDef> GenericParameters)
            {
                if (GenericParameters.Count == 0)
                {
                    return "";
                }
                else
                {
                    return "<" + String.Join(", ", GenericParameters.Select(gp => gp.Name)) + ">";
                }
            }
            public List<String> GetAlias(AliasDef a)
            {
                var Name = a.TypeFriendlyName() + GetGenericParameters(a.GenericParameters);
                return GetTemplate("Alias").Substitute("Name", Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
            }
            public List<String> GetTupleElement(Int64 NameIndex, TypeSpec Type)
            {
                return GetTemplate("TupleElement").Substitute("NameIndex", NameIndex.ToInvariantString()).Substitute("Type", GetTypeString(Type));
            }
            public List<String> GetTupleElements(List<TypeSpec> Types)
            {
                var l = new List<String>();
                var n = 0;
                foreach (var e in Types)
                {
                    l.AddRange(GetTupleElement(n, e));
                    n += 1;
                }
                return l;
            }
            public List<String> GetTuple(String Name, List<TypeSpec> Types)
            {
                var TupleElements = GetTupleElements(Types);
                return GetTemplate("Tuple").Substitute("Name", Name).Substitute("TupleElements", TupleElements);
            }
            public List<String> GetField(VariableDef f)
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
            public List<String> GetFields(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var f in Fields)
                {
                    l.AddRange(GetField(f));
                }
                return l;
            }
            public List<String> GetRecord(RecordDef r)
            {
                var Name = r.TypeFriendlyName() + GetGenericParameters(r.GenericParameters);
                var Fields = GetFields(r.Fields);
                return GetTemplate("Record").Substitute("Name", Name).Substitute("Fields", Fields).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public List<String> GetAlternativeLiterals(List<VariableDef> Alternatives, TypeSpec UnderlyingType)
            {
                return GetLiterals(Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToList(), UnderlyingType);
            }
            public List<String> GetAlternative(VariableDef a)
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
            public List<String> GetAlternatives(List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetAlternative(a));
                }
                return l;
            }
            public List<String> GetAlternativeCreate(TaggedUnionDef tu, VariableDef a)
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
            public List<String> GetAlternativeCreates(TaggedUnionDef tu)
            {
                var l = new List<String>();
                foreach (var a in tu.Alternatives)
                {
                    l.AddRange(GetAlternativeCreate(tu, a));
                }
                return l;
            }
            public List<String> GetAlternativePredicate(TaggedUnionDef tu, VariableDef a)
            {
                var TaggedUnionName = tu.TypeFriendlyName() + GetGenericParameters(tu.GenericParameters);
                var TaggedUnionTagName = tu.TypeFriendlyName() + "Tag";
                return GetTemplate("AlternativePredicate").Substitute("TaggedUnionName", TaggedUnionName).Substitute("TaggedUnionTagName", TaggedUnionTagName).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
            }
            public List<String> GetAlternativePredicates(TaggedUnionDef tu)
            {
                var l = new List<String>();
                foreach (var a in tu.Alternatives)
                {
                    l.AddRange(GetAlternativePredicate(tu, a));
                }
                return l;
            }
            public List<String> GetTaggedUnion(TaggedUnionDef tu)
            {
                var Name = tu.TypeFriendlyName() + GetGenericParameters(tu.GenericParameters);
                var TagName = tu.TypeFriendlyName() + "Tag";
                var AlternativeLiterals = GetAlternativeLiterals(tu.Alternatives, TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }));
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("TagName", TagName).Substitute("AlternativeLiterals", AlternativeLiterals).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public List<String> GetLiteral(LiteralDef lrl, TypeSpec UnderlyingType)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("UnderlyingType", GetEnumTypeString(UnderlyingType)).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public List<String> GetLiterals(List<LiteralDef> Literals, TypeSpec UnderlyingType)
            {
                var l = new List<String>();
                foreach (var lrl in Literals)
                {
                    l.AddRange(GetLiteral(lrl, UnderlyingType));
                }
                return l;
            }
            public List<String> GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e.Literals, e.UnderlyingType);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingType", GetEnumTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
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
                if (Description == "") { return new List<String> { }; }

                var d = Description.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

                var Lines = d.UnifyNewLineToLf().Split('\n').ToList();
                if (Lines.Count == 1)
                {
                    return GetTemplate("SingleLineXmlComment").Substitute("Description", d);
                }
                else
                {
                    return GetTemplate("MultiLineXmlComment").Substitute("Description", Lines);
                }
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
                    var GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Description = "" } }, Description = "" };
                    foreach (var gts in GenericTypeSpecs)
                    {
                        if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                        {
                            var ElementType = gts.GenericTypeSpec.ParameterValues.Single();
                            var Name = "Opt" + ElementType.TypeFriendlyName();
                            var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();
                            l.AddRange(GetTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
                            l.Add("");
                        }
                    }
                }

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
                return Value.UnifyNewLineToLf().Split('\n').ToList();
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
            private static HashSet<String> TypeKeywords = new HashSet<String>(new List<String> { "boolean", "byte", "short", "int", "long", "float", "double" });
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
            public static List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Lines.Select(Line => rType.Replace(Line, s => GetEscapedType(s.Result("${Type}")))).Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToList();
            }
        }

        public static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + LowercaseCamelize(Parameter) + "}";
            var LowercaseValue = LowercaseCamelize(Value);

            var l = new List<String>();
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
            return l;
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
        public static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            var l = new List<String>();
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
            return l;
        }
    }
}
