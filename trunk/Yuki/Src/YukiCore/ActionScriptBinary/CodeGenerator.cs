//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0二进制通讯代码生成器
//  Version:     2012.12.19.
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
                this.PackageName = PackageName;
                this.Hash = Schema.Hash();
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
                    var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                    l.AddRange(GetTemplate("BinarySerializationClient_ServerCommandHandle").Substitute("Name", c.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandHandles(TypeDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ClientCommands)
                {
                    var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
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
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { c }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
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
