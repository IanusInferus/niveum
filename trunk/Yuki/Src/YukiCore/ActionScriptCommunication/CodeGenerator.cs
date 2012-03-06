//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0通讯代码生成器
//  Version:     2012.03.06.
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

namespace Yuki.ObjectSchema.ActionScriptCommunication
{
    public static class CodeGenerator
    {
        public class FileResult
        {
            public String Path;
            public String Content;
        }

        public static FileResult[] CompileToActionScriptCommunication(this Schema Schema, String PackageName)
        {
            var s = Schema.Reduce();
            var h = s.Hash();
            Writer w = new Writer() { Schema = s, PackageName = PackageName, Hash = h };
            var Files = w.GetFiles();
            return Files;
        }

        private class TemplateInfo
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

        private class Writer
        {
            private static TemplateInfo TemplateInfo;

            public Schema Schema;
            public String PackageName;
            public UInt64 Hash;

            static Writer()
            {
                var b = Properties.Resources.ActionScriptCommunication;
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

            public FileResult[] GetFiles()
            {
                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Value == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.Name()));
                    }
                }

                List<FileResult> l = new List<FileResult>();

                EnumSet = new HashSet<String>(Schema.TypeRefs.Concat(Schema.Types).Where(c => c.OnEnum).Select(c => c.Enum.Name).Distinct());
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
                            l.Add(GetFile("Unit", GetRecord(new Record { Name = "Unit", Fields = new Variable[] { }, Description = "" })));
                        }
                    }
                    else if (c.OnAlias)
                    {
                        l.Add(GetFile(c.Alias.Name, GetRecord(new Record { Name = c.Alias.Name, Fields = new Variable[] { new Variable { Name = "Value", Type = c.Alias.Type, Description = "" } }, Description = "" })));
                    }
                    else if (c.OnRecord)
                    {
                        l.Add(GetFile(c.Record.Name, GetRecord(c.Record)));
                    }
                    else if (c.OnTaggedUnion)
                    {
                        l.Add(GetFile(c.TaggedUnion.Name + "Tag", GetEnum(new Enum { Name = c.TaggedUnion.Name + "Tag", UnderlyingType = TypeSpec.CreateTypeRef("Int"), Literals = c.TaggedUnion.Alternatives.Select((a, i) => new Literal { Name = a.Name, Value = i, Description = a.Description }).ToArray(), Description = c.TaggedUnion.Description })));
                        l.Add(GetFile(c.TaggedUnion.Name, GetTaggedUnion(c.TaggedUnion)));
                    }
                    else if (c.OnEnum)
                    {
                        l.Add(GetFile(c.Enum.Name, GetEnum(c.Enum)));
                    }
                    else if (c.OnClientCommand)
                    {
                        l.Add(GetFile(c.ClientCommand.Name + "Request", GetRecord(new Record { Name = c.ClientCommand.Name + "Request", Fields = c.ClientCommand.OutParameters, Description = c.ClientCommand.Description })));
                        l.Add(GetFile(c.ClientCommand.Name + "ReplyTag", GetEnum(new Enum { Name = c.ClientCommand.Name + "ReplyTag", UnderlyingType = TypeSpec.CreateTypeRef("Int"), Literals = c.ClientCommand.InParameters.Select((a, i) => new Literal { Name = a.Name, Value = i, Description = a.Description }).ToArray(), Description = c.ClientCommand.Description })));
                        l.Add(GetFile(c.ClientCommand.Name + "Reply", GetTaggedUnion(new TaggedUnion { Name = c.ClientCommand.Name + "Reply", Alternatives = c.ClientCommand.InParameters, Description = c.ClientCommand.Description })));
                    }
                    else if (c.OnServerCommand)
                    {
                        l.Add(GetFile(c.ServerCommand.Name + "Event", GetRecord(new Record { Name = c.ServerCommand.Name + "Event", Fields = c.ServerCommand.OutParameters, Description = c.ServerCommand.Description })));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }

                foreach (var t in Tuples)
                {
                    l.Add(GetFile(t.TypeFriendlyName(), GetRecord(new Record { Name = t.TypeFriendlyName(), Fields = t.Tuple.Types.Select((tp, i) => new Variable { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToArray(), Description = "" })));
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                if (GenericOptionalTypes.Length > 0)
                {
                    var GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                    foreach (var gps in GenericTypeSpecs)
                    {
                        if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef == "Optional")
                        {
                            var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                            var Name = "Opt" + ElementType.TypeFriendlyName();
                            var Alternatives = GenericOptionalType.Alternatives.Select(a => new Variable { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                            l.Add(GetFile(Name + "Tag", GetEnum(new Enum { Name = Name + "Tag", UnderlyingType = TypeSpec.CreateTypeRef("Int"), Literals = Alternatives.Select((a, i) => new Literal { Name = a.Name, Value = i, Description = a.Description }).ToArray(), Description = GenericOptionalType.Description })));
                            l.Add(GetFile(Name, GetTaggedUnion(new TaggedUnion { Name = Name, Alternatives = Alternatives, Description = GenericOptionalType.Description })));
                        }
                    }
                }

                var ClientCommands = Schema.Types.Where(t => t.OnClientCommand).Select(t => t.ClientCommand).ToArray();
                var ServerCommands = Schema.Types.Where(t => t.OnServerCommand).Select(t => t.ServerCommand).ToArray();
                l.Add(GetFile("ISender", GetTemplate("ISender")));
                l.Add(GetFile("IClientImplementation", GetIClientImplementation(ServerCommands)));
                l.Add(GetFile("Client", GetClient(ClientCommands, ServerCommands)));
                l.Add(GetFile("JsonTranslator", GetJsonTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray())));

                return l.ToArray();
            }

            public FileResult GetFile(String Path, String[] Type)
            {
                var a = EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Type", Type));

                return new FileResult() { Path = Path, Content = String.Join("\r\n", a) };
            }

            public String GetTypeString(Primitive Type)
            {
                return TemplateInfo.PrimitiveMappings[Type.Name].PlatformName;
            }

            private HashSet<String> EnumSet = new HashSet<String>();
            public String GetTypeString(TypeSpec Type)
            {
                switch (Type._Tag)
                {
                    case TypeSpecTag.TypeRef:
                        if (TemplateInfo.PrimitiveMappings.ContainsKey(Type.TypeRef.Value))
                        {
                            return TemplateInfo.PrimitiveMappings[Type.TypeRef.Value].PlatformName;
                        }
                        else if (EnumSet.Contains(Type.TypeRef.Value))
                        {
                            return "int";
                        }
                        else
                        {
                            return Type.TypeRef.Value;
                        }
                    case TypeSpecTag.Tuple:
                        {
                            return "TupleOf" + String.Join("And", Type.Tuple.Types.Select(t => t.TypeFriendlyName()).ToArray());
                        }
                    case TypeSpecTag.GenericTypeSpec:
                        {
                            if (Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef.Value == "Optional")
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

            public String[] GetField(Variable f)
            {
                var d = f.Description;
                if (f.Type.OnTypeRef && EnumSet.Contains(f.Type.TypeRef.Value))
                {
                    if (d == "")
                    {
                        d = String.Format("类型: {0}", f.Type.TypeRef.Value);
                    }
                    else
                    {
                        d = String.Format("{0}\r\n类型: {1}", d, f.Type.TypeRef.Value);
                    }
                }
                return GetTemplate("Field").Substitute("Name", f.Name).Substitute("Type", GetTypeString(f.Type)).Substitute("XmlComment", GetXmlComment(d));
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
                var Fields = GetFields(r.Fields);
                return GetTemplate("Record").Substitute("Name", r.Name).Substitute("Fields", Fields).Substitute("FieldFroms", GetJsonTranslatorFieldFroms(r.Fields)).Substitute("FieldTos", GetJsonTranslatorFieldTos(r.Fields)).Substitute("XmlComment", GetXmlComment(r.Description));
            }
            public String[] GetAlternativeCreate(TaggedUnion tu, Variable a)
            {
                if ((a.Type.OnTypeRef) && (a.Type.TypeRef.Value.Equals("Unit", StringComparison.OrdinalIgnoreCase)))
                {
                    return GetTemplate("AlternativeCreateUnit").Substitute("TaggedUnionName", tu.Name).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
                }
                else
                {
                    return GetTemplate("AlternativeCreate").Substitute("TaggedUnionName", tu.Name).Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(a.Description));
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
                return GetTemplate("AlternativePredicate").Substitute("TaggedUnionName", tu.Name).Substitute("Name", a.Name).Substitute("XmlComment", GetXmlComment(a.Description));
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
            public String[] GetAlternative(Variable a)
            {
                var d = a.Description;
                if (a.Type.OnTypeRef && EnumSet.Contains(a.Type.TypeRef.Value))
                {
                    if (d == "")
                    {
                        d = String.Format("类型: {0}", a.Type.TypeRef.Value);
                    }
                    else
                    {
                        d = String.Format("{0}\r\n类型: {1}", d, a.Type.TypeRef.Value);
                    }
                }
                return GetTemplate("Alternative").Substitute("Name", a.Name).Substitute("Type", GetTypeString(a.Type)).Substitute("XmlComment", GetXmlComment(d));
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
            public String[] GetTaggedUnion(TaggedUnion tu)
            {
                var Alternatives = GetAlternatives(tu.Alternatives);
                var AlternativeCreates = GetAlternativeCreates(tu);
                var AlternativePredicates = GetAlternativePredicates(tu);
                return GetTemplate("TaggedUnion").Substitute("Name", tu.Name).Substitute("Alternatives", Alternatives).Substitute("AlternativeCreates", AlternativeCreates).Substitute("AlternativePredicates", AlternativePredicates).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(tu.Name, tu.Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(tu.Name, tu.Alternatives)).Substitute("XmlComment", GetXmlComment(tu.Description));
            }
            public String[] GetLiteral(Literal lrl)
            {
                return GetTemplate("Literal").Substitute("Name", lrl.Name).Substitute("Value", lrl.Value.ToInvariantString()).Substitute("XmlComment", GetXmlComment(lrl.Description));
            }
            public String[] GetLiterals(Literal[] Literals)
            {
                List<String> l = new List<String>();
                foreach (var lrl in Literals)
                {
                    l.AddRange(GetLiteral(lrl));
                }
                return l.ToArray();
            }
            public String[] GetEnum(Enum e)
            {
                var Literals = GetLiterals(e.Literals);
                return GetTemplate("Enum").Substitute("Name", e.Name).Substitute("Literals", Literals).Substitute("XmlComment", GetXmlComment(e.Description));
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

            public String[] GetIClientImplementation(ServerCommand[] Commands)
            {
                return GetTemplate("IClientImplementation").Substitute("Commands", GetIClientImplementationCommands(Commands));
            }
            public String[] GetIClientImplementationCommands(ServerCommand[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    l.AddRange(GetTemplate("IClientImplementation_ServerCommand").Substitute("Name", c.Name).Substitute("XmlComment", GetXmlComment(c.Description)));
                }
                return l.ToArray();
            }
            public String[] GetClient(ClientCommand[] ClientCommands, ServerCommand[] ServerCommands)
            {
                var NumClientCommand = ClientCommands.Length;
                var Client_ServerCommandHandles = GetClientServerCommandHandles(ServerCommands);
                var Client_ClientCommandHandles = GetClientClientCommandHandles(ClientCommands);
                var Client_ClientCommandDeques = GetClientClientCommandDeques(ClientCommands);
                var Client_ClientCommands = GetClientClientCommands(ClientCommands);
                return GetTemplate("Client").Substitute("NumClientCommand", NumClientCommand.ToInvariantString()).Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Client_ServerCommandHandles", Client_ServerCommandHandles).Substitute("Client_ClientCommandHandles", Client_ClientCommandHandles).Substitute("Client_ClientCommandDeques", Client_ClientCommandDeques).Substitute("Client_ClientCommands", Client_ClientCommands);
            }
            public String[] GetClientServerCommandHandles(ServerCommand[] ServerCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    l.AddRange(GetTemplate("Client_ServerCommandHandle").Substitute("Name", c.Name));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandHandles(ClientCommand[] ClientCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("Client_ClientCommandHandle").Substitute("Name", c.Name));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandDeques(ClientCommand[] ClientCommands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("Client_ClientCommandDeque").Substitute("Name", c.Name).Substitute("ClientCommandIndex", k.ToInvariantString()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommands(ClientCommand[] ClientCommands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("Client_ClientCommand").Substitute("Name", c.Name).Substitute("ClientCommandIndex", k.ToInvariantString()).Substitute("XmlComment", GetXmlComment(c.Description)));
                    k += 1;
                }
                return l.ToArray();
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
                        l.AddRange(GetJsonTranslatorRecord(new Record { Name = c.Alias.Name, Fields = new Variable[] { new Variable { Name = "Value", Type = c.Alias.Type, Description = "" } }, Description = "" }));
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
                    l.AddRange(GetJsonTranslatorRecord(new Record { Name = t.TypeFriendlyName(), GenericParameters = new Variable[] { }, Fields = t.Tuple.Types.Select((tp, i) => new Variable { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToArray(), Description = "" }));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToArray();
                TaggedUnion GenericOptionalType = null;
                if (GenericOptionalTypes.Length > 0)
                {
                    GenericOptionalType = GenericOptionalTypes.Single().TaggedUnion;
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && TemplateInfo.PrimitiveMappings.ContainsKey(gps.GenericTypeSpec.TypeSpec.TypeRef.Value) && TemplateInfo.PrimitiveMappings[gps.GenericTypeSpec.TypeSpec.TypeRef.Value].PlatformName == "Vector")
                    {
                        l.AddRange(GetJsonTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef == "Optional")
                    {
                        var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new Variable { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                        l.AddRange(GetJsonTranslatorTaggedUnion(new TaggedUnion { Name = Name, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
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
            public String[] GetJsonTranslatorRecord(Record a)
            {
                return GetJsonTranslatorRecord(a.Name, a.Fields);
            }
            public String[] GetJsonTranslatorRecord(String Name, Variable[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldFroms(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorFieldTos(Variable[] Fields)
            {
                List<String> l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorTaggedUnion(TaggedUnion tu)
            {
                return GetJsonTranslatorTaggedUnion(tu.Name, tu.Alternatives);
            }
            public String[] GetJsonTranslatorTaggedUnion(String Name, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", Name + "Tag"));
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeFroms(String TaggedUnionName, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorAlternativeTos(String TaggedUnionName, Variable[] Alternatives)
            {
                List<String> l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetJsonTranslatorEnum(Enum e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.Name);
            }
            public String[] GetJsonTranslatorClientCommand(ClientCommand c)
            {
                List<String> l = new List<String>();
                l.AddRange(GetJsonTranslatorRecord(c.Name + "Request", c.OutParameters));
                l.AddRange(GetJsonTranslatorTaggedUnion(c.Name + "Reply", c.InParameters));
                return l.ToArray();
            }
            public String[] GetJsonTranslatorServerCommand(ServerCommand c)
            {
                List<String> l = new List<String>();
                return GetJsonTranslatorRecord(c.Name + "Event", c.OutParameters);
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
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return Lines.Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToArray();
            }
        }

        private static String TypeFriendlyName(this TypeSpec Type)
        {
            if (Type.OnGenericTypeSpec && Type.GenericTypeSpec.TypeSpec.OnTypeRef && Type.GenericTypeSpec.TypeSpec.TypeRef == "Optional")
            {
                return "Opt" + TypeFriendlyName(Type.GenericTypeSpec.GenericParameterValues.Single().TypeSpec);
            }
            return ObjectSchema.ObjectSchemaExtensions.TypeFriendlyName(Type, gpr => gpr.Value, (t, e) => TypeFriendlyName(t));
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
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
        private static String LowercaseCamelize(String PascalName)
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
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
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
