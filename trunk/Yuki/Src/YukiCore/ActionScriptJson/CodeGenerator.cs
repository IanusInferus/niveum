//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0 JSON通讯代码生成器
//  Version:     2013.03.31.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;
using Yuki.ObjectSchema.ActionScript.Common;

namespace Yuki.ObjectSchema.ActionScriptJson
{
    public static class CodeGenerator
    {
        public static ActionScript.FileResult[] CompileToActionScriptJson(this Schema Schema, String PackageName)
        {
            var w = new Writer(Schema, PackageName);
            var Files = w.GetFiles();
            return Files;
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private ActionScript.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String PackageName;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScript);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScriptJson);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String PackageName)
            {
                this.Schema = Schema;
                this.PackageName = PackageName;
                this.Hash = this.Schema.Hash();
            }
            
            public ActionScript.FileResult[] GetFiles()
            {
                InnerWriter = new ActionScript.Common.CodeGenerator.Writer(Schema, PackageName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }

                List<ActionScript.FileResult> l = new List<ActionScript.FileResult>();

                InnerWriter.FillEnumSet();

                l.Add(GetFile("JsonTranslator", GetJsonTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray())));

                var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToArray();
                if (Commands.Length > 0)
                {
                    l.Add(GetFile("IJsonSender", GetTemplate("IJsonSender")));
                    l.Add(GetFile("JsonSerializationClient", GetClient(Commands)));
                }

                return l.ToArray();
            }

            public ActionScript.FileResult GetFile(String Path, String[] Type)
            {
                var a = EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Type", Type)).Select(Line => Line.TrimEnd(' ')).ToArray();

                return new ActionScript.FileResult() { Path = Path, Content = String.Join("\r\n", a) };
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }

            public String[] GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
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
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new VariableDef[] { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new VariableDef[] { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gps.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                        l.AddRange(GetJsonTranslatorTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new VariableDef[] { }, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gps.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetJsonTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gps.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetJsonTranslatorSet(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gps.GenericTypeSpec.GenericParameterValues.Length == 2)
                    {
                        l.AddRange(GetJsonTranslatorMap(gps));
                        l.Add("");
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
            public String[] GetJsonTranslatorRecord(RecordDef r)
            {
                return GetJsonTranslatorRecord(r.TypeFriendlyName(), r.Fields);
            }
            public String[] GetJsonTranslatorRecord(String Name, VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetJsonTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetJsonTranslatorFieldTos(Fields)));
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
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives)));
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
            public String[] GetJsonTranslatorList(TypeSpec c)
            {
                return GetTemplate("JsonTranslator_List").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorSet(TypeSpec c)
            {
                return GetTemplate("JsonTranslator_Set").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetJsonTranslatorMap(TypeSpec c)
            {
                var KeyTypeFriendlyName = c.GenericTypeSpec.GenericParameterValues[0].TypeSpec.TypeFriendlyName();
                var ValueTypeFriendlyName = c.GenericTypeSpec.GenericParameterValues[0].TypeSpec.TypeFriendlyName();
                return GetTemplate("JsonTranslator_Map").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("KeyTypeFriendlyName", KeyTypeFriendlyName).Substitute("ValueTypeFriendlyName", ValueTypeFriendlyName);
            }

            public String[] GetClient(TypeDef[] Commands)
            {
                var ClientCommands = Commands.Where(c => c.OnClientCommand).ToArray();
                var ServerCommands = Commands.Where(c => c.OnServerCommand).ToArray();
                var NumClientCommand = ClientCommands.Length;
                var Client_ServerCommandHandles = GetClientServerCommandHandles(ServerCommands);
                var Client_ClientCommandHandles = GetClientClientCommandHandles(ClientCommands);
                var Client_ClientCommandDeques = GetClientClientCommandDeques(ClientCommands);
                var Client_Commands = GetClientCommands(Commands);
                return GetTemplate("JsonSerializationClient").Substitute("NumClientCommand", NumClientCommand.ToInvariantString()).Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Client_ServerCommandHandles", Client_ServerCommandHandles).Substitute("Client_ClientCommandHandles", Client_ClientCommandHandles).Substitute("Client_ClientCommandDeques", Client_ClientCommandDeques).Substitute("Client_Commands", Client_Commands);
            }
            public String[] GetClientServerCommandHandles(TypeDef[] ServerCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("JsonSerializationClient_ServerCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandHandles(TypeDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ClientCommands)
                {
                    var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("JsonSerializationClient_ClientCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandDeques(TypeDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("JsonSerializationClient_ClientCommandDeque").Substitute("Name", c.TypeFriendlyName()).Substitute("ClientCommandIndex", k.ToInvariantString()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetClientCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonSerializationClient_ClientCommand").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommandIndex", k.ToInvariantString()));
                        k += 1;
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("JsonSerializationClient_ServerCommand").Substitute("Name", c.TypeFriendlyName()));
                    }
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

        private static String TypeFriendlyName(this TypeSpec Type)
        {
            return Yuki.ObjectSchema.ActionScript.Common.CodeGenerator.TypeFriendlyName(Type);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return ActionScript.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return ActionScript.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
