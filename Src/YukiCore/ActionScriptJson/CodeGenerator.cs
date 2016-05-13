//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0 JSON通讯代码生成器
//  Version:     2016.05.13.
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
        public static List<ActionScript.FileResult> CompileToActionScriptJson(this Schema Schema, String PackageName)
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
            private ISchemaClosureGenerator SchemaClosureGenerator;
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
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.PackageName = PackageName;
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).Hash();

                InnerWriter = new ActionScript.Common.CodeGenerator.Writer(Schema, PackageName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }
            
            public List<ActionScript.FileResult> GetFiles()
            {
                var l = new List<ActionScript.FileResult>();

                l.Add(GetFile("JsonTranslator", GetJsonTranslator()));

                var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToList();
                if (Commands.Count > 0)
                {
                    l.Add(GetFile("IJsonSender", GetTemplate("IJsonSender")));
                    l.Add(GetFile("JsonSerializationClient", GetClient(Commands)));
                }

                return l;
            }

            public ActionScript.FileResult GetFile(String Path, List<String> Type)
            {
                var a = EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Type", Type)).Select(Line => Line.TrimEnd(' ')).ToList();

                return new ActionScript.FileResult() { Path = Path, Content = String.Join("\r\n", a) };
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }

            public List<String> GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public List<String> GetJsonTranslator()
            {
                return GetTemplate("JsonTranslator").Substitute("Serializers", GetJsonTranslatorSerializers());
            }

            public List<String> GetJsonTranslatorSerializers()
            {
                var l = new List<String>();

                foreach (var c in Schema.TypeRefs.Concat(Schema.Types))
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
                        l.AddRange(GetJsonTranslatorRecord(new RecordDef { Name = c.Alias.Name, Version = c.Alias.Version, GenericParameters = c.Alias.GenericParameters, Fields = new List<VariableDef> { new VariableDef { Name = "Value", Type = c.Alias.Type, Description = "" } }, Description = "" }));
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

                var scg = Schema.GetSchemaClosureGenerator();
                var sc = scg.GetClosure(Schema.TypeRefs.Concat(Schema.Types), new List<TypeSpec> { });
                var Tuples = sc.TypeSpecs.Where(t => t.OnTuple).ToList();
                var GenericTypeSpecs = sc.TypeSpecs.Where(t => t.OnGenericTypeSpec).ToList();

                foreach (var t in Tuples)
                {
                    l.AddRange(GetJsonTranslatorRecord(new RecordDef { Name = t.TypeFriendlyName(), Version = "", GenericParameters = new List<VariableDef> { }, Fields = t.Tuple.Types.Select((tp, i) => new VariableDef { Name = String.Format("Item{0}", i), Type = tp, Description = "" }).ToList(), Description = "" }));
                    l.Add("");
                }

                var GenericOptionalTypes = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.Name() == "Optional").ToList();
                TaggedUnionDef GenericOptionalType = null;
                if (GenericOptionalTypes.Count > 0)
                {
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new List<VariableDef> { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new List<VariableDef> { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                }
                foreach (var gts in GenericTypeSpecs)
                {
                    if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gts.GenericTypeSpec.GenericParameterValues.Count == 1)
                    {
                        var ElementType = gts.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToList();
                        l.AddRange(GetJsonTranslatorTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new List<VariableDef> { }, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gts.GenericTypeSpec.GenericParameterValues.Count == 1)
                    {
                        l.AddRange(GetJsonTranslatorList(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gts.GenericTypeSpec.GenericParameterValues.Count == 1)
                    {
                        l.AddRange(GetJsonTranslatorSet(gts));
                        l.Add("");
                    }
                    else if (gts.GenericTypeSpec.TypeSpec.OnTypeRef && gts.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gts.GenericTypeSpec.GenericParameterValues.Count == 2)
                    {
                        l.AddRange(GetJsonTranslatorMap(gts));
                        l.Add("");
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("NonListGenericTypeNotSupported: {0}", gts.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
                    }
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l;
            }
            public List<String> GetJsonTranslatorRecord(RecordDef r)
            {
                return GetJsonTranslatorRecord(r.TypeFriendlyName(), r.Fields);
            }
            public List<String> GetJsonTranslatorRecord(String Name, List<VariableDef> Fields)
            {
                var l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetJsonTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetJsonTranslatorFieldTos(Fields)));
                return l;
            }
            public List<String> GetJsonTranslatorFieldFroms(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldFrom").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorFieldTos(List<VariableDef> Fields)
            {
                var l = new List<String>();
                foreach (var a in Fields)
                {
                    l.AddRange(GetTemplate("JsonTranslator_FieldTo").Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorTaggedUnion(TaggedUnionDef tu)
            {
                return GetJsonTranslatorTaggedUnion(tu.TypeFriendlyName(), tu.Alternatives);
            }
            public List<String> GetJsonTranslatorTaggedUnion(String Name, List<VariableDef> Alternatives)
            {
                var TagName = Name + "Tag";
                var l = new List<String>();
                l.AddRange(GetTemplate("JsonTranslator_Enum").Substitute("Name", TagName));
                l.AddRange(GetTemplate("JsonTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetJsonTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetJsonTranslatorAlternativeTos(Name, Alternatives)));
                return l;
            }
            public List<String> GetJsonTranslatorAlternativeFroms(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeFrom").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorAlternativeTos(String TaggedUnionName, List<VariableDef> Alternatives)
            {
                var l = new List<String>();
                foreach (var a in Alternatives)
                {
                    l.AddRange(GetTemplate("JsonTranslator_AlternativeTo").Substitute("TaggedUnionName", TaggedUnionName).Substitute("Name", a.Name).Substitute("TypeFriendlyName", a.Type.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetJsonTranslatorEnum(EnumDef e)
            {
                return GetTemplate("JsonTranslator_Enum").Substitute("Name", e.TypeFriendlyName());
            }
            public List<String> GetJsonTranslatorClientCommand(ClientCommandDef c)
            {
                var l = new List<String>();
                l.AddRange(GetJsonTranslatorRecord(c.TypeFriendlyName() + "Request", c.OutParameters));
                l.AddRange(GetJsonTranslatorTaggedUnion(c.TypeFriendlyName() + "Reply", c.InParameters));
                return l;
            }
            public List<String> GetJsonTranslatorServerCommand(ServerCommandDef c)
            {
                var l = new List<String>();
                return GetJsonTranslatorRecord(c.TypeFriendlyName() + "Event", c.OutParameters);
            }
            public List<String> GetJsonTranslatorList(TypeSpec c)
            {
                return GetTemplate("JsonTranslator_List").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public List<String> GetJsonTranslatorSet(TypeSpec c)
            {
                return GetTemplate("JsonTranslator_Set").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public List<String> GetJsonTranslatorMap(TypeSpec c)
            {
                var KeyTypeFriendlyName = c.GenericTypeSpec.GenericParameterValues[0].TypeSpec.TypeFriendlyName();
                var ValueTypeFriendlyName = c.GenericTypeSpec.GenericParameterValues[0].TypeSpec.TypeFriendlyName();
                return GetTemplate("JsonTranslator_Map").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("KeyTypeFriendlyName", KeyTypeFriendlyName).Substitute("ValueTypeFriendlyName", ValueTypeFriendlyName);
            }

            public List<String> GetClient(List<TypeDef> Commands)
            {
                var ClientCommands = Commands.Where(c => c.OnClientCommand).ToList();
                var ServerCommands = Commands.Where(c => c.OnServerCommand).ToList();
                var NumClientCommand = ClientCommands.Count;
                var Client_ServerCommandHandles = GetClientServerCommandHandles(ServerCommands);
                var Client_ClientCommandHandles = GetClientClientCommandHandles(ClientCommands);
                var Client_ClientCommandDeques = GetClientClientCommandDeques(ClientCommands);
                var Client_Commands = GetClientCommands(Commands);
                return GetTemplate("JsonSerializationClient").Substitute("NumClientCommand", NumClientCommand.ToInvariantString()).Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Client_ServerCommandHandles", Client_ServerCommandHandles).Substitute("Client_ClientCommandHandles", Client_ClientCommandHandles).Substitute("Client_ClientCommandDeques", Client_ClientCommandDeques).Substitute("Client_Commands", Client_Commands);
            }
            public List<String> GetClientServerCommandHandles(List<TypeDef> ServerCommands)
            {
                var l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("JsonSerializationClient_ServerCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l;
            }
            public List<String> GetClientClientCommandHandles(List<TypeDef> ClientCommands)
            {
                var l = new List<String>();
                foreach (var c in ClientCommands)
                {
                    var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("JsonSerializationClient_ClientCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l;
            }
            public List<String> GetClientClientCommandDeques(List<TypeDef> ClientCommands)
            {
                var l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("JsonSerializationClient_ClientCommandDeque").Substitute("Name", c.TypeFriendlyName()).Substitute("ClientCommandIndex", k.ToInvariantString()));
                    k += 1;
                }
                return l;
            }
            public List<String> GetClientCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
                var k = 0;
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        var CommandHash = (UInt32)(SchemaClosureGenerator.GetSubSchema(new List<TypeDef> { c }, new List<TypeSpec> { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("JsonSerializationClient_ClientCommand").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommandIndex", k.ToInvariantString()));
                        k += 1;
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("JsonSerializationClient_ServerCommand").Substitute("Name", c.TypeFriendlyName()));
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
                return ActionScript.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return ActionScript.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return ActionScript.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String TypeFriendlyName(this TypeSpec Type)
        {
            return Yuki.ObjectSchema.ActionScript.Common.CodeGenerator.TypeFriendlyName(Type);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return ActionScript.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return ActionScript.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
