//==========================================================================
//
//  File:        CSharpMemory.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C# Memory代码生成器
//  Version:     2026.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping;
using Firefly.Streaming;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CSharpMemory
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpMemory(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
        {
            var w = new Templates(Schema, EntityNamespaceName, ContextNamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a.Select(Line => Line.TrimEnd(' ')));
        }
    }

    public partial class Templates
    {
        private CSharpPlain.Templates Inner;
        private Schema Schema;
        private String EntityNamespaceName;
        private String NamespaceName;
        private OS.Schema InnerSchema;
        private Dictionary<String, TypeDef> TypeDict;
        private Dictionary<String, OS.TypeDef> InnerTypeDict;

        public Templates(Schema Schema, String EntityNamespaceName, String NamespaceName)
        {
            this.Schema = Schema;
            this.EntityNamespaceName = EntityNamespaceName;
            this.NamespaceName = NamespaceName;
            InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, EntityNamespaceName);
            TypeDict = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            InnerTypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key.Split('.').Last(), p => p.Value, StringComparer.OrdinalIgnoreCase);
            Inner = new CSharpPlain.Templates(Schema, EntityNamespaceName);

            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int64").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int64"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public List<String> GetPrimitives()
        {
            return Inner.GetPrimitives();
        }

        public String GetTypeString(OS.TypeSpec Type)
        {
            return Inner.GetTypeString(Type);
        }

        public IEnumerable<String> GetTables()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                l.AddRange(DataAccessBase_Table(e.Name));
            }
            return l;
        }

        private class StringArrayComparer : IEqualityComparer<List<String>>
        {
            public Boolean Equals(List<String> x, List<String> y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(List<String> obj)
            {
                return obj.Select(o => o.GetHashCode()).Aggregate((a, b) => a ^ b);
            }
        }

        public String GetIndexType(EntityDef e, List<String> Key)
        {
            var or = InnerTypeDict[e.Name].Record;
            var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            var l = new LinkedList<String>();
            l.AddLast("List<Int>");
            foreach (var c in Key.AsEnumerable().Reverse())
            {
                l.AddFirst("SortedDictionary<" + GetTypeString(d[c].Type) + ", ");
                l.AddLast(">");
            }
            return String.Join("", l.ToArray());
        }

        public IEnumerable<String> GetIndices()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToList()).Distinct(new StringArrayComparer()).ToList();
                foreach (var k in NondirectionalKeys)
                {
                    var IndexName = e.Name + GetByIndex(k);
                    var IndexType = GetIndexType(e, k);
                    l.AddRange(DataAccessBase_Index(IndexName, IndexType));
                }
            }
            return l;
        }

        public IEnumerable<String> GetSelects()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                l.AddRange(DataAccessBase_SelectAll(e.Name));

                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var h = new HashSet<List<String>>(new StringArrayComparer());
                var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToList()).Distinct(new StringArrayComparer()).ToList();
                foreach (var k in NondirectionalKeys)
                {
                    for (var j = k.Count; j > 0; j -= 1)
                    {
                        var Key = k.Take(j).ToList();
                        if (h.Contains(Key)) { continue; }
                        h.Add(Key);
                        var Remain = k.Skip(j).ToList();
                        var PartialIndexName = GetByIndex(Key);
                        var IndexName = e.Name + GetByIndex(k);
                        var ParameterDeclarations = String.Join(", ", Key.Select(c => "{0} {1}".Formats(GetEscapedIdentifier(GetTypeString(d[c].Type)), GetEscapedIdentifier(c))).ToArray());
                        var Fetches = new List<String>();
                        for (var n = 0; n < Key.Count; n += 1)
                        {
                            var ParentByIndex = GetByIndex(Key.Take(n));
                            var Column = GetEscapedIdentifier(k[n]);
                            var ByIndex = GetByIndex(Key.Take(n + 1));
                            Fetches.AddRange(DataAccessBase_SelectMany_Fetch(e.Name, ParentByIndex, Column, ByIndex));
                        }
                        var Filters = String.Join("", Remain.Select(c => ".SelectMany(_d_ => _d_.Value)").ToArray());
                        l.AddRange(DataAccessBase_SelectMany(e.Name, PartialIndexName, IndexName, ParameterDeclarations, Fetches, GetByIndex(Key), Filters));
                    }
                }
            }
            return l;
        }

        public IEnumerable<String> GetGenerates()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var rd = e.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToList()).Distinct(new StringArrayComparer()).ToList();
                foreach (var k in NondirectionalKeys)
                {
                    var IndexName = e.Name + GetByIndex(k);
                    var IndexType = GetIndexType(e, k);
                    var Fetches = new List<String>();
                    for (var n = 0; n < k.Count; n += 1)
                    {
                        var ParentByIndex = GetByIndex(k.Take(n));
                        var RemainIndexType = GetIndexType(e, k.Skip(n + 1).ToList());
                        var NextColumnConstructorParameters = "";
                        if (n + 1 < k.Count) { NextColumnConstructorParameters = rd[k[n + 1]].Type.OnOptional ? "new OptionalComparer<" + GetEscapedIdentifier(GetTypeString(d[k[n + 1]].Type.GenericTypeSpec.ParameterValues.Single())) + ">()" : ""; }
                        var Column = k[n];
                        var ByIndex = GetByIndex(k.Take(n + 1));
                        Fetches.AddRange(DataAccessBase_Generate_Fetch(ParentByIndex, RemainIndexType, NextColumnConstructorParameters, Column, ByIndex));
                    }
                    var Add = DataAccessBase_Generate_Add(GetByIndex(k));
                    var FirstColumnConstructorParameters = rd[k[0]].Type.OnOptional ? "new OptionalComparer<" + GetEscapedIdentifier(GetTypeString(d[k[0]].Type.GenericTypeSpec.ParameterValues.Single())) + ">()" : "";
                    l.AddRange(DataAccessBase_Generate(e.Name, IndexName, IndexType, FirstColumnConstructorParameters, Fetches, Add));
                }
            }
            return l;
        }

        public String GetByIndex(IEnumerable<String> KeyColumns)
        {
            var l = KeyColumns.ToList();
            if (l.Count == 0)
            {
                return "";
            }
            else
            {
                return "By" + String.Join("And", l);
            }
        }

        public String GetOrderBy(QueryDef q)
        {
            var l = new List<String>();
            var First = true;
            foreach (var k in q.OrderBy)
            {
                if (First)
                {
                    if (k.IsDescending)
                    {
                        l.Add($".OrderByDescending(_e_ => _e_.{GetEscapedIdentifier(k.Name)})");
                    }
                    else
                    {
                        l.Add($".OrderBy(_e_ => _e_.{GetEscapedIdentifier(k.Name)})");
                    }
                    First = false;
                }
                else
                {
                    if (k.IsDescending)
                    {
                        l.Add($".ThenByDescending(_e_ => _e_.{GetEscapedIdentifier(k.Name)})");
                    }
                    else
                    {
                        l.Add($".ThenBy(_e_ => _e_.{GetEscapedIdentifier(k.Name)})");
                    }
                }
            }
            return String.Join("", l.ToArray());
        }

        public IEnumerable<String> GetQuery(QueryDef q)
        {
            var e = TypeDict[q.EntityName].Entity;

            var Signature = Inner.GetQuerySignature(q);
            var ManyName = (new QueryDef { EntityName = q.EntityName, Verb = q.Verb, Numeral = Numeral.CreateMany(), By = q.By, OrderBy = new List<KeyColumn> { } }).FriendlyName();
            var AllName = (new QueryDef { EntityName = q.EntityName, Verb = q.Verb, Numeral = Numeral.CreateAll(), By = q.By, OrderBy = new List<KeyColumn> { } }).FriendlyName();
            var Parameters = String.Join(", ", q.By.Select(c => GetEscapedIdentifier(c)).ToArray());
            var OrderBys = GetOrderBy(q);
            IEnumerable<String> Content;
            if (q.Verb.OnSelect || q.Verb.OnLock)
            {
                if (q.Numeral.OnOptional)
                {
                    Content = SelectLock_Optional(ManyName, Parameters);
                }
                else if (q.Numeral.OnOne)
                {
                    Content = SelectLock_One(ManyName, Parameters);
                }
                else if (q.Numeral.OnMany)
                {
                    Content = SelectLock_Many(ManyName, Parameters, OrderBys);
                }
                else if (q.Numeral.OnAll)
                {
                    Content = SelectLock_All(AllName, OrderBys);
                }
                else if (q.Numeral.OnRange)
                {
                    if (q.By.Count == 0)
                    {
                        Content = SelectLock_RangeAll(AllName, OrderBys);
                    }
                    else
                    {
                        Content = SelectLock_Range(ManyName, Parameters, OrderBys);
                    }
                }
                else if (q.Numeral.OnCount)
                {
                    Content = SelectLock_Count(ManyName, Parameters);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert || q.Verb.OnDelete)
            {
                Content = InsertUpdateUpsertDelete();
            }
            else
            {
                throw new InvalidOperationException();
            }
            return Query(Signature, Content);
        }

        public IEnumerable<String> GetQueries()
        {
            var l = new List<String>();
            foreach (var q in Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries))
            {
                l.AddRange(GetQuery(q));
                l.Add("");
            }
            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }
            return l;
        }

        public IEnumerable<String> GetComplexTypes()
        {
            var l = new List<String>();

            var Tables = GetTables();
            var Indices = GetIndices();
            var Selects = GetSelects();
            var Generates = GetGenerates();
            var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            l.AddRange(DataAccessBase(Tables, Indices, Selects, Generates, Hash));
            l.Add("");

            var Queries = GetQueries();
            l.AddRange(DataAccess(Queries));
            l.Add("");
            l.AddRange(DataAccessPool(Hash));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public IEnumerable<String> GetSchema()
        {
            var Primitives = GetPrimitives();
            var ComplexTypes = GetComplexTypes();

            return Main(NamespaceName, Schema.Imports, Primitives, ComplexTypes);
        }
    }
}
