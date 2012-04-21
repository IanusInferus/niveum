//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构ActionScript3.0 JSON通讯代码生成器
//  Version:     2012.04.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.ActionScriptJson
{
    public static class CodeGenerator
    {
        public static ActionScript.FileResult[] CompileToActionScriptJson(this Schema Schema, String PackageName)
        {
            var s = Schema.Reduce();
            var h = s.Hash();
            Writer w = new Writer() { Schema = s, PackageName = PackageName, Hash = h };
            var Files = w.GetFiles();
            return Files;
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private ActionScript.Common.CodeGenerator.Writer InnerWriter;

            public Schema Schema;
            public String PackageName;
            public UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScript);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.ActionScriptJson);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public ActionScript.FileResult[] GetFiles()
            {
                InnerWriter = new ActionScript.Common.CodeGenerator.Writer { Schema = Schema, PackageName = PackageName, Hash = Hash };

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }

                List<ActionScript.FileResult> l = new List<ActionScript.FileResult>();

                InnerWriter.FillEnumSet();

                var ClientCommands = Schema.Types.Where(t => t.OnClientCommand).Select(t => t.ClientCommand).Where(c => c.Version == "").ToArray();
                var ServerCommands = Schema.Types.Where(t => t.OnServerCommand).Select(t => t.ServerCommand).Where(c => c.Version == "").ToArray();
                if (ClientCommands.Length + ServerCommands.Length > 0)
                {
                    l.Add(GetFile("IJsonSender", GetTemplate("IJsonSender")));
                    l.Add(GetFile("JsonClient", GetClient(ClientCommands, ServerCommands)));
                }

                return l.ToArray();
            }

            public ActionScript.FileResult GetFile(String Path, String[] Type)
            {
                var a = EvaluateEscapedIdentifiers(GetTemplate("Main").Substitute("PackageName", PackageName).Substitute("Imports", Schema.Imports).Substitute("Type", Type));

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

            public String[] GetClient(ClientCommandDef[] ClientCommands, ServerCommandDef[] ServerCommands)
            {
                var NumClientCommand = ClientCommands.Length;
                var Client_ServerCommandHandles = GetClientServerCommandHandles(ServerCommands);
                var Client_ClientCommandHandles = GetClientClientCommandHandles(ClientCommands);
                var Client_ClientCommandDeques = GetClientClientCommandDeques(ClientCommands);
                var Client_ClientCommands = GetClientClientCommands(ClientCommands);
                return GetTemplate("JsonClient").Substitute("NumClientCommand", NumClientCommand.ToInvariantString()).Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Client_ServerCommandHandles", Client_ServerCommandHandles).Substitute("Client_ClientCommandHandles", Client_ClientCommandHandles).Substitute("Client_ClientCommandDeques", Client_ClientCommandDeques).Substitute("Client_ClientCommands", Client_ClientCommands);
            }
            public String[] GetClientServerCommandHandles(ServerCommandDef[] ServerCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    l.AddRange(GetTemplate("JsonClient_ServerCommandHandle").Substitute("Name", c.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandHandles(ClientCommandDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("JsonClient_ClientCommandHandle").Substitute("Name", c.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommandDeques(ClientCommandDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("JsonClient_ClientCommandDeque").Substitute("Name", c.TypeFriendlyName()).Substitute("ClientCommandIndex", k.ToInvariantString()));
                    k += 1;
                }
                return l.ToArray();
            }
            public String[] GetClientClientCommands(ClientCommandDef[] ClientCommands)
            {
                List<String> l = new List<String>();
                var k = 0;
                foreach (var c in ClientCommands)
                {
                    l.AddRange(GetTemplate("JsonClient_ClientCommand").Substitute("Name", c.TypeFriendlyName()).Substitute("ClientCommandIndex", k.ToInvariantString()).Substitute("XmlComment", GetXmlComment(c.Description)));
                    k += 1;
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
            return ActionScript.Common.CodeGenerator.TypeFriendlyName(Type);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return ActionScript.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String LowercaseCamelize(String PascalName)
        {
            return ActionScript.Common.CodeGenerator.LowercaseCamelize(PascalName);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return ActionScript.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
