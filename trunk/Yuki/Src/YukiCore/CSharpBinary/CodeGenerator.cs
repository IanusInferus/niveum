//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#二进制通讯代码生成器
//  Version:     2012.04.21.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.CSharpBinary
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpBinary(this Schema Schema, String NamespaceName)
        {
            var s = Schema;
            var h = s.Hash();
            Writer w = new Writer() { Schema = s, NamespaceName = NamespaceName, Hash = h };
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpBinary(this Schema Schema)
        {
            return CompileToCSharpBinary(Schema, "");
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            public Schema Schema;
            public String NamespaceName;
            public UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpBinary);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public String[] GetSchema()
            {
                InnerWriter = new CSharp.Common.CodeGenerator.Writer { Schema = Schema, NamespaceName = NamespaceName };

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

            public String[] GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public String[] GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public String[] GetBinaryServer(CommandDef[] Commands)
            {
                return GetTemplate("BinaryServer").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("Commands", GetBinaryServerCommands(Commands));
            }
            public String[] GetBinaryServerCommands(CommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandDefTag.Client)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { TypeDef.CreateClientCommand(c.Client) }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinaryServer_ClientCommand").Substitute("CommandName", c.Client.Name).Substitute("Name", c.Client.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (c._Tag == CommandDefTag.Server)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { TypeDef.CreateServerCommand(c.Server) }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinaryServer_ServerCommand").Substitute("CommandName", c.Server.Name).Substitute("Name", c.Server.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetBinaryClient(CommandDef[] Commands)
            {
                return GetTemplate("BinaryClient").Substitute("Hash", Hash.ToString("X16", System.Globalization.CultureInfo.InvariantCulture)).Substitute("ClientCommands", GetBinaryClientClientCommands(Commands)).Substitute("ServerCommands", GetBinaryClientServerCommands(Commands));
            }
            public String[] GetBinaryClientClientCommands(CommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandDefTag.Client)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { TypeDef.CreateClientCommand(c.Client) }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinaryClient_ClientCommand").Substitute("CommandName", c.Client.Name).Substitute("Name", c.Client.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)).Substitute("XmlComment", GetXmlComment(c.Client.Description)));
                    }
                }
                return l.ToArray();
            }
            public String[] GetBinaryClientServerCommands(CommandDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c._Tag == CommandDefTag.Server)
                    {
                        var CommandHash = (UInt32)(Schema.GetSubSchema(new TypeDef[] { TypeDef.CreateServerCommand(c.Server) }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                        l.AddRange(GetTemplate("BinaryClient_ServerCommand").Substitute("CommandName", c.Server.Name).Substitute("Name", c.Server.TypeFriendlyName()).Substitute("CommandHash", CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return l.ToArray();
            }

            public String[] GetBinaryTranslator(TypeDef[] Types)
            {
                return GetTemplate("BinaryTranslator");
            }

            public String[] GetComplexTypes(Schema Schema)
            {
                List<String> l = new List<String>();

                List<CommandDef> cl = new List<CommandDef>();

                foreach (var c in Schema.Types)
                {
                    if (c.OnClientCommand)
                    {
                        cl.Add(CommandDef.CreateClient(c.ClientCommand));
                    }
                    else if (c.OnServerCommand)
                    {
                        cl.Add(CommandDef.CreateServer(c.ServerCommand));
                    }
                }

                if (cl.Count > 0)
                {
                    var ca = cl.ToArray();

                    l.AddRange(GetBinaryServer(ca));
                    l.Add("");
                    l.AddRange(GetTemplate("IBinarySender"));
                    l.Add("");
                    l.AddRange(GetBinaryClient(ca));
                    l.Add("");
                }

                l.AddRange(GetBinaryTranslator(Schema.TypeRefs.Concat(Schema.Types).ToArray()));
                l.Add("");

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
                return InnerWriter.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return InnerWriter.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String LowercaseCamelize(String PascalName)
        {
            return CSharp.Common.CodeGenerator.LowercaseCamelize(PascalName);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
