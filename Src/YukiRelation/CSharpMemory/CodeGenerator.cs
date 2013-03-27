//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# Memory代码生成器
//  Version:     2013.03.27.
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

namespace Yuki.RelationSchema.CSharpMemory
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpMemory(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
        {
            Writer w = new Writer(Schema, EntityNamespaceName, ContextNamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        public class Writer
        {
            private static OS.ObjectSchemaTemplateInfo TemplateInfo;

            private CSharpPlain.CodeGenerator.Writer InnerWriter;

            private Schema Schema;
            private String EntityNamespaceName;
            private String NamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, TypeDef> TypeDict;
            private Dictionary<String, OS.TypeDef> InnerTypeDict;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpMemory);
                TemplateInfo.Keywords = OriginalTemplateInfo.Keywords;
                TemplateInfo.PrimitiveMappings = OriginalTemplateInfo.PrimitiveMappings;
            }

            public Writer(Schema Schema, String EntityNamespaceName, String NamespaceName)
            {
                this.Schema = Schema;
                this.EntityNamespaceName = EntityNamespaceName;
                this.NamespaceName = NamespaceName;
                InnerSchema = PlainObjectSchemaGenerator.Generate(Schema);
                TypeDict = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
                InnerTypeDict = Yuki.ObjectSchema.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            }

            public String[] GetSchema()
            {
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

                InnerWriter = new CSharpPlain.CodeGenerator.Writer(Schema, NamespaceName);

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
                if (EntityNamespaceName == NamespaceName || EntityNamespaceName == "")
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", new String[] { });
                }
                else
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", EntityNamespaceName);
                }
            }

            public String[] GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(OS.TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }

            public String[] GetTables()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    l.AddRange(GetTemplate("DataAccessBase_Table").Substitute("EntityName", e.Name));
                }
                return l.ToArray();
            }

            private class StringArrayComparer : IEqualityComparer<String[]>
            {
                public Boolean Equals(String[] x, String[] y)
                {
                    return x.SequenceEqual(y);
                }

                public int GetHashCode(String[] obj)
                {
                    return obj.Select(o => o.GetHashCode()).Aggregate((a, b) => a ^ b);
                }
            }

            public String GetIndexType(EntityDef e, String[] Key)
            {
                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var l = new LinkedList<String>();
                l.AddLast("List<Int>");
                foreach (var c in Key.Reverse())
                {
                    l.AddFirst("SortedDictionary<" + GetTypeString(d[c].Type) + ", ");
                    l.AddLast(">");
                }
                return String.Join("", l.ToArray());
            }

            public String[] GetIndices()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToArray()).Distinct(new StringArrayComparer()).ToArray();
                    foreach (var k in NondirectionalKeys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k);
                        var IndexType = GetIndexType(e, k);
                        l.AddRange(GetTemplate("DataAccessBase_Index").Substitute("IndexName", IndexName).Substitute("IndexType", IndexType));
                    }
                }
                return l.ToArray();
            }

            public String[] GetSelects()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    l.AddRange(GetTemplate("DataAccessBase_SelectAll").Substitute("EntityName", e.Name));

                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var h = new HashSet<String[]>(new StringArrayComparer());
                    var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToArray()).Distinct(new StringArrayComparer()).ToArray();
                    foreach (var k in NondirectionalKeys)
                    {
                        for (var j = k.Length; j > 0; j -= 1)
                        {
                            var Key = k.Take(j).ToArray();
                            if (h.Contains(Key)) { continue; }
                            h.Add(Key);
                            var Remain = k.Skip(j).ToArray();
                            var PartialIndexName = e.Name + "By" + String.Join("And", Key);
                            var IndexName = e.Name + "By" + String.Join("And", k);
                            var ParameterDeclarations = String.Join(", ", Key.Select(c => "{0} {1}".Formats(GetEscapedIdentifier(GetTypeString(d[c].Type)), GetEscapedIdentifier(c))).ToArray());
                            var Fetches = new List<String>();
                            for (var n = 0; n < Key.Length; n += 1)
                            {
                                var ParentByIndex = "";
                                if (n > 0)
                                {
                                    ParentByIndex = "By" + String.Join("And", Key.Take(n).ToArray());
                                }
                                var Column = GetEscapedIdentifier(k[n]);
                                var ByIndex = "By" + String.Join("And", Key.Take(n + 1).ToArray());
                                Fetches.AddRange(GetTemplate("DataAccessBase_SelectMany_Fetch").Substitute("EntityName", e.Name).Substitute("ParentByIndex", ParentByIndex).Substitute("Column", Column).Substitute("ByIndex", ByIndex));
                            }
                            var Filters = String.Join("", Remain.Select(c => ".SelectMany(_d_ => _d_.Value)").ToArray());
                            l.AddRange(GetTemplate("DataAccessBase_SelectMany").Substitute("EntityName", e.Name).Substitute("PartialIndexName", PartialIndexName).Substitute("IndexName", IndexName).Substitute("ParameterDeclarations", ParameterDeclarations).Substitute("Fetches", Fetches.ToArray()).Substitute("ByIndex", "By" + String.Join("And", Key.ToArray())).Substitute("Filters", Filters));
                        }
                    }
                }
                return l.ToArray();
            }

            public String[] GetGenerates()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var rd = e.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToArray()).Distinct(new StringArrayComparer()).ToArray();
                    foreach (var k in NondirectionalKeys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k);
                        var IndexType = GetIndexType(e, k);
                        var Fetches = new List<String>();
                        for (var n = 0; n < k.Length; n += 1)
                        {
                            var ParentByIndex = "";
                            if (n > 0)
                            {
                                ParentByIndex = "By" + String.Join("And", k.Take(n).ToArray());
                            }
                            var RemainIndexType = GetIndexType(e, k.Skip(n + 1).ToArray());
                            var NextColumnConstructorParameters = "";
                            if (n + 1 < k.Length) { NextColumnConstructorParameters = rd[k[n + 1]].Type.OnOptional ? "new OptionalComparer<" + GetEscapedIdentifier(GetTypeString(d[k[n + 1]].Type.GenericTypeSpec.GenericParameterValues.Single().TypeSpec)) + ">()" : ""; }
                            var Column = k[n];
                            var ByIndex = "By" + String.Join("And", k.Take(n + 1).ToArray());
                            Fetches.AddRange(GetTemplate("DataAccessBase_Generate_Fetch").Substitute("ParentByIndex", ParentByIndex).Substitute("RemainIndexType", RemainIndexType).Substitute("NextColumnConstructorParameters", NextColumnConstructorParameters).Substitute("Column", Column).Substitute("ByIndex", ByIndex));
                        }
                        var Add = GetTemplate("DataAccessBase_Generate_Add").Substitute("ByIndex", "By" + String.Join("And", k));
                        var FirstColumnConstructorParameters = rd[k[0]].Type.OnOptional ? "new OptionalComparer<" + GetEscapedIdentifier(GetTypeString(d[k[0]].Type.GenericTypeSpec.GenericParameterValues.Single().TypeSpec)) + ">()" : "";
                        l.AddRange(GetTemplate("DataAccessBase_Generate").Substitute("EntityName", e.Name).Substitute("IndexName", IndexName).Substitute("IndexType", IndexType).Substitute("FirstColumnConstructorParameters", FirstColumnConstructorParameters).Substitute("Fetches", Fetches.ToArray()).Substitute("Add", Add));
                    }
                }
                return l.ToArray();
            }

            public static String GetOrderBy(QueryDef q)
            {
                var l = new List<String>();
                var First = true;
                foreach (var k in q.OrderBy)
                {
                    if (First)
                    {
                        if (k.IsDescending)
                        {
                            l.Add(".OrderByDescending(_e_ => _e_.[[{0}]])".Formats(k.Name));
                        }
                        else
                        {
                            l.Add(".OrderBy(_e_ => _e_.[[{0}]])".Formats(k.Name));
                        }
                        First = false;
                    }
                    else
                    {
                        if (k.IsDescending)
                        {
                            l.Add(".ThenByDescending(_e_ => _e_.[[{0}]])".Formats(k.Name));
                        }
                        else
                        {
                            l.Add(".ThenBy(_e_ => _e_.[[{0}]])".Formats(k.Name));
                        }
                    }
                }
                return String.Join("", l.ToArray());
            }

            public String[] GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;

                var Signature = InnerWriter.GetQuerySignature(q);
                var ManyName = (new QueryDef { Verb = q.Verb, Numeral = Numeral.CreateMany(), EntityName = q.EntityName, By = q.By, OrderBy = new KeyColumn[] { } }).FriendlyName();
                var Parameters = String.Join(", ", q.By);
                var OrderBys = GetOrderBy(q);
                String[] Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    if (q.Numeral.OnOptional)
                    {
                        Content = GetTemplate("SelectLock_Optional").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters);
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Content = GetTemplate("SelectLock_One").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters);
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Content = GetTemplate("SelectLock_Many").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters).Substitute("OrderBys", OrderBys);
                    }
                    else if (q.Numeral.OnAll)
                    {
                        var AllName = (new QueryDef { Verb = q.Verb, Numeral = q.Numeral, EntityName = q.EntityName, By = q.By, OrderBy = new KeyColumn[] { } }).FriendlyName();
                        Content = GetTemplate("SelectLock_All").Substitute("AllName", AllName).Substitute("OrderBys", OrderBys);
                    }
                    else if (q.Numeral.OnRange)
                    {
                        Content = GetTemplate("SelectLock_Range").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters).Substitute("OrderBys", OrderBys);
                    }
                    else if (q.Numeral.OnCount)
                    {
                        Content = GetTemplate("SelectLock_Count").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert || q.Verb.OnDelete)
                {
                    Content = GetTemplate("InsertUpdateUpsertDelete");
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return GetTemplate("Query").Substitute("Signature", Signature).Substitute("Content", Content);
            }

            public String[] GetComplexTypes()
            {
                var l = new List<String>();

                var Tables = GetTables();
                var Indices = GetIndices();
                var Selects = GetSelects();
                var Generates = GetGenerates();
                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                l.AddRange(GetTemplate("DataAccessBase").Substitute("Tables", Tables).Substitute("Indices", Indices).Substitute("Selects", Selects).Substitute("Generates", Generates).Substitute("Hash", Hash));
                l.Add("");

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToArray();
                var ql = new List<String>();
                foreach (var q in Queries)
                {
                    ql.AddRange(GetQuery(q));
                    ql.Add("");
                }
                if (ql.Count > 0)
                {
                    ql = ql.Take(ql.Count - 1).ToList();
                }
                l.AddRange(GetTemplate("DataAccess").Substitute("Queries", ql.ToArray()));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessPool"));
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
            public String[] EvaluateEscapedIdentifiers(String[] Lines)
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
