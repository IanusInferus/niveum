//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0代码生成器
//  Version:     2012.04.24.
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
        public static FileResult[] CompileToActionScript(this Schema Schema, String PackageName)
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
            }

            public void FillEnumSet()
            {
                EnumSet = new HashSet<String>(Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).Select(c => c.VersionedName()).Distinct());
            }

            public ActionScript.FileResult[] GetFiles()
            {
                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }

                List<ActionScript.FileResult> l = new List<ActionScript.FileResult>();

                FillEnumSet();
                var ltf = new TupleAndGenericTypeSpecFetcher();
                ltf.PushTypeDefs(Schema.TypeRefs.Concat(Schema.Types));
                var Tuples = ltf.GetTuples();
                var GenericTypeSpecs = ltf.GetGenericTypeSpecs();

                foreach (var c in Schema.Types)
                {
                    if (c.GenericParameters().Count() != 0)
                    {
                        continue;
                    }
                    if (c.OnPrimitive)
                    {
                        if (c.Primitive.Name == "Unit")
                        {
                            l.Add(GetFile("Unit", GetRecord(new RecordDef { Name = "Unit", Version = "", Fields = new VariableDef[] { }, Description = "" })));
                        }
                    }
                    else if (c.OnAlias)
                    {
                        var r = new RecordDef { Name = c.Alias.TypeFriendlyName(), Version = "", Fields = new VariableDef[] { new VariableDef { Name = "Value", Type = c.Alias.Type, Description = "" } }, Description = "" };
                        l.Add(GetFile(r.TypeFriendlyName(), GetRecord(r)));
                    }
                    else if (c.OnRecord)
                    {
                        l.Add(GetFile(c.Record.TypeFriendlyName(), GetRecord(c.Record)));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        var tut = new EnumDef { Name = c.TaggedUnion.TypeFriendlyName() + "Tag", Version = "", UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }), Literals = c.TaggedUnion.Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToArray(), Description = c.TaggedUnion.Description };
                        l.Add(GetFile(tut.TypeFriendlyName(), GetEnum(tut)));
                        l.Add(GetFile(c.TaggedUnion.TypeFriendlyName(), GetTaggedUnion(c.TaggedUnion)));
                    }
                    else if (c.OnEnum)
                    {
                        l.Add(GetFile(c.Enum.TypeFriendlyName(), GetEnum(c.Enum)));
                    }
                    else if (c.OnClientCommand)
                    {
                        var creq = new RecordDef { Name = c.ClientCommand.TypeFriendlyName() + "Request", Version = "", GenericParameters = new VariableDef[] { }, Fields = c.ClientCommand.OutParameters, Description = c.ClientCommand.Description };
                        var crept = new EnumDef { Name = c.ClientCommand.TypeFriendlyName() + "ReplyTag", Version = "", UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }), Literals = c.ClientCommand.InParameters.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToArray(), Description = c.ClientCommand.Description };
                        var crep = new TaggedUnionDef { Name = c.ClientCommand.TypeFriendlyName() + "Reply", Version = "", GenericParameters = new VariableDef[] { }, Alternatives = c.ClientCommand.InParameters, Description = c.ClientCommand.Description };
                        l.Add(GetFile(creq.TypeFriendlyName(), GetRecord(creq)));
                        l.Add(GetFile(crept.TypeFriendlyName(), GetEnum(crept)));
                        l.Add(GetFile(crep.TypeFriendlyName(), GetTaggedUnion(crep)));
                    }
                    else if (c.OnServerCommand)
                    {
                        var se = new RecordDef { Name = c.ServerCommand.TypeFriendlyName() + "Event", Version = "", GenericParameters = new VariableDef[] { }, Fields = c.ServerCommand.OutParameters, Description = c.ServerCommand.Description };
                        l.Add(GetFile(se.TypeFriendlyName(), GetRecord(se)));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                foreach (var t in Tuples)
                {
                    l.Add(GetFile(t.TypeFriendlyName(), GetRecord(new RecordDef { Name = t.TypeFriendlyName(), Version = "", GenericParameters = new VariableDef[]{}, Fields = t.Tuple.Types.Select((tp, i) => new VariableDef { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToArray(), Description = "" })));
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                if (GenericOptionalTypes.Length > 0)
                {
                    var GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                    foreach (var gps in GenericTypeSpecs)
                    {
                        if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                        {
                            var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                            var Name = "Opt" + ElementType.TypeFriendlyName();
                            var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                            var tut = new EnumDef { Name = Name + "Tag", Version = "", UnderlyingType = TypeSpec.CreateTypeRef(new TypeRef { Name = "Int", Version = "" }), Literals = Alternatives.Select((a, i) => new LiteralDef { Name = a.Name, Value = i, Description = a.Description }).ToArray(), Description = GenericOptionalType.Description };
                            var tu = new TaggedUnionDef { Name = Name, Version = "", Alternatives = Alternatives, Description = GenericOptionalType.Description };
                            l.Add(GetFile(tut.TypeFriendlyName(), GetEnum(tut)));
                            l.Add(GetFile(tu.TypeFriendlyName(), GetTaggedUnion(tu)));
                        }
                    }
                }

                l.Add(GetFile("BinaryTranslator", GetBinaryTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray())));
                l.Add(GetFile("JsonTranslator", GetJsonTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray())));

                var ClientCommands = Schema.Types.Where(t => t.OnClientCommand).Select(t => t.ClientCommand).ToArray();
                var ServerCommands = Schema.Types.Where(t => t.OnServerCommand).Select(t => t.ServerCommand).ToArray();
                if (ClientCommands.Length + ServerCommands.Length > 0)
                {
                    l.Add(GetFile("IClientImplementation", GetIClientImplementation(ServerCommands)));
                }

                return l.ToArray();
            }

            public ActionScript.FileResult GetFile(String Path, String[] Type)
            {
                var a = EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Type", Type));

                return new ActionScript.FileResult() { Path = Path, Content = String.Join("\r\n", a) };
            }

            public String GetTypeString(PrimitiveDef Type)
            {
                return TemplateInfo.PrimitiveMappings[Type.Name].PlatformName;
            }

            private HashSet<String> EnumSet = new HashSet<String>();
            public String GetTypeString(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
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
                    case TypeSpecTag.Tuple:
                        {
                            return "TupleOf" + String.Join("And", Type.Tuple.Types.Select(t => t.TypeFriendlyName()).ToArray());
                        }
                    case TypeSpecTag.GenericTypeSpec:
                        {
                            if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                            {
                                return "Opt" + Type.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName();
                            }
                            if (Type.GenericTypeSpec.GenericParameterValues.Count() > 0 && Type.GenericTypeSpec.GenericParameterValues.All(gpv => gpv.OnTypeSpec))
                            {
                                return GetTypeString(Type.GenericTypeSpec.TypeSpec) + ".<" + String.Join(", ", Type.GenericTypeSpec.GenericParameterValues.Select(p => GetTypeString(p.TypeSpec)).ToArray()) + ">";
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

            public String[] GetField(VariableDef f)
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
                var Fields = GetFields(r.Fields);
                return GetTemplate("Record").Substitute("Name", r.TypeFriendlyName()).Substitute("Fields", Fields).Substitute("BinaryFieldFroms", GetBinaryTranslatorFieldFroms(r.Fields)).Substitute("BinaryFieldTos", GetBinaryTranslatorFieldTos(r.Fields)).Substitute("JsonFieldFroms", GetJsonTranslatorFieldFroms(r.Fields)).Substitute("JsonFieldTos", GetJsonTranslatorFieldTos(r.Fields)).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetAlternativeCreate(TaggedUnionDef tu, VariableDef a)
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
                return GetTemplate("AlternativePredicate").Substitute("TaggedUnionName", tu.TypeFriendlyName()).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
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
            public String[] GetAlternative(VariableDef a)
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
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                var Name = tu.TypeFriendlyName();
                return GetTemplate("TaggedUnion").Substitute("Name", Name).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("BinaryAlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, tu.Alternatives)).Substitute("BinaryAlternativeTos", GetBinaryTranslatorAlternativeTos(Name, tu.Alternatives)).Substitute("JsonAlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, tu.Alternatives)).Substitute("JsonAlternativeTos", GetJsonTranslatorAlternativeTos(Name, tu.Alternatives)).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public String[] GetLiteral(LiteralDef lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(LiteralDef[] Literals)
            {
                List<String> l = new List<String>();
                foreach (var lrl in Literals)
                {
                    l.AddRange(GetLiteral(lrl));
                }
                return l.ToArray();
            }
            public String[] GetEnum(EnumDef e)
            {
                var Literals = GetLiterals(e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.TypeFriendlyName()).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
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

            public String[] GetIClientImplementation(ServerCommandDef[] Commands)
            {
                return GetTemplate("IClientImplementation").Substitute("Commands", GetIClientImplementationCommands(Commands));
            }
            public String[] GetIClientImplementationCommands(ServerCommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    l.AddRange(GetTemplate("IClientImplementation_ServerCommand").Substitute("Name", c.TypeFriendlyName()).Substitute("XmlComment", GetXmlComment(c.Description)));
                }
                return l.ToArray();
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
                        continue;
                    }
                    else if (c.OnAlias)
                    {
                        l.AddRange(GetBinaryTranslatorRecord(new RecordDef { Name = c.Alias.Name, Version = c.Alias.Version, GenericParameters = c.Alias.GenericParameters, Fields = new VariableDef[] { new VariableDef { Name = "Value", Type = c.Alias.Type, Description = "" } }, Description = "" }));
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
                    l.AddRange(GetBinaryTranslatorRecord(new RecordDef { Name = t.TypeFriendlyName(), Version = "", GenericParameters = new VariableDef[] { }, Fields = t.Tuple.Types.Select((tp, i) => new VariableDef { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToArray(), Description = "" }));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gps.GenericTypeSpec.TypeSpec.TypeRef.Name) && TemplateInfo.PrimitiveMappings[gps.GenericTypeSpec.TypeSpec.TypeRef.Name].PlatformName == "Vector")
                    {
                        l.AddRange(GetBinaryTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                    {
                        var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                        l.AddRange(GetBinaryTranslatorTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", Alternatives = Alternatives, Description = GenericOptionalType.Description }));
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

                return l.ToArray();
            }
            public String[] GetBinaryTranslatorRecord(RecordDef a)
            {
                return GetBinaryTranslatorRecord(a.TypeFriendlyName(), a.Fields);
            }
            public String[] GetBinaryTranslatorRecord(String Name, VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Record").Substitute("Name", Name));
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
                l.AddRange(GetTemplate("BinaryTranslator_Enum").Substitute("Name", TagName));
                l.AddRange(GetTemplate("BinaryTranslator_TaggedUnion").Substitute("Name", Name));
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
                return GetTemplate("BinaryTranslator_Enum").Substitute("Name", e.TypeFriendlyName());
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
            public String[] GetBinaryTranslatorList(TypeSpec l)
            {
                return GetTemplate("BinaryTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }

            public String[] GetJsonTranslator(TypeDef[] Types)
            {
                return GetTemplate("JsonTranslator").Substitute("Serializers", GetJsonTranslatorSerializers(Types));
            }

            public String[] GetJsonTranslatorSerializers(TypeDef[] Types)
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
                        continue;
                    }
                    else if (c.OnAlias)
                    {
                        l.AddRange(GetJsonTranslatorRecord(new RecordDef { Name = c.Alias.Name, Version = c.Alias.Version, GenericParameters = c.Alias.GenericParameters, Fields = new VariableDef[] { new VariableDef { Name = "Value", Type = c.Alias.Type, Description = "" } }, Description = "" }));
                    }
                    else if (c.OnRecord)
                    {
                        l.AddRange(GetJsonTranslatorRecord(c.Record));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        l.AddRange(GetJsonTranslatorTaggedUnion(c.TaggedUnion));
                    }
                    else if (c.OnEnum)
                    {
                        l.AddRange(GetJsonTranslatorEnum(c.Enum));
                    }
                    else if (c.OnClientCommand)
                    {
                        l.AddRange(GetJsonTranslatorClientCommand(c.ClientCommand));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetJsonTranslatorServerCommand(c.ServerCommand));
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
                    l.AddRange(GetJsonTranslatorRecord(new RecordDef { Name = t.TypeFriendlyName(), Version = "", GenericParameters = new VariableDef[] { }, Fields = t.Tuple.Types.Select((tp, i) => new VariableDef { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToArray(), Description = "" }));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gps.GenericTypeSpec.TypeSpec.TypeRef.Name) && TemplateInfo.PrimitiveMappings[gps.GenericTypeSpec.TypeSpec.TypeRef.Name].PlatformName == "Vector")
                    {
                        l.AddRange(GetJsonTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
                    {
                        var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                        l.AddRange(GetJsonTranslatorTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", Alternatives = Alternatives, Description = GenericOptionalType.Description }));
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

                return l.ToArray();
            }
            public String[] GetJsonTranslatorRecord(RecordDef a)
            {
                return GetJsonTranslatorRecord(a.TypeFriendlyName(), a.Fields);
            }
            public String[] GetJsonTranslatorRecord(String Name, VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldFroms(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldTos(VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorTaggedUnion(TaggedUnionDef tu)
            {
                return GetJsonTranslatorTaggedUnion(tu.TypeFriendlyName(), tu.Alternatives);
            }
            public String[] GetJsonTranslatorTaggedUnion(String Name, VariableDef[] Alternatives)
            {
                var TagName = Name + "Tag";
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", TagName));
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeFroms(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeTos(String TaggedUnionName, VariableDef[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorEnum(EnumDef e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorClientCommand(ClientCommandDef c)
            {
                List<String> l = new List<String>();
                l.AddRange(GetJsonTranslatorRecord(c.TypeFriendlyName() + "Request", c.OutParameters));
                l.AddRange(GetJsonTranslatorTaggedUnion(c.TypeFriendlyName() + "Reply", c.InParameters));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorServerCommand(ServerCommandDef c)
            {
                List<String> l = new List<String>();
                return GetJsonTranslatorRecord(c.TypeFriendlyName() + "Event", c.OutParameters);
            }
            public String[] GetJsonTranslatorList(TypeSpec l)
            {
                return GetTemplate("JsonTranslator_List").Substitute("TypeFriendlyName", l.TypeFriendlyName()).Substitute("TypeString", GetTypeString(l)).Substitute("ElementTypeFriendlyName", l.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
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
                        l.Add("__" + IdentifierPart);
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

        public static String TypeFriendlyName(this TypeSpec Type)
        {
            if (Type.OnGenericTypeSpec && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional")
            {
                return "Opt" + TypeFriendlyName(Type.GenericTypeSpec.GenericParameterValues.Single().TypeSpec);
            }
            return ObjectSchema.ObjectSchemaExtensions.TypeFriendlyName(Type, gpr => gpr.Value, (t, e) => TypeFriendlyName(t));
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
