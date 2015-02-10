//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#重试循环代码生成器
//  Version:     2015.02.10.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;

namespace Yuki.ObjectSchema.CSharpRetry
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpRetry(this Schema Schema, String NamespaceName, HashSet<String> AsyncCommands)
        {
            Writer w = new Writer(Schema, NamespaceName, AsyncCommands);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToCSharpRetry(this Schema Schema)
        {
            return CompileToCSharpRetry(Schema, "", new HashSet<String> { });
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private ISchemaClosureGenerator SchemaClosureGenerator;
            private String NamespaceName;
            private HashSet<String> AsyncCommands;
            private UInt64 Hash;

            static Writer()
            {
                var OriginalTemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharp);
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpRetry);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName, HashSet<String> AsyncCommands)
            {
                this.Schema = Schema;
                this.SchemaClosureGenerator = Schema.GetSchemaClosureGenerator();
                this.NamespaceName = NamespaceName;
                this.AsyncCommands = AsyncCommands;
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new TypeSpec[] { }).Hash();

                InnerWriter = new CSharp.Common.CodeGenerator.Writer(Schema, NamespaceName, AsyncCommands, false);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
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

            public String[] GetRetryWrapper(TypeDef[] Commands)
            {
                return GetTemplate("RetryWrapper").Substitute("ServerCommandHooks", GetRetryWrapperServerCommandHooks(Commands.Where(c => c.OnServerCommand).ToArray())).Substitute("Commands", GetRetryWrapperCommands(Commands));
            }
            public String[] GetRetryWrapperServerCommandHooks(TypeDef[] ServerCommands)
            {
                List<String> l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    l.AddRange(GetTemplate("RetryWrapper_ServerCommandHook").Substitute("Name", c.TypeFriendlyName()));
                }
                return l.ToArray();
            }
            public String[] GetRetryWrapperCommands(TypeDef[] Commands)
            {
                List<String> l = new List<String>();
                foreach (var c in Commands)
                {
                    if (c.OnClientCommand)
                    {
                        if (AsyncCommands.Contains(c.ClientCommand.Name))
                        {
                            l.AddRange(GetTemplate("RetryWrapper_ClientCommandAsyncHook").Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                        }
                        else
                        {
                            l.AddRange(GetTemplate("RetryWrapper_ClientCommandHook").Substitute("Name", c.ClientCommand.TypeFriendlyName()));
                        }
                    }
                    else if (c.OnServerCommand)
                    {
                        l.AddRange(GetTemplate("RetryWrapper_ServerCommand").Substitute("Name", c.ServerCommand.TypeFriendlyName()));
                    }
                }
                return l.ToArray();
            }

            public String[] GetComplexTypes()
            {
                List<String> l = new List<String>();

                List<TypeDef> cl = new List<TypeDef>();

                foreach (var c in Schema.Types)
                {
                    if (c.OnClientCommand)
                    {
                        cl.Add(c);
                    }
                    else if (c.OnServerCommand)
                    {
                        cl.Add(c);
                    }
                }

                var oca = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToArray();
                if (oca.Length > 0)
                {
                    l.AddRange(GetRetryWrapper(oca));
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
            public static String[] GetLines(String Value)
            {
                return CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
