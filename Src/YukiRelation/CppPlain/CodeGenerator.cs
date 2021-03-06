﻿//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C++简单类型代码生成器
//  Version:     2019.04.28.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;
using OS = Niveum.ObjectSchema;
using ObjectSchemaTemplateInfo = Yuki.ObjectSchema.ObjectSchemaTemplateInfo;

namespace Yuki.RelationSchema.CppPlain
{
    public static class CodeGenerator
    {
        public static String CompileToCppPlain(this Schema Schema, String NamespaceName)
        {
            var w = new Writer(Schema, NamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        public class Writer
        {
            private static ObjectSchemaTemplateInfo TemplateInfo;

            private OS.Cpp.Templates InnerWriter;

            private Schema Schema;
            private String NamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, OS.TypeDef> TypeDict;

            static Writer()
            {
                TemplateInfo = ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CppPlain);
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.NamespaceName = NamespaceName;
                InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, NamespaceName);
                TypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key.Split('.').Last(), p => p.Value, StringComparer.OrdinalIgnoreCase);
                InnerWriter = new OS.Cpp.Templates(InnerSchema);

                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
            }

            public List<String> GetSchema()
            {
                var Includes = Schema.Imports.Where(i => IsInclude(i)).ToList();
                var Types = GetTypes();
                return EvaluateEscapedIdentifiers(GetMain(Includes, Types)).Select(Line => Line.TrimEnd(' ')).ToList();
            }

            public List<String> GetMain(IEnumerable<String> Includes, IEnumerable<String> Types)
            {
                return GetTemplate("Main").Substitute("Includes", Includes).Substitute("Types", Types);
            }

            public IEnumerable<String> WrapNamespace(String Namespace, IEnumerable<String> Contents)
            {
                return InnerWriter.WrapNamespace(Namespace, Contents);
            }

            public Boolean IsInclude(String s)
            {
                return InnerWriter.IsInclude(s);
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives(InnerSchema);
            }

            public String GetTypeString(OS.TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type, NamespaceName);
            }

            public String GetQuerySignature(QueryDef q)
            {
                var or = TypeDict[q.EntityName].Record;
                var Name = q.FriendlyName();
                var pl = new List<String>();
                if (q.Verb.OnInsert || q.Verb.OnUpdate)
                {
                    if (q.Numeral.OnOptional)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName)), GetEscapedIdentifier("v")));
                    }
                    else if (q.Numeral.OnOne)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName)), GetEscapedIdentifier("v")));
                    }
                    else if (q.Numeral.OnMany)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (q.Verb.OnUpsert)
                {
                    if (q.Numeral.OnOne)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName)), GetEscapedIdentifier("v")));
                    }
                    else if (q.Numeral.OnMany)
                    {
                        pl.Add("{0} {1}".Formats(GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName)), GetEscapedIdentifier("l")));
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                pl.AddRange(q.By.Select(c => "{0} {1}".Formats(GetEscapedIdentifier(GetTypeString(or.Fields.Where(f => f.Name == c).Single().Type)), GetEscapedIdentifier(c))).ToArray());
                if (q.Numeral.OnRange)
                {
                    pl.Add("Int _Skip_");
                    pl.Add("Int _Take_");
                }
                var ParameterList = String.Join(", ", pl.ToArray());
                String Type;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    if (q.Numeral.OnOptional)
                    {
                        Type = GetEscapedIdentifier("std::optional<std::shared_ptr<class {0}>>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Type = GetEscapedIdentifier("std::shared_ptr<class {0}>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Type = GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnAll)
                    {
                        Type = GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName));
                    }
                    else if (q.Numeral.OnRange)
                    {
                        Type = GetEscapedIdentifier("std::shared_ptr<std::vector<std::shared_ptr<class {0}>>>".Formats(q.EntityName));
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
                return GetTemplate("QuerySignature").Substitute("Name", Name).Substitute("ParameterList", ParameterList).Substitute("Type", Type).Single();
            }

            public List<String> GetDataAccessTypes()
            {
                var l = new List<String>();

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();
                if (Queries.Count > 0)
                {
                    l.AddRange(GetTemplate("IDataAccess").Substitute("Queries", Queries.Select(q => GetQuerySignature(q)).ToList()));
                    l.Add("");
                    l.AddRange(GetTemplate("IDataAccessPool"));
                    l.Add("");
                }

                if (l.Count > 0)
                {
                    l = l.Take(l.Count - 1).ToList();
                }

                return l;
            }

            public List<String> GetTypes()
            {
                var l = new List<String>();
                l.AddRange(InnerWriter.GetTypes(InnerSchema, NamespaceName));
                l.Add("");

                l.AddRange(WrapNamespace(NamespaceName, GetDataAccessTypes()));
                l.Add("");

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
            public List<String> GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n').ToList();
            }
            public String GetEscapedIdentifier(String Identifier)
            {
                return InnerWriter.GetEscapedIdentifier(Identifier);
            }
            private Regex rIdentifier = new Regex(@"(?<!\[\[)\[\[(?<Identifier>.*?)\]\](?!\]\])", RegexOptions.ExplicitCapture);
            public List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return Lines.Select(Line => rIdentifier.Replace(Line, s => GetEscapedIdentifier(s.Result("${Identifier}"))).Replace("[[[[", "[[").Replace("]]]]", "]]")).ToList();
            }
        }

        public static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + LowercaseCamelize(Parameter) + "}";
            var LowercaseValue = LowercaseCamelize(Value);

            var l = new List<String>();
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

                l.Add(NewLine);
            }
            return l;
        }
        public static String LowercaseCamelize(String PascalName)
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
        public static List<String> Substitute(this List<String> Lines, String Parameter, IEnumerable<String> Value)
        {
            var l = new List<String>();
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
            return l;
        }
    }
}
