//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构VB.Net代码生成器
//  Version:     2016.08.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.VB
{
    public static class CodeGenerator
    {
        public static String CompileToVB(this Schema Schema, String NamespaceName, Boolean WithFirefly)
        {
            var w = new Common.CodeGenerator.Writer(Schema, NamespaceName, WithFirefly);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToVB(this Schema Schema)
        {
            return CompileToVB(Schema, "", true);
        }
    }
}

namespace Yuki.ObjectSchema.VB.Common
{
    public static class CodeGenerator
    {
        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Schema Schema;
            private String NamespaceName;
            private Boolean WithFirefly;

            static Writer()
            {
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.VB);
            }

            public Writer(Schema Schema, String NamespaceName, Boolean WithFirefly)
            {
                this.Schema = Schema;
                this.NamespaceName = NamespaceName;
                this.WithFirefly = WithFirefly;
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

            public List<String> GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", Name).Substitute("PlatformName", PlatformName);
            }
            public List<String> GetPrimitives()
            {
                var l = new List<String>();

                foreach (var p in Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnPrimitive).Select(c => c.Primitive))
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
                return GetTypeString(Type);
            }

            public String GetTypeString(TypeSpec Type)
            {
                if (Type.OnTypeRef)
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                    {
                        var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Name].PlatformName;
                        if (PlatformName.StartsWith("System.Collections.Generic."))
                        {
                            return new String(PlatformName.Skip("System.Collections.Generic.".Length).ToArray());
                        }
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
                    if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                    {
                        return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + ">";
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
                return GetTemplate("Field").Substitute("Name", f.Name).Substitute("Type", GetTypeString(f.Type)).Substitute("XmlComment", GetXmlComment(f.Description));
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
            public List<String> GetAlternativeLiterals(List<VariableDef> Alternatives)
            {
                return GetLiterals(Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Attributes = a.Attributes, Description = a.Description }).ToList());
            }
            public List<String> GetAlternative(VariableDef a)
            {
                return GetTemplate("Alternative").Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
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
                var AlternativeLiterals = GetAlternativeLiterals(tu.Alternatives);
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("TagName", TagName).Substitute("AlternativeLiterals", AlternativeLiterals).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public List<String> GetLiteral(LiteralDef lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public List<String> GetLastLiteral(LiteralDef lrl)
            {
                return GetTemplate("LastLiteral").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public List<String> GetLiterals(List<LiteralDef> Literals)
            {
                var l = new List<String>();
                foreach (var lrl in Literals.Take(Literals.Count - 1))
                {
                    l.AddRange(GetLiteral(lrl));
                }
                foreach (var lrl in Literals.Skip(Literals.Count - 1))
                {
                    l.AddRange(GetLastLiteral(lrl));
                }
                return l;
            }
            public List<String> GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingType", GetEnumTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public List<String> GetEnumParser(EnumDef e)
            {
                var LiteralDict = e.Literals.ToDictionary(l => l.Name);
                var LiteralNameAdds = e.Literals.Select(l => new { Name = l.Name, NameOrDescription = l.Name });
                var LiteralDescriptionAdds = e.Literals.GroupBy(l => l.Description).Where(l => l.Count() == 1).Select(l => l.Single()).Where(l => !LiteralDict.ContainsKey(l.Description)).Select(l => new { Name = l.Name, NameOrDescription = l.Description });
                var LiteralAdds = LiteralNameAdds.Concat(LiteralDescriptionAdds).Select(l => GetTemplate("LiteralAdd").Substitute("EnumName", e.TypeFriendlyName()).Substitute("LiteralName", l.Name).Substitute("NameOrDescription", l.NameOrDescription.Escape()).Single()).ToList();
                return GetTemplate("EnumParser").Substitute("Name", e.TypeFriendlyName()).Substitute("LiteralAdds", LiteralAdds).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public List<String> GetEnumWriter(EnumDef e)
            {
                var LiteralAddWriters = e.Literals.Select(l => GetTemplate("LiteralAddWriter").Substitute("EnumName", e.TypeFriendlyName()).Substitute("LiteralName", l.Name).Substitute("Description", l.Description.Escape()).Single()).ToList();
                return GetTemplate("EnumWriter").Substitute("Name", e.TypeFriendlyName()).Substitute("LiteralAddWriters", LiteralAddWriters).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public List<String> GetClientCommand(ClientCommandDef c)
            {
                var l = new List<String>();
                l.AddRange(GetRecord(new RecordDef { Name = c.TypeFriendlyName() + "Request", Version = "", GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Attributes = c.Attributes, Description = c.Description }));
                l.AddRange(GetTaggedUnion(new TaggedUnionDef { Name = c.TypeFriendlyName() + "Reply", Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = c.InParameters, Attributes = c.Attributes, Description = c.Description }));
                return l;
            }
            public List<String> GetServerCommand(ServerCommandDef c)
            {
                return GetRecord(new RecordDef { Name = c.TypeFriendlyName() + "Event", Version = "", GenericParameters = new List<VariableDef> { }, Fields = c.OutParameters, Attributes = c.Attributes, Description = c.Description });
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

            public List<String> GetIApplicationServer(List<TypeDef> Commands)
            {
                return GetTemplate("IApplicationServer").Substitute("Commands", GetIApplicationServerCommands(Commands));
            }
            public List<String> GetIApplicationServerCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        l.AddRange(GetTemplate("IApplicationServer_ClientCommand").Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ClientCommand.Description)));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("IApplicationServer_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ServerCommand.Description)));
                    }
                }
                return l;
            }
            public List<String> GetIApplicationClient(List<TypeDef> Commands)
            {
                return GetTemplate("IApplicationClient").Substitute("Commands", GetIApplicationClientCommands(Commands));
            }
            public List<String> GetIApplicationClientCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        l.AddRange(GetTemplate("IApplicationClient_ClientCommand").Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ClientCommand.Description)));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("IApplicationClient_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ServerCommand.Description)));
                    }
                }
                return l;
            }

            public List<String> GetComplexTypes()
            {
                var l = new List<String>();

                List<TypeDef> cl = new List<TypeDef>();

                if (Schema.TypeRefs.Count == 0)
                {
                    if (WithFirefly)
                    {
                        l.AddRange(GetTemplate("PredefinedTypes_WithFirefly"));
                    }
                    else
                    {
                        l.AddRange(GetTemplate("PredefinedTypes"));
                    }
                }

                var scg = Schema.GetSchemaClosureGenerator();
                var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
                var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();

                foreach (var c in Schema.Types)
                {
                    if (!c.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        continue;
                    }

                    if (c.OnPrimitive)
                    {
                        if (c.Name() == "Optional")
                        {
                            l.AddRange(GetTemplate("PredefinedType_Optional"));
                        }
                        else
                        {
                            continue;
                        }
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
                        l.AddRange(GetEnumParser(c.Enum));
                        l.AddRange(GetEnumWriter(c.Enum));
                    }
                    else if (c.OnClientCommand)
                    {
                        l.AddRange(GetClientCommand(c.ClientCommand));
                        cl.Add(c);
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetServerCommand(c.ServerCommand));
                        cl.Add(c);
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

                if (cl.Count > 0)
                {
                    var ca = cl.ToList();

                    l.AddRange(GetIApplicationServer(ca));
                    l.Add("");
                    l.AddRange(GetIApplicationClient(ca));
                    l.Add("");
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
            private static Regex rIdentifierPart = new Regex(@"[^\u0000-\u002D\u002F\u003A-\u0040\u005B-\u005E\u0060\u007B-\u007F]+");
            public static String GetEscapedIdentifier(String Identifier)
            {
                return rIdentifierPart.Replace(Identifier, m =>
                {
                    var IdentifierPart = m.Value;
                    if (TemplateInfo.Keywords.Contains(IdentifierPart))
                    {
                        return "[" + IdentifierPart + "]";
                    }
                    else
                    {
                        return IdentifierPart;
                    }
                }).Replace("<", "(Of ").Replace(">", ")");
            }
            private static Regex rIdentifier = new Regex(@"(?<!\[\[)\[\[(?<Identifier>.*?)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            public static List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Lines.Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToList();
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
