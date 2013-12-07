//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0二进制通讯代码生成器
//  Version:     2013.12.07.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.ActionScriptBinary
{
    public static class CodeGenerator
    {
        public static ActionScript.FileResult[] CompileToActionScriptBinary(this Schema Schema, String PackageName)
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
            private Func<IEnumerable<TypeDef>, IEnumerable<TypeSpec>, Schema> SubSchemaGen;
            private String PackageName;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScript);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScriptBinary);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String PackageName)
            {
                this.Schema = Schema;
                this.SubSchemaGen = Schema.GetSubSchemaGenerator();
                this.PackageName = PackageName;
                this.Hash = SubSchemaGen(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { }).Hash();

                InnerWriter = new ActionScript.Common.CodeGenerator.Writer(Schema, PackageName);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public ActionScript.FileResult[] GetFiles()
            {
                List<ActionScript.FileResult> l = new List<ActionScript.FileResult>();

                l.Add(GetFile("BinaryTranslator", GetBinaryTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray())));

                var Commands = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).Where(t => t.Version() == "").ToArray();
                if (Commands.Length > 0)
                {
                    l.Add(GetFile("IBinarySender", GetTemplate("IBinarySender")));
                    l.Add(GetFile("BinarySerializationClient", GetClient(Commands)));
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
                    GenericOptionalType = new TaggedUnionDef { Name = "TaggedUnion", Version = "", GenericParameters = new VariableDef[] { new VariableDef { Name = "T", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Type", Version = "" }), Description = "" } }, Alternatives = new VariableDef[] { new VariableDef { Name = "NotHasValue", Type = TypeSpec.CreateTypeRef(new TypeRef { Name = "Unit", Version = "" }), Description = "" }, new VariableDef { Name = "HasValue", Type = TypeSpec.CreateGenericParameterRef(new GenericParameterRef { Value = "T" }), Description = "" } }, Description = "" };
                }
                foreach (var gps in GenericTypeSpecs)
                {
                    if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Optional" && gps.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        var ElementType = gps.GenericTypeSpec.GenericParameterValues.Single().TypeSpec;
                        var Name = "Opt" + ElementType.TypeFriendlyName();
                        var Alternatives = GenericOptionalType.Alternatives.Select(a => new VariableDef { Name = a.Name, Type = a.Type.OnGenericParameterRef ? ElementType : a.Type, Description = a.Description }).ToArray();
                        l.AddRange(GetBinaryTranslatorTaggedUnion(new TaggedUnionDef { Name = Name, Version = "", GenericParameters = new VariableDef[] { }, Alternatives = Alternatives, Description = GenericOptionalType.Description }));
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "List" && gps.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetBinaryTranslatorList(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Set" && gps.GenericTypeSpec.GenericParameterValues.Length == 1)
                    {
                        l.AddRange(GetBinaryTranslatorSet(gps));
                        l.Add("");
                    }
                    else if (gps.GenericTypeSpec.TypeSpec.OnTypeRef && gps.GenericTypeSpec.TypeSpec.TypeRef.Name == "Map" && gps.GenericTypeSpec.GenericParameterValues.Length == 2)
                    {
                        l.AddRange(GetBinaryTranslatorMap(gps));
                        l.Add("");
                    }
                    else
                    {
                        throw new InvalidOperationException(String.Format("GenericTypeNotSupported: {0}", gps.GenericTypeSpec.TypeSpec.TypeRef.VersionedName()));
                    }
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l.ToArray();
            }
            public String[] GetBinaryTranslatorRecord(RecordDef r)
            {
                return GetBinaryTranslatorRecord(r.TypeFriendlyName(), r.Fields);
            }
            public String[] GetBinaryTranslatorRecord(String Name, VariableDef[] Fields)
            {
                List<String> l = new List<String>();
                l.AddRange(GetTemplate("BinaryTranslator_Record").Substitute("Name", Name).Substitute("FieldFroms", GetBinaryTranslatorFieldFroms(Fields)).Substitute("FieldTos", GetBinaryTranslatorFieldTos(Fields)));
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
                l.AddRange(GetTemplate("BinaryTranslator_TaggedUnion").Substitute("Name", Name).Substitute("AlternativeFroms", GetBinaryTranslatorAlternativeFroms(Name, Alternatives)).Substitute("AlternativeTos", GetBinaryTranslatorAlternativeTos(Name, Alternatives)));
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
            public String[] GetBinaryTranslatorList(TypeSpec c)
            {
                return GetTemplate("BinaryTranslator_List").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorSet(TypeSpec c)
            {
                return GetTemplate("BinaryTranslator_Set").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("ElementTypeFriendlyName", c.GenericTypeSpec.GenericParameterValues.Single().TypeSpec.TypeFriendlyName());
            }
            public String[] GetBinaryTranslatorMap(TypeSpec c)
            {
                var KeyTypeFriendlyName = c.GenericTypeSpec.GenericParameterValues[0].TypeSpec.TypeFriendlyName();
                var ValueTypeFriendlyName = c.GenericTypeSpec.GenericParameterValues[1].TypeSpec.TypeFriendlyName();
                return GetTemplate("BinaryTranslator_Map").Substitute("TypeFriendlyName", c.TypeFriendlyName()).Substitute("TypeString", GetTypeString(c)).Substitute("KeyTypeFriendlyName", KeyTypeFriendlyName).Substitute("ValueTypeFriendlyName", ValueTypeFriendlyName);
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
                return GetTemplate("BinarySerializationClient").Substitute("NumClientCommand", NumClientCommand.ToInvariantString()).Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Client_ServerCommandHandles", Client_ServerCommandHandles).Substitute("Client_ClientCommandHandles", Client_ClientCommandHandles).Substitute("Client_ClientCommandDeques", Client_ClientCommandDeques).Substitute("Client_Commands", Client_Commands);
            }
            public String[] GetClientServerCommandHandles(TypeDef[] ServerCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    var CommandHash = (UInt32)(SubSchemaGen(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("BinarySerializationClient_ServerCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandHandles(TypeDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ClientCommands)
                {
                    var CommandHash = (UInt32)(SubSchemaGen(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("BinarySerializationClient_ClientCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandDeques(TypeDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("BinarySerializationClient_ClientCommandDeque").Substitute("Name", c.TypeFriendlyName()).Substitute("ClientCommandIndex", k.ToInvariantString()));
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
                        var CommandHash = (UInt32)(SubSchemaGen(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinarySerializationClient_ClientCommand").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommandIndex", k.ToInvariantString()));
                        k += 1;
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("BinarySerializationClient_ServerCommand").Substitute("Name", c.TypeFriendlyName()));
                    }
                }
                return l.ToArray();
            }

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static String[] GetLines(String Value)
            {
                return ActionScript.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return ActionScript.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return ActionScript.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String TypeFriendlyName(this TypeSpec Type)
        {
            return ActionScript.Common.CodeGenerator.TypeFriendlyName(Type);
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
