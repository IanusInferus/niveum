//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构C#重试循环代码生成器
//  Version:     2016.05.13.
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
            var w = new Writer(Schema, NamespaceName, AsyncCommands);
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
                this.Hash = SchemaClosureGenerator.GetSubSchema(Schema.Types.Where(t => (t.OnClientCommand || t.OnServerCommand) && t.Version() == ""), new List<TypeSpec> { }).Hash();

                InnerWriter = new CSharp.Common.CodeGenerator.Writer(Schema, NamespaceName, AsyncCommands, false);

                foreach (var t in Schema.TypeRefs.Concat(Schema.Types))
                {
                    if (!t.GenericParameters().All(gp => gp.Type.OnTypeRef && gp.Type.TypeRef.Name == "Type"))
                    {
                        throw new InvalidOperationException(String.Format("GenericParametersNotAllTypeParameter: {0}", t.VersionedName()));
                    }
                }
            }

            public List<String> GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
            }

            public List<String> GetHeader()
            {
                return GetTemplate("Header");
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public List<String> GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public List<String> GetRetryWrapper(List<TypeDef> Commands)
            {
                return GetTemplate("RetryWrapper").Substitute("ServerCommandHooks", GetRetryWrapperServerCommandHooks(Commands.Where(c => c.OnServerCommand).ToList())).Substitute("Commands", GetRetryWrapperCommands(Commands));
            }
            public List<String> GetRetryWrapperServerCommandHooks(List<TypeDef> ServerCommands)
            {
                var l = new List<String>();
                foreach (var c in ServerCommands)
                {
                    l.AddRange(GetTemplate("RetryWrapper_ServerCommandHook").Substitute("Name", c.TypeFriendlyName()));
                }
                return l;
            }
            public List<String> GetRetryWrapperCommands(List<TypeDef> Commands)
            {
                var l = new List<String>();
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
                return l;
            }

            public List<String> GetComplexTypes()
            {
                var l = new List<String>();

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

                var ocl = Schema.Types.Where(t => t.OnClientCommand || t.OnServerCommand).ToList();
                if (ocl.Count > 0)
                {
                    l.AddRange(GetRetryWrapper(ocl));
                    l.Add("");
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l;
            }

            public List<String> GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static List<String> GetLines(String Value)
            {
                return CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
