//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C++简单类型代码生成器
//  Version:     2016.08.06.
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
            private static OS.ObjectSchemaTemplateInfo TemplateInfo;

            private OS.Cpp.Common.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String NamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, OS.TypeDef> TypeDict;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Yuki.ObjectSchema.Properties.Resources.Cpp);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CppPlain);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String NamespaceName)
            {
                this.Schema = Schema;
                this.NamespaceName = NamespaceName;
                InnerSchema = PlainObjectSchemaGenerator.Generate(Schema);
                TypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
                InnerWriter = new OS.Cpp.Common.CodeGenerator.Writer(InnerSchema, NamespaceName);

                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
            }

            public List<String> GetSchema()
            {
                var Header = GetHeader();
                var Includes = Schema.Imports.Where(i => IsInclude(i)).ToList();
                var Primitives = GetPrimitives();
                var SimpleTypes = GetSimpleTypes();
                var EnumFunctors = GetEnumFunctors();
                var ComplexTypes = GetComplexTypes();
                return EvaluateEscapedIdentifiers(GetMain(Header, Includes, Primitives, WrapContents(NamespaceName, SimpleTypes), WrapContents("std", EnumFunctors), WrapContents(NamespaceName, ComplexTypes))).Select(Line => Line.TrimEnd(' ')).ToList();
            }

            public List<String> GetMain(List<String> Header, List<String> Includes, List<String> Primitives, List<String> SimpleTypes, List<String> EnumFunctors, List<String> ComplexTypes)
            {
                return InnerWriter.GetMain(Header, Includes, Primitives, SimpleTypes, EnumFunctors, ComplexTypes);
            }

            public List<String> WrapContents(String Namespace, List<String> Contents)
            {
                return InnerWriter.WrapContents(Namespace, Contents);
            }

            public Boolean IsInclude(String s)
            {
                return InnerWriter.IsInclude(s);
            }

            public List<String> GetHeader()
            {
                return InnerWriter.GetHeader();
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(OS.TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
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
                        Type = GetEscapedIdentifier("Optional<std::shared_ptr<class {0}>>".Formats(q.EntityName));
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

            public List<String> GetSimpleTypes()
            {
                return InnerWriter.GetSimpleTypes();
            }

            public List<String> GetEnumFunctors()
            {
                return InnerWriter.GetEnumFunctors();
            }

            public List<String> GetComplexTypes()
            {
                var l = new List<String>();
                l.AddRange(InnerWriter.GetComplexTypes());
                l.Add("");

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

            public List<String> GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static List<String> GetLines(String Value)
            {
                return OS.Cpp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return OS.Cpp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return OS.Cpp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return OS.Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return OS.Cpp.Common.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
