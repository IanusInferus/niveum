//==========================================================================
//
//  File:        CppMemory.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C++ Memory代码生成器
//  Version:     2026.06.05.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Mapping.TreeText;
using Firefly.TextEncoding;
using Niveum.RelationSchema;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CppMemory
{
    public static class CodeGenerator
    {
        public static String CompileToCppMemory(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
        {
            var t = new Templates(Schema, EntityNamespaceName, ContextNamespaceName);
            var Lines = t.GetSchema().Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
        }
    }

    public partial class Templates
    {
        private CppPlain.Templates Inner;
        private OS.CppBinary.Templates InnerBinaryWriter;

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

            Inner = new CppPlain.Templates(Schema, EntityNamespaceName);
            var Types = new List<OS.TypeDef>();
            Types.AddRange(InnerSchema.Types);
            var EntityNamespaceParts = EntityNamespaceName.Split('.').ToList();
            var TableFields = Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).Select(
                e => new OS.VariableDef
                {
                    Name = e.Name,
                    Type = OS.TypeSpec.CreateGenericTypeSpec(new OS.GenericTypeSpec
                    {
                        TypeSpec = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = new List<String> { "List" }, Version = "" }),
                        ParameterValues = new List<OS.TypeSpec>
                        {
                            OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = EntityNamespaceParts.Concat(new List<String> { e.Name }).ToList(), Version = "" })
                        }
                    }),
                    Attributes = new List<KeyValuePair<String, List<String>>> { },
                    Description = ""
                }).Cast<OS.VariableDef>().ToList();
            Types.Add(OS.TypeDef.CreateRecord(new OS.RecordDef { Name = EntityNamespaceParts.Concat(new List<String> { "MemoryDataTables" }).ToList(), Version = "", GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Fields = TableFields, Description = "" }));
            InnerSchema = new OS.Schema { Types = Types, TypeRefs = InnerSchema.TypeRefs, Imports = InnerSchema.Imports };
            InnerTypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key.Split('.').Last(), p => p.Value, StringComparer.OrdinalIgnoreCase);
            InnerBinaryWriter = new OS.CppBinary.Templates(InnerSchema, false, false);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public Boolean IsInclude(String s)
        {
            return Inner.IsInclude(s);
        }

        public List<String> GetPrimitives()
        {
            return Inner.GetPrimitives().ToList();
        }

        public String GetTypeString(OS.TypeSpec Type)
        {
            return Inner.GetTypeString(Type);
        }

        public IEnumerable<String> WrapNamespace(String Namespace, IEnumerable<String> Contents)
        {
            return Inner.WrapNamespace(Namespace, Contents);
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

        public String GetIndexType(EntityDef e, List<String> Key, Boolean AsValueType = false)
        {
            var or = InnerTypeDict[e.Name].Record;
            var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
            var l = new LinkedList<String>();
            l.AddLast("std::vector<Int>");
            foreach (var c in Key.AsEnumerable().Reverse())
            {
                l.AddFirst("std::map<" + GetTypeString(d[c].Type) + ", std::shared_ptr<");
                l.AddLast(">>");
            }
            if (!AsValueType)
            {
                l.AddFirst("std::shared_ptr<");
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

        public IEnumerable<String> GetGenerates()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var or = InnerTypeDict[e.Name].Record;
                var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToList()).Distinct(new StringArrayComparer()).ToList();
                foreach (var k in NondirectionalKeys)
                {
                    var IndexName = e.Name + GetByIndex(k);
                    var IndexType = GetIndexType(e, k, true);
                    var Fetches = new List<String>();
                    for (var n = 0; n < k.Count; n += 1)
                    {
                        var ParentByIndex = GetByIndex(k.Take(n));
                        var RemainIndexType = GetIndexType(e, k.Skip(n + 1).ToList(), true);
                        var Column = k[n];
                        var ByIndex = GetByIndex(k.Take(n + 1));
                        Fetches.AddRange(DataAccessBase_Generate_Fetch(ParentByIndex, RemainIndexType, Column, ByIndex));
                    }
                    var Add = DataAccessBase_Generate_Add(GetByIndex(k));
                    l.AddRange(DataAccessBase_Generate(e.Name, IndexName, IndexType, Fetches, Add));
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
        public String GetOrderBy(QueryDef q, String EntityName)
        {
            var l = new List<String>();
            var First = true;
            foreach (var k in q.OrderBy)
            {
                if (First)
                {
                    if (k.IsDescending)
                    {
                        l.Add($@">>orderby_descending([](std::shared_ptr<class {GetEscapedIdentifier(EntityName)}> _e_) {{ return _e_->{GetEscapedIdentifier(k.Name)}; }})");
                    }
                    else
                    {
                        l.Add($@">>orderby([](std::shared_ptr<class {GetEscapedIdentifier(EntityName)}> _e_) {{ return _e_->{GetEscapedIdentifier(k.Name)}; }})");
                    }
                    First = false;
                }
                else
                {
                    if (k.IsDescending)
                    {
                        l.Add($@">>thenby_descending([](std::shared_ptr<class {GetEscapedIdentifier(EntityName)}> _e_) {{ return _e_->{GetEscapedIdentifier(k.Name)}; }})");
                    }
                    else
                    {
                        l.Add($@">>thenby([](std::shared_ptr<class {GetEscapedIdentifier(EntityName)}> _e_) {{ return _e_->{GetEscapedIdentifier(k.Name)}; }})");
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
            var OrderBys = GetOrderBy(q, e.Name);
            IEnumerable<String> Content;
            if (q.Verb.OnSelect || q.Verb.OnLock)
            {
                Func<String, IEnumerable<String>, String, String, IEnumerable<String>> NumberalTemplate;
                List<String> WhenEmpty;
                if (q.Numeral.OnOptional)
                {
                    NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_Optional(ManyName, Parameters, e.Name, IndexName, Fetches, ByIndex, Filters);
                    WhenEmpty = SelectLock_Optional_WhenEmpty(e.Name).ToList();
                }
                else if (q.Numeral.OnOne)
                {
                    NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_One(ManyName, Parameters, e.Name, IndexName, Fetches, ByIndex, Filters);
                    WhenEmpty = SelectLock_One_WhenEmpty(e.Name).ToList();
                }
                else if (q.Numeral.OnMany)
                {
                    NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_Many(ManyName, Parameters, OrderBys, e.Name, IndexName, Fetches, ByIndex, Filters);
                    WhenEmpty = SelectLock_ManyRange_WhenEmpty(e.Name).ToList();
                }
                else if (q.Numeral.OnAll)
                {
                    NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_All(AllName, OrderBys, e.Name);
                    WhenEmpty = null;
                }
                else if (q.Numeral.OnRange)
                {
                    if (q.By.Count == 0)
                    {
                        NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_RangeAll(AllName, OrderBys, e.Name);
                    }
                    else
                    {
                        NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_Range(ManyName, Parameters, OrderBys, e.Name, IndexName, Fetches, ByIndex, Filters);
                    }
                    WhenEmpty = SelectLock_ManyRange_WhenEmpty(e.Name).ToList();
                }
                else if (q.Numeral.OnCount)
                {
                    NumberalTemplate = (IndexName, Fetches, ByIndex, Filters) => SelectLock_Count(ManyName, Parameters, e.Name, IndexName, Fetches, ByIndex, Filters);
                    WhenEmpty = SelectLock_Count_WhenEmpty(e.Name).ToList();
                }
                else
                {
                    throw new InvalidOperationException();
                }

                if (q.Numeral.OnAll)
                {
                    Content = NumberalTemplate(null, null, null, null);
                }
                else
                {
                    var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(kk => kk.Columns.Select(c => c.Name).ToList()).Distinct(new StringArrayComparer()).ToList();
                    var Key = q.By.ToList();
                    List<String> k = null;
                    if (NondirectionalKeys.Contains(Key))
                    {
                        k = Key;
                    }
                    else
                    {
                        foreach (var kk in NondirectionalKeys)
                        {
                            if (kk.Count >= Key.Count && kk.Take(Key.Count).SequenceEqual(Key))
                            {
                                k = kk;
                                break;
                            }
                        }
                        if (k == null) { throw new InvalidOperationException(); }
                    }
                    var IndexName = e.Name + GetByIndex(k);
                    var Fetches = new List<String>();
                    for (var n = 0; n < Key.Count; n += 1)
                    {
                        var ParentByIndex = GetByIndex(Key.Take(n));
                        var Column = GetEscapedIdentifier(k[n]);
                        var ByIndex = GetByIndex(Key.Take(n + 1));
                        Fetches.AddRange(SelectMany_Fetch(e.Name, ParentByIndex, Column, ByIndex, WhenEmpty));
                    }
                    var Filters = new List<String>();
                    for (var n = Key.Count; n < k.Count; n += 1)
                    {
                        Filters.Add(@">>select_many([](auto _d_) { return from(*std::get<1>(_d_)); })");
                    }
                    Content = NumberalTemplate(IndexName, Fetches, GetByIndex(Key), String.Join("", Filters.ToArray()));
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

        public List<String> GetComplexTypes()
        {
            var l = new List<String>();

            var Tables = GetTables();
            var Indices = GetIndices();
            var Streams = InnerBinaryWriter.Streams().ToList();
            var BinaryTranslator = InnerBinaryWriter.BinaryTranslator(InnerSchema, EntityNamespaceName).ToList();
            var Generates = GetGenerates();
            var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            var Queries = GetQueries();
            l.AddRange(DataAccess(Tables, Indices, Streams, BinaryTranslator, Generates, Hash, Queries));
            l.Add("");

            if (l.Count > 0)
            {
                l = l.Take(l.Count - 1).ToList();
            }

            return l;
        }

        public List<String> GetSchema()
        {
            var Includes = Schema.Imports.Where(i => IsInclude(i)).ToList();
            var Primitives = GetPrimitives();
            var ComplexTypes = GetComplexTypes();
            IEnumerable<String> Contents = ComplexTypes;
            if (EntityNamespaceName == NamespaceName || EntityNamespaceName == "")
            {
                Contents = NamespaceImplementation(new List<String> { }, Contents).ToList();
            }
            else
            {
                Contents = NamespaceImplementation(new List<String> { EntityNamespaceName.Replace(".", "::") }, Contents);
            }
            Contents = WrapNamespace(NamespaceName, Contents);
            return Main(Includes, Primitives, Contents).Select(Line => Line.TrimEnd(' ')).ToList();
        }
    }
}
