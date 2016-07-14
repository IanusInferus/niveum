﻿//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0代码生成器
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

namespace Yuki.ObjectSchema.ActionScript
{
    public class FileResult
    {
        public String Path;
        public String Content;
    }

    public static class CodeGenerator
    {
        public static List<FileResult> CompileToActionScript(this Schema Schema, String PackageName)
        {
            var w = new Common.CodeGenerator.Writer(Schema, PackageName);
            var Files = w.GetFiles();
            return Files;
        }
    }
}

namespace Yuki.ObjectSchema.ActionScript.Common
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
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScript);
            }

            public Writer(Schema Schema, String PackageName)
            {
                this.Schema = Schema;
                this.PackageName = PackageName;

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }

                EnumSet = new HashSet<String>(Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).Select(c => c.VersionedName()).Distinct());
            }

            public List<ActionScript.FileResult> GetFiles()
            {
                var l = new List<ActionScript.FileResult>();

                var scg = Schema.GetSchemaClosureGenerator();
                var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
                var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
                var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

                foreach (var c in Schema.Types)
                {
                    if (c.GenericParameters().Count() != 0)
                    {
                        continue;
                    }
                    if (c.OnPrimitive)
                    {
                        if (!TemplateInfo.PrimitiveMappings.ContainsKey(c.Name()))
                        {
                            throw new NotSupportedException(c.Name());
                        }
                        if (c.Primitive.Name == "Unit")
                        {
                            l.Add(GetFile("Unit", GetRecord(new RecordDef { Name = "Unit", Version = "", Fields = new List<VariableDef> { }, Description = "" })));
                        }
                    }
                    else if (c.OnAlias)
                    {
                        l.Add(GetFile(c.Alias.TypeFriendlyName(), GetAlias(c.Alias)));
                    }
                    else if (c.OnRecord)
                    {
                        l.Add(GetFile(c.Record.TypeFriendlyName(), GetRecord(c.Record)));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        var tut = new EnumDef { Name = c.TaggedUnion.TypeFriendlyName() + "Tag", Version = "", UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }), Literals = c.TaggedUnion.Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToList(), Description = c.TaggedUnion.Description };
                        l.Add(GetFile(tut.TypeFriendlyName(), GetEnum(tut)));
                        l.Add(GetFile(c.TaggedUnion.TypeFriendlyName(), GetTaggedUnion(c.TaggedUnion)));
                    }
                    else if (c.OnEnum)
                    {
                        l.Add(GetFile(c.Enum.TypeFriendlyName(), GetEnum(c.Enum)));
                    }
                    else if (c.OnClientCommand)
                    {
                        var creq = new RecordDef { Name = c.ClientCommand.TypeFriendlyName() + "Request", Version = "", GenericParameters = new List<VariableDef> { }, Fields = c.ClientCommand.OutParameters, Description = c.ClientCommand.Description };
                        var crept = new EnumDef { Name = c.ClientCommand.TypeFriendlyName() + "ReplyTag", Version = "", UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }), Literals = c.ClientCommand.InParameters.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToList(), Description = c.ClientCommand.Description };
                        var crep = new TaggedUnionDef { Name = c.ClientCommand.TypeFriendlyName() + "Reply", Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = c.ClientCommand.InParameters, Description = c.ClientCommand.Description };
                        l.Add(GetFile(creq.TypeFriendlyName(), GetRecord(creq)));
                        l.Add(GetFile(crept.TypeFriendlyName(), GetEnum(crept)));
                        l.Add(GetFile(crep.TypeFriendlyName(), GetTaggedUnion(crep)));
                    }
                    else if (c.OnServerCommand)
                    {
                        var se = new RecordDef { Name = c.ServerCommand.TypeFriendlyName() + "Event", Version = "", GenericParameters = new List<VariableDef> { }, Fields = c.ServerCommand.OutParameters, Description = c.ServerCommand.Description };
                        l.Add(GetFile(se.TypeFriendlyName(), GetRecord(se)));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                foreach (var t in Tuples)
                {
                    l.Add(GetFile(t.TypeFriendlyName(), GetTuple(t.TypeFriendlyName(), t.Tuple)));
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
                if (GenericOptionalTypes.Count > 0)
                {
                    var GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef("T"), Description = "" } }, Description = "" };
                    var GenericKeyValuePairType = new RecordDef { Name = "KeyValuePair", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "TKey", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" }, new VariableDef { Name = "TValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Fields = new List<VariableDef> { new VariableDef { Name = "Key", Type = TypeSpec.CreateGenericParameterRef("TKey"), Description = "" }, new VariableDef { Name = "Value", Type = TypeSpec.CreateGenericParameterRef("TValue"), Description = "" } }, Description = "" };
                    foreach (var gts in GenericTypeSpecs)
                    {
                        if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.ParameterValues.Count == 1)
                        {
                            var ElementType = gts.GenericTypeSpec.ParameterValues.Single();
                            var Name = "Opt" + ElementType.TypeFriendlyName();
                            var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();
                            var tut = new EnumDef { Name = Name + "Tag", Version = "", UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }), Literals = Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToList(), Description = GenericOptionalType.Description };
                            var tu = new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Description = GenericOptionalType.Description };
                            l.Add(GetFile(tut.TypeFriendlyName(), GetEnum(tut)));
                            l.Add(GetFile(tu.TypeFriendlyName(), GetTaggedUnion(tu)));
                        }
                        else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.ParameterValues.Count == 2)
                        {
                            var KeyType = gts.GenericTypeSpec.ParameterValues[0];
                            var ValueType = gts.GenericTypeSpec.ParameterValues[1];
                            var Name = "KeyValuePairOf" + KeyType.TypeFriendlyName() + "And" + ValueType.TypeFriendlyName();
                            var Fields = GenericKeyValuePairType.Fields.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef && a.Type.GenericParameterRef == "TKey" ? KeyType : a.Type.OnGenericParameterRef && a.Type.GenericParameterRef == "TValue" ? ValueType : a.Type, Description = a.Description }).ToList();
                            var r = new RecordDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Fields = Fields, Description = GenericOptionalType.Description };
                            l.Add(GetFile(r.TypeFriendlyName(), GetRecord(r)));
                        }
                    }
                }

                var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
                if (Commands.Count > 0)
                {
                    l.Add(GetFile("IApplicationClient", GetIApplicationClient(Commands)));
                }

                return l;
            }

            public ActionScript.FileResult GetFile(String Path, List<String> Type)
            {
                var a = EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Type", Type)).Select(Line => Line.TrimEnd(' ')).ToList();

                return new ActionScript.FileResult() { Path = Path, Content = String.Join("\r\n", a) };
            }

            public String GetTypeString(PrimitiveDef Type)
            {
                return TemplateInfo.PrimitiveMappings[Type.Name].PlatformName;
            }

            private HashSet<String> EnumSet = new HashSet<String>();
            public String GetTypeString(TypeSpec Type)
            {
                if (Type.OnTypeRef)
                {
                    if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Name))
                    {
                        return TemplateInfo.PrimitiveMappings[Type.TypeRef.Name].PlatformName;
                    }
                    else if (EnumSet.Contains(Type.TypeRef.VersionedName()))
                    {
                        return "int";
                    }
                    else
                    {
                        return Type.TypeRef.TypeFriendlyName();
                    }
                }
                else if (Type.OnTuple)
                {
                    return "TupleOf" + String.Join("And", Type.Tuple.Select(t => t.TypeFriendlyName()));
                }
                else if (Type.OnGenericTypeSpec)
                {
                    if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && Type.GenericTypeSpec.ParameterValues.Count == 1)
                    {
                        return "Opt" + Type.GenericTypeSpec.ParameterValues.Single().TypeFriendlyName();
                    }
                    else if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && Type.GenericTypeSpec.ParameterValues.Count == 2)
                    {
                        return "Vector.<KeyValuePairOf" + Type.GenericTypeSpec.ParameterValues[0].TypeFriendlyName() + "And" + Type.GenericTypeSpec.ParameterValues[1].TypeFriendlyName() + ">";
                    }
                    if (Type.GenericTypeSpec.ParameterValues.Count() > 0)
                    {
                        return GetTypeString(Type.GenericTypeSpec.TypeSpec) + ".<" + String.Join(", ", Type.GenericTypeSpec.ParameterValues.Select(p => GetTypeString(p))) + ">";
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

            public List<String> GetAlias(AliasDef a)
            {
                return GetTemplate("Alias").Substitute("Name", a.TypeFriendlyName()).Substitute("Type", GetTypeString(a.Type)).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(a.Description));
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
                var Fields = Types.Select((tp, i) => new VariableDef { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToList();
                return GetTemplate("Tuple").Substitute("Name", Name).Substitute("TupleElements", TupleElements);
            }
            public List<String> GetField(VariableDef f)
            {
                var d = f.Description;
                if (f.Type.OnTypeRef && EnumSet.Contains(f.Type.TypeRef.VersionedName()))
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
                var Fields = GetFields(r.Fields);
                return GetTemplate("Record").Substitute("Name", r.TypeFriendlyName()).Substitute("Fields", Fields).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public List<String> GetAlternativeCreate(TaggedUnionDef tu, VariableDef a)
            {
                if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Name.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                {
                    return GetTemplate("AlternativeCreateUnit").Substitute("TaggedUnionName", tu.TypeFriendlyName()).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
                }
                else
                {
                    return GetTemplate("AlternativeCreate").Substitute("TaggedUnionName", tu.TypeFriendlyName()).Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
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
                return GetTemplate("AlternativePredicate").Substitute("TaggedUnionName", tu.TypeFriendlyName()).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
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
            public List<String> GetAlternative(VariableDef a)
            {
                var d = a.Description;
                if (a.Type.OnTypeRef && EnumSet.Contains(a.Type.TypeRef.VersionedName()))
                {
                    if (d == "")
                    {
                        d = String.Format("类型: {0}", a.Type.TypeRef.TypeFriendlyName());
                    }
                    else
                    {
                        d = String.Format("{0}\r\n类型: {1}", d, a.Type.TypeRef.TypeFriendlyName());
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
            public List<String> GetTaggedUnion(TaggedUnionDef tu)
            {
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                var Name = tu.TypeFriendlyName();
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public List<String> GetLiteral(LiteralDef lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public List<String> GetLiterals(List<LiteralDef> Literals)
            {
                var l = new List<String>();
                foreach (var lrl in Literals)
                {
                    l.AddRange(GetLiteral(lrl));
                }
                return l;
            }
            public List<String> GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
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
                        var Description = String.Join("\r\n", (new List<String> { c.ClientCommand.Description }).Concat(GetTemplate("IApplicationClient_ClientCommandCallback").Substitute("Name", c.ClientCommand.TypeFriendlyName())));
                        l.AddRange(GetTemplate("IApplicationClient_ClientCommand").Substitute("Name", c.ClientCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(Description)));
                    }
                    else if (c.OnServerCommand)
                    {
                        var Description = String.Join("\r\n", (new List<String> { c.ServerCommand.Description }).Concat(GetTemplate("IApplicationClient_ServerCommandFunction").Substitute("Name", c.ServerCommand.TypeFriendlyName())));
                        l.AddRange(GetTemplate("IApplicationClient_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(Description)));
                    }
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
            public static String GetEscapedIdentifier(String Identifier)
            {
                var l = new List<String>();
                foreach (var IdentifierPart in Identifier.Split('.'))
                {
                    if (TemplateInfo.Keywords.Contains(IdentifierPart))
                    {
                        l.Add("__" + IdentifierPart);
                    }
                    else
                    {
                        l.Add(IdentifierPart);
                    }
                }
                return String.Join(".", l);
            }
            private static Regex rIdentifier = new Regex(@"(?<!\[\[)\[\[(?<Identifier>.*?)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            public static List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Lines.Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToList();
            }
        }

        public static String TypeFriendlyName(this TypeSpec Type)
        {
            if (Type.OnGenericTypeSpec && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && Type.GenericTypeSpec.ParameterValues.Count == 1)
            {
                return "Opt" + TypeFriendlyName(Type.GenericTypeSpec.ParameterValues.Single());
            }
            return ObjectSchema.ObjectSchemaExtensions.TypeFriendlyName(Type, gpr => gpr, (t, e) => TypeFriendlyName(t));
        }
        public static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + LowercaseCamelize(Parameter) + "}";
            var LowercaseValue = LowercaseCamelize(Value);
            var UppercaseCapitalizeString = "${" + Parameter.ToUpper() + "}";
            var UppercaseValue = Value.ToUpper();

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

                if (Line.Contains(UppercaseCapitalizeString))
                {
                    NewLine = NewLine.Replace(UppercaseCapitalizeString, UppercaseValue);
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
