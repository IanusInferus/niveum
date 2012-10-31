//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构VB.Net代码生成器
//  Version:     2012.10.30.
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
        public static String CompileToVB(this Schema Schema, String NamespaceName)
        {
            var w = new Common.CodeGenerator.Writer() { Schema = Schema, NamespaceName = NamespaceName };
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToVB(this Schema Schema)
        {
            return CompileToVB(Schema, "");
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

            public Schema Schema;
            public String NamespaceName;

            static Writer()
            {
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.VB);
            }

            public String[] GetSchema()
            {
                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gp.Type.TypeRef.Name) && TemplateInfo.PrimitiveMappings[gp.Type.TypeRef.Name].PlatformName == "System.Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
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

            public String[] GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", Name).Substitute("PlatformName", PlatformName);
            }
            public String[] GetPrimitives()
            {
                List<String> l = new List<String>();

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
                    else
                    {
                        throw new NotSupportedException(p.Name);
                    }
                }
                return l.ToArray();
            }

            public String GetEnumTypeString(TypeSpec Type)
            {
                return GetTypeString(Type);
            }

            public String GetTypeString(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                        {
                            var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Name].PlatformName;
                            if (PlatformName.StartsWith("System.Collections.Generic."))
                            {
                                return new String(PlatformName.Skip("System.Collections.Generic.".Length).ToArray());
                            }
                            if (PlatformName.StartsWith("System."))
                            {
                                return new String(PlatformName.Skip("System.".Length).ToArray());
                            }
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
                            if (Type.GenericTypeSpec.GenericParameterValues.Count() > 0 && Type.GenericTypeSpec.GenericParameterValues.All(gpv => gpv.OnTypeSpec))
                            {
                                return GetTypeString(Type.GenericTypeSpec.TypeSpec) + "<" + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(p => GetTypeString(p.TypeSpec)).ToArray()) + ">";
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
                return GetTemplate("Field").Substitute("Name", f.Name).Substitute("Type", GetTypeString(f.Type)).Substitute("XmlComment", GetXmlComment(f.Description));
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
            public String[] GetAlternativeLiterals(VariableDef[] Alternatives)
            {
                return GetLiterals(Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToArray());
            }
            public String[] GetAlternative(VariableDef a)
            {
                return GetTemplate("Alternative").Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
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
                var AlternativeLiterals = GetAlternativeLiterals(tu.Alternatives);
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("TagName", TagName).Substitute("AlternativeLiterals", AlternativeLiterals).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public String[] GetLiteral(LiteralDef lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLastLiteral(LiteralDef lrl)
            {
                return GetTemplate("LastLiteral").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(LiteralDef[] Literals)
            {
                List<String> l = new List<String>();
                foreach (var lrl in Literals.Reverse().Skip(1).Reverse())
                {
                    l.AddRange(GetLiteral(lrl));
                }
                foreach (var lrl in Literals.Reverse().Take(1))
                {
                    l.AddRange(GetLastLiteral(lrl));
                }
                return l.ToArray();
            }
            public String[] GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingType", GetEnumTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public String[] GetEnumParser(EnumDef e)
            {
                var LiteralDict = e.Literals.ToDictionary(l => l.Name);
                var LiteralNameAdds = e.Literals.Select(l => new { Name = l.Name, NameOrDescription = l.Name });
                var LiteralDescriptionAdds = e.Literals.GroupBy(l => l.Description).Where(l => l.Count() == 1).Select(l => l.Single()).Where(l => !LiteralDict.ContainsKey(l.Description)).Select(l => new { Name = l.Name, NameOrDescription = l.Description });
                var LiteralAdds = LiteralNameAdds.Concat(LiteralDescriptionAdds).Select(l => GetTemplate("LiteralAdd").Substitute("EnumName", e.TypeFriendlyName()).Substitute("LiteralName", l.Name).Substitute("NameOrDescription", l.NameOrDescription.Escape()).Single()).ToArray();
                return GetTemplate("EnumParser").Substitute("Name", e.TypeFriendlyName()).Substitute("LiteralAdds", LiteralAdds).Substitute("XmlComment", GetXmlComment(e.Description));
            }
            public String[] GetEnumWriter(EnumDef e)
            {
                var LiteralAddWriters = e.Literals.Select(l => GetTemplate("LiteralAddWriter").Substitute("EnumName", e.TypeFriendlyName()).Substitute("LiteralName", l.Name).Substitute("Description", l.Description.Escape()).Single()).ToArray();
                return GetTemplate("EnumWriter").Substitute("Name", e.TypeFriendlyName()).Substitute("LiteralAddWriters", LiteralAddWriters).Substitute("XmlComment", GetXmlComment(e.Description));
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

            public String[] GetIServerImplementation(TypeDef[] Commands)
            {
                return GetTemplate("IServerImplementation").Substitute("Commands", GetIServerImplementationCommands(Commands));
            }
            public String[] GetIServerImplementationCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        l.AddRange(GetTemplate("IServerImplementation_ClientCommand").Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ClientCommand.Description)));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("IServerImplementation_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ServerCommand.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetIClientImplementation(TypeDef[] Commands)
            {
                return GetTemplate("IClientImplementation").Substitute("Commands", GetIClientImplementationCommands(Commands));
            }
            public String[] GetIClientImplementationCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("IClientImplementation_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.ServerCommand.Description)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                List<TypeDef> cl = new List<TypeDef>();

                if (Schema.TypeRefs.Length == 0)
                {
                    l.AddRange(GetTemplate("PredefinedTypes"));
                }

                foreach (var c in Schema.Types)
                {
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

                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Schema.Types);
                var Tuples = ltf.GetTuples();
                foreach (var t in Tuples)
                {
                    l.AddRange(GetTuple(t.TypeFriendlyName(), t.Tuple));
                    l.Add("");
                }

                if (cl.Count > 0)
                {
                    var ca = cl.ToArray();

                    l.AddRange(GetIServerImplementation(ca));
                    l.Add("");
                    l.AddRange(GetIClientImplementation(ca));
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
            private static Regex rIdentifierPart = new Regex(@"[^\u0000-\u002D\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007F]+");
            public String GetEscapedIdentifier(String Identifier)
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
