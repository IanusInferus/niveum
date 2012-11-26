﻿//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C#简单类型代码生成器
//  Version:     2012.11.26.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.MetaSchema;
using Firefly.TextEncoding;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.CSharpPlain
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpPlain(this Schema Schema, String NamespaceName)
        {
            Writer w = new Writer(Schema, NamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        public class Writer
        {
            private static OS.ObjectSchemaTemplateInfo TemplateInfo;

            private OS.CSharp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String NamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, OS.TypeDef> TypeDict;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Yuki.ObjectSchema.Properties.Resources.CSharp);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.NamespaceName = NamespaceName;
                InnerSchema = PlainObjectSchemaGenerator.Generate(Schema);
                TypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            }

            public String[] GetSchema()
            {
                InnerWriter = new OS.CSharp.Common.CodeGenerator.Writer(InnerSchema, NamespaceName);

                var Header = InnerWriter.GetHeader();
                var Primitives = InnerWriter.GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(InnerWriter.GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes));
                }
                else
                {
                    return EvaluateEscapedIdentifiers(InnerWriter.GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes));
                }
            }

            public String GetTypeString(OS.TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }
            public String[] GetXmlComment(String Description)
            {
                return InnerWriter.GetXmlComment(Description);
            }

            public String[] GetQuerySignature(QueryDef q)
            {
                var or = TypeDict[q.EntityName].Record;
                var Name = q.FriendlyName();
                var pl = new List<String>();
                if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    if (q.Numeral.OnOne)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier(q.EntityName), GetEscapedIdentifier("v")));
                    }
                    else if (q.Numeral.OnMany)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier("List<{0}>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                pl.AddRange(q.By.Select(c => "{0} {1}".Formats(GetEscapedIdentifier(GetTypeString(or.Fields.Where(f => f.Name == c).Single().Type)), GetEscapedIdentifier(c))).ToArray());
                if (q.Numeral.OnRange)
                {
                    pl.Add("Int Skip");
                    pl.Add("Int Take");
                }
                var ParameterList = String.Join(",", pl.ToArray());
                String Type;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    if (q.Numeral.OnOptional)
                    {
                        Type = GetEscapedIdentifier("Optional<{0}>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Type = GetEscapedIdentifier(q.EntityName);
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Type = GetEscapedIdentifier("List<{0}>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnAll)
                    {
                        Type = GetEscapedIdentifier("List<{0}>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnRange)
                    {
                        Type = GetEscapedIdentifier("List<{0}>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnCount)
                    {
                        Type = GetEscapedIdentifier("Int");
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    Type = "void";
                }
                return GetTemplate("QuerySignature").Substitute("Name", Name).Substitute("ParameterList", ParameterList).Substitute("Type", Type);
            }

            public String[] GetComplexTypes()
            {
                List<String> l = new List<String>();
                l.AddRange(InnerWriter.GetComplexTypes(InnerSchema));
                l.Add("");

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToArray();
                if (Queries.Length > 0)
                {
                    l.AddRange(GetTemplate("IRegularDataAccess").Substitute("Queries", Queries.SelectMany(q => GetQuerySignature(q)).ToArray()));
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
            return OS.CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            return OS.CSharp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
