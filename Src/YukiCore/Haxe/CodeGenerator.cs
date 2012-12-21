//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Haxe代码生成器
//  Version:     2012.12.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.Haxe
{
    public static class CodeGenerator
    {
        public static String CompileToHaxe(this Schema Schema, String PackageName)
        {
            var w = new Common.CodeGenerator.Writer(Schema, PackageName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToHaxe(this Schema Schema)
        {
            return CompileToHaxe(Schema, "");
        }

    }
}

namespace Yuki.ObjectSchema.Haxe.Common
{
    public static class CodeGenerator
    {
        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private Schema Schema;
            private String PackageName;

            static Writer()
            {
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.Haxe);
            }

            public Writer(Schema Schema, String PackageName)
            {
                this.Schema = Schema;
                this.PackageName = PackageName;
            }

            public String[] GetSchema()
            {
                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }

                var Types = GetTypes(Schema);

                if (PackageName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Types", Types)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", new String[] { }).Substitute("Imports", Schema.Imports).Substitute("Types", Types)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
            }

            public String[] GetPrimitive(String Name, String PlatformName)
            {
                return GetTemplate("Primitive").Substitute("Name", Name).Substitute("PlatformName", PlatformName);
            }

            public String GetTypeString(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                        {
                            var Name = Type.TypeRef.Name;
                            if (Name.Equals("Optional", StringComparison.OrdinalIgnoreCase) || Name.Equals("List", StringComparison.OrdinalIgnoreCase) || Name.Equals("Set", StringComparison.OrdinalIgnoreCase) || Name.Equals("Map", StringComparison.OrdinalIgnoreCase))
                            {
                                var PlatformName = TemplateInfo.PrimitiveMappings[Type.TypeRef.Name].PlatformName;
                                return PlatformName;
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
            public String[] GetAlternative(VariableDef a)
            {
                if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                {
                    return GetTemplate("AlternativeUnit").Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
                }
                else
                {
                    return GetTemplate("Alternative").Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
                }
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
            public String[] GetTaggedUnion(TaggedUnionDef tu)
            {
                var Name = tu.TypeFriendlyName() + GetGenericParameters(tu.GenericParameters);
                var Alternatives = GetAlternatives(tu.Alternatives);
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("Alternatives", Alternatives).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public String[] GetLiteral(EnumDef e, LiteralDef lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("UnderlyingType", GetTypeString(e.UnderlyingType)).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(EnumDef e)
            {
                List<String> l = new List<String>();
                foreach (var lrl in e.Literals)
                {
                    l.AddRange(GetLiteral(e, lrl));
                }
                return l.ToArray();
            }
            public String[] GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("UnderlyingType", GetTypeString(e.UnderlyingType)).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
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

            public String[] GetIApplicationClient(TypeDef[] Commands)
            {
                return GetTemplate("IApplicationClient").Substitute("Commands", GetIApplicationClientCommands(Commands));
            }
            public String[] GetIApplicationClientCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
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
                return l.ToArray();
            }

            public String[] GetTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                if (Schema.TypeRefs.Length == 0)
                {
                    l.AddRange(GetTemplate("PredefinedTypes"));
                }

                foreach (var c in Schema.Types)
                {
                    if (c.OnPrimitive)
                    {
                        var p = c.Primitive;
                        if (TemplateInfo.PrimitiveMappings.ContainsKey(p.Name))
                        {
                            var Name = p.TypeFriendlyName();
                            var PlatformName = TemplateInfo.PrimitiveMappings[p.Name].PlatformName;
                            if (Name != PlatformName && p.GenericParameters.Count() == 0 && PlatformName != "Error")
                            {
                                l.AddRange(GetPrimitive(Name, PlatformName));
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            throw new NotSupportedException(p.Name);
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

                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Schema.Types);
                var Tuples = ltf.GetTuples();
                foreach (var t in Tuples)
                {
                    l.AddRange(GetTuple(t.TypeFriendlyName(), t.Tuple));
                    l.Add("");
                }

                var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToArray();
                if (Commands.Length > 0)
                {
                    l.AddRange(GetIApplicationClient(Commands));
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
            public String GetEscapedIdentifier(String Identifier)
            {
                var l = new List<String>();
                foreach (var IdentifierPart in Identifier.Split('.'))
                {
                    if (TemplateInfo.Keywords.Contains(IdentifierPart))
                    {
                        l.Add("_" + IdentifierPart);
                    }
                    else
                    {
                        l.Add(IdentifierPart);
                    }
                }
                return String.Join(".", l.ToArray());
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
            var UppercaseCapitalizeString = "${" + Parameter.ToUpper() + "}";
            var UppercaseValue = Value.ToUpper();

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

                if (Line.Contains(UppercaseCapitalizeString))
                {
                    NewLine = NewLine.Replace(UppercaseCapitalizeString, UppercaseValue);
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
