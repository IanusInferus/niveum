//==========================================================================
//
//  File:        CSharpKrustallos.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C# Krustallos代码生成器
//  Version:     2026.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CSharpKrustallos
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpKrustallos(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
        {
            var t = new Templates(Schema, EntityNamespaceName, ContextNamespaceName);
            var Lines = t.GetSchema().Select(Line => Line.TrimEnd(' '));
            return String.Join("\r\n", Lines);
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
        private Dictionary<String, Key[]> KeysDict;
        private Dictionary<QueryDef, Key> QueryToSearchKey;
        private Dictionary<Key, Boolean> KeyCanBePartitioned;

        public Templates(Schema Schema, String EntityNamespaceName, String NamespaceName)
        {
            this.Schema = Schema;
            this.EntityNamespaceName = EntityNamespaceName;
            this.NamespaceName = NamespaceName;
            InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, EntityNamespaceName);
            TypeDict = Schema.GetMap().ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            InnerTypeDict = OS.ObjectSchemaExtensions.GetMap(InnerSchema).ToDictionary(p => p.Key.Split('.').Last(), p => p.Value, StringComparer.OrdinalIgnoreCase);

            KeysDict = new Dictionary<String, Key[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys.Select(k => ConvertNonUniqueKeyToUniqueKey(k, e.PrimaryKey))).Select(k => new Key { Columns = k.Columns, IsClustered = false }).Distinct(new KeyComparer()).ToArray();
                KeysDict.Add(e.Name, Keys);
            }

            var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();
            QueryToSearchKey = new Dictionary<QueryDef, Key>();
            foreach (var q in Queries)
            {
                var e = TypeDict[q.EntityName].Entity;

                var By = q.By;
                if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    By = e.PrimaryKey.Columns.Select(c => c.Name).ToList();
                }
                var ByColumns = new HashSet<String>(By, StringComparer.OrdinalIgnoreCase);
                var ActualOrderBy = q.OrderBy.Where(c => !ByColumns.Contains(c.Name)).ToList();
                Key SearchKey = null;
                foreach (var k in KeysDict[e.Name])
                {
                    if (k.Columns.Count < By.Count + ActualOrderBy.Count) { continue; }
                    if (!k.Columns.Take(By.Count).Zip(By, (Left, Right) => Left.Name.Equals(Right, StringComparison.OrdinalIgnoreCase)).Any(f => !f))
                    {
                        if (!k.Columns.Skip(By.Count).Take(ActualOrderBy.Count).Zip(ActualOrderBy, (Left, Right) => Left.Name.Equals(Right.Name) && (Left.IsDescending == Right.IsDescending)).Any(f => !f))
                        {
                            SearchKey = k;
                            break;
                        }
                    }
                }
                if (SearchKey == null)
                {
                    throw new InvalidOperationException();
                }
                QueryToSearchKey.Add(q, SearchKey);
            }

            KeyCanBePartitioned = new Dictionary<Key, Boolean>();
            var EntityNameToQueries = Queries.GroupBy(q => q.EntityName).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var Keys = KeysDict[e.Name];
                var KeyQueries = (EntityNameToQueries.ContainsKey(e.Name) ? EntityNameToQueries[e.Name] : new List<QueryDef>()).GroupBy(q => QueryToSearchKey[q]).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var k in Keys)
                {
                    var CanBePartitioned = true;
                    if (KeyQueries.ContainsKey(k))
                    {
                        foreach (var q in KeyQueries[k])
                        {
                            if (q.Verb.OnSelect || q.Verb.OnLock || q.Verb.OnDelete)
                            {
                                if ((q.By.Count == 0) && (q.OrderBy.Count != 0))
                                {
                                    CanBePartitioned = false;
                                    break;
                                }
                            }
                        }
                    }
                    var FirstColumnType = d[k.Columns.First().Name].Type;
                    if (!FirstColumnType.OnTypeRef || !FirstColumnType.TypeRef.Name.Equals("Int"))
                    {
                        CanBePartitioned = false;
                    }
                    KeyCanBePartitioned.Add(k, CanBePartitioned);
                }
            }

            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int64").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int64"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
            if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

            Inner = new CSharpPlain.Templates(Schema, EntityNamespaceName);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Inner.GetEscapedIdentifier(Identifier);
        }

        public IEnumerable<String> GetPrimitives()
        {
            return Inner.GetPrimitives();
        }

        public String GetTypeString(OS.TypeSpec Type)
        {
            return Inner.GetTypeString(Type);
        }

        public Key ConvertNonUniqueKeyToUniqueKey(Key NonUniqueKey, Key PrimaryKey)
        {
            return new Key { Columns = NonUniqueKey.Columns.Concat(PrimaryKey.Columns.Select(c => c.Name).Except(NonUniqueKey.Columns.Select(c => c.Name)).Select(Name => new KeyColumn { Name = Name, IsDescending = false })).ToList(), IsClustered = NonUniqueKey.IsClustered };
        }

        public IEnumerable<String> GetIndexAndSequenceDefinitions()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var Keys = KeysDict[e.Name];
                foreach (var k in Keys)
                {
                    var IndexName = e.Name + GetByIndex(k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var IndexType = "ImmutableSortedDictionary<Key, " + e.Name + ">";
                    l.AddRange(Data_IndexDefinition(IndexName, IndexType));
                }
                foreach (var f in e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity))
                {
                    var SequenceName = "SequenceOf" + e.Name + "Dot" + f.Name;
                    var SequenceType = "Sequence" + OS.ObjectSchemaExtensions.SimpleName(d[f.Name].Type, NamespaceName);
                    l.AddRange(Data_SequenceDefinition(SequenceName, SequenceType));
                }
            }
            return l;
        }

        public IEnumerable<String> GetIndexAndSequenceInitializations(List<QueryDef> Queries_)
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var Keys = KeysDict[e.Name];
                foreach (var k in Keys)
                {
                    var Index = String.Join(", ", (new List<String> { e.Name }).Concat(k.Columns.Select(c => c.IsDescending ? c.Name + "-" : c.Name)).Select(v => @"""{0}""".Formats(v)));
                    var IndexName = e.Name + GetByIndex(k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var IndexType = "ImmutableSortedDictionary<Key, " + e.Name + ">";
                    var KeyComparer = "new KeyComparer({0})".Formats(String.Join(", ", k.Columns.Select(c => "ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<{0}>({1}))".Formats(GetTypeString(d[c.Name].Type), c.IsDescending ? "true" : "false"))));
                    var CanBePartitioned = KeyCanBePartitioned[k];
                    var NumPartition = CanBePartitioned ? "NumPartition" : "1";
                    l.AddRange(Data_IndexInitialization(Index, IndexName, IndexType, KeyComparer, NumPartition));
                }
                foreach (var f in e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity))
                {
                    var SequenceName = "SequenceOf" + e.Name + "Dot" + f.Name;
                    var SequenceType = "Sequence" + OS.ObjectSchemaExtensions.SimpleName(d[f.Name].Type, NamespaceName);
                    l.AddRange(Data_SequenceInitialization(SequenceName, SequenceType));
                }
            }
            return l;
        }

        public IEnumerable<String> GetClones()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var FieldClones = new List<String>();
                foreach (var f in e.Fields.Where(ff => ff.Attribute.OnColumn))
                {
                    if (f.Type.OnOptional && f.Type.Optional.Value.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        FieldClones.AddRange(DataAccessClone_OptionalBinaryField(f.Name));
                    }
                    else if (f.Type.OnTypeRef && f.Type.TypeRef.Value.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        FieldClones.AddRange(DataAccessClone_BinaryField(f.Name));
                    }
                    else
                    {
                        FieldClones.AddRange(DataAccessClone_Field(f.Name));
                    }
                }
                l.AddRange(DataAccessClone(e.Name, FieldClones));
            }
            return l;
        }

        public String GetFilters(QueryDef q, int OuterByCount, bool IsV = false)
        {
            var l = new List<String>();
            if (q.By.Count > 0)
            {
                var Lower = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => (IsV ? "v." : "") + GetEscapedIdentifier(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Min"))));
                var Upper = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => (IsV ? "v." : "") + GetEscapedIdentifier(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Max"))));
                if (q.Numeral.OnCount)
                {
                    l.Add(".RangeCount({0}, {1})".Formats(Lower, Upper));
                }
                else if (q.Numeral.OnRange)
                {
                    l.Add(".Range({0}, {1}, _Skip_, _Take_)".Formats(Lower, Upper));
                }
                else
                {
                    l.Add(".Range({0}, {1})".Formats(Lower, Upper));
                }
            }
            else
            {
                if (q.Numeral.OnCount)
                {
                    l.Add(".Count");
                }
                else if (q.Numeral.OnRange)
                {
                    l.Add(".RangeByIndex(_Skip_, _Skip_ + _Take_ - 1)");
                }
            }
            if (!q.Numeral.OnCount)
            {
                l.Add(".Select(_p_ => _p_.Value)");
            }
            return String.Join("", l.ToArray());
        }

        public static String GetByIndex(IEnumerable<String> KeyColumns)
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

        public IEnumerable<String> GetQuery(QueryDef q)
        {
            var e = TypeDict[q.EntityName].Entity;

            var Signature = Inner.GetQuerySignature(q);
            var By = q.By;
            var IsV = false;
            if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
            {
                By = e.PrimaryKey.Columns.Select(c => c.Name).ToList();
                IsV = true;
            }
            var ByColumns = new HashSet<String>(By, StringComparer.OrdinalIgnoreCase);
            var ActualOrderBy = q.OrderBy.Where(c => !ByColumns.Contains(c.Name)).ToList();
            var SearchKey = QueryToSearchKey[q];
            var Parameters = String.Join(", ", By.Select(c => (IsV ? "v." + GetEscapedIdentifier(c) : GetEscapedIdentifier(c))).ToArray());
            IEnumerable<String> Content;
            if (q.Verb.OnSelect || q.Verb.OnLock)
            {
                var CanBePartitioned = KeyCanBePartitioned[SearchKey];
                Func<String, IEnumerable<String>, String, String, String, IEnumerable<String>> NumeralTemplate;
                if (q.Numeral.OnOptional)
                {
                    NumeralTemplate = SelectLock_Optional;
                }
                else if (q.Numeral.OnOne)
                {
                    NumeralTemplate = SelectLock_One;
                }
                else if (q.Numeral.OnMany)
                {
                    NumeralTemplate = SelectLock_ManyAllRange;
                }
                else if (q.Numeral.OnAll || q.Numeral.OnRange)
                {
                    if (CanBePartitioned)
                    {
                        NumeralTemplate = SelectLock_AllRange_Partitioned;
                    }
                    else
                    {
                        NumeralTemplate = SelectLock_ManyAllRange;
                    }
                }
                else if (q.Numeral.OnCount)
                {
                    if (CanBePartitioned)
                    {
                        NumeralTemplate = SelectLock_Count_Partitioned;
                    }
                    else
                    {
                        NumeralTemplate = SelectLock_Count;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var LockingStatement = Enumerable.Empty<String>();
                if (q.Verb.OnLock)
                {
                    var EntityNameAndParameterAndValues = new List<String>();
                    EntityNameAndParameterAndValues.Add(@"""" + q.EntityName + @"""");
                    foreach (var c in By)
                    {
                        EntityNameAndParameterAndValues.Add(@"""" + c + @"""");
                        EntityNameAndParameterAndValues.Add(GetEscapedIdentifier(c));
                    }
                    LockingStatement = Lock_LockingStatement(String.Join(", ", EntityNameAndParameterAndValues.ToArray()));
                }
                var IndexName = e.Name + GetByIndex(SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                var Filters = GetFilters(q, SearchKey.Columns.Count);
                if (CanBePartitioned && (By.Count == 0))
                {
                    Content = NumeralTemplate(IndexName, LockingStatement, Parameters, "", Filters);
                }
                else
                {
                    var PartitionIndex = CanBePartitioned ? GetEscapedIdentifier(By.First()) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                    Content = NumeralTemplate(IndexName, LockingStatement, Parameters, PartitionIndex, Filters);
                }
            }
            else if (q.Verb.OnInsert)
            {
                Func<IEnumerable<String>, IEnumerable<String>, IEnumerable<String>> NumeralTemplate;
                if (q.Numeral.OnOptional || q.Numeral.OnOne)
                {
                    NumeralTemplate = Insert_OptionalOne;
                }
                else if (q.Numeral.OnMany)
                {
                    NumeralTemplate = Insert_Many;
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var IdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Select(f => f.Name).ToList();
                var IdentityStatements = new List<String>();
                foreach (var FieldName in IdentityColumns)
                {
                    var SequenceName = "SequenceOf" + e.Name + "Dot" + FieldName;
                    IdentityStatements.AddRange(Insert_IdentityStatement(SequenceName, FieldName));
                }
                String Function;
                if (q.Numeral.OnOptional)
                {
                    Function = "AddIfNotExist";
                }
                else
                {
                    Function = "Add";
                }
                var UpdateStatements = new List<String>();
                var Keys = KeysDict[e.Name];
                foreach (var k in Keys)
                {
                    var IndexName = e.Name + GetByIndex(k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var PartitionIndex = KeyCanBePartitioned[k] ? "_v_." + GetEscapedIdentifier(k.Columns.First().Name) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                    var Key = String.Join(", ", k.Columns.Select(c => "_v_." + GetEscapedIdentifier(c.Name)));
                    UpdateStatements.AddRange(Insert_UpdateStatement(IndexName, Function, PartitionIndex, Key));
                }
                Content = NumeralTemplate(IdentityStatements, UpdateStatements);
            }
            else if (q.Verb.OnUpdate || q.Verb.OnUpsert)
            {
                Func<String, String, String, String, IEnumerable<String>, IEnumerable<String>, IEnumerable<String>> NumeralTemplate;
                if (q.Verb.OnUpdate)
                {
                    if (q.Numeral.OnOptional)
                    {
                        NumeralTemplate = Update_Optional;
                    }
                    else if (q.Numeral.OnOne)
                    {
                        NumeralTemplate = Update_One;
                    }
                    else if (q.Numeral.OnMany)
                    {
                        NumeralTemplate = Update_Many;
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
                        NumeralTemplate = Upsert_One;
                    }
                    else if (q.Numeral.OnMany)
                    {
                        NumeralTemplate = Upsert_Many;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var DeleteStatements = new List<String>();
                var UpdateStatements = new List<String>();
                var Keys = KeysDict[e.Name];
                foreach (var k in Keys)
                {
                    var IndexName = e.Name + GetByIndex(k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var PartitionIndex = KeyCanBePartitioned[k] ? "_v_." + GetEscapedIdentifier(k.Columns.First().Name) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                    var Key = String.Join(", ", k.Columns.Select(c => "_v_." + GetEscapedIdentifier(c.Name)));
                    DeleteStatements.AddRange(Delete_UpdateStatement_ManyRange(IndexName, PartitionIndex, "Remove", Key));
                    UpdateStatements.AddRange(Insert_UpdateStatement(IndexName, "Add", PartitionIndex, Key));
                }
                {
                    var IndexName = e.Name + GetByIndex(SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var PartitionIndex = KeyCanBePartitioned[SearchKey] ? "_v_." + GetEscapedIdentifier(By.First()) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                    var Filters = GetFilters(new QueryDef { EntityName = q.EntityName, Verb = q.Verb, Numeral = q.Numeral, By = By, OrderBy = q.OrderBy }, SearchKey.Columns.Count, true);
                    Content = NumeralTemplate(IndexName, Parameters, PartitionIndex, Filters, DeleteStatements, UpdateStatements);
                }
            }
            else if (q.Verb.OnDelete)
            {
                var CanBePartitioned = KeyCanBePartitioned[SearchKey];
                Func<String, String, String, String, IEnumerable<String>, IEnumerable<String>> NumeralTemplate;
                String Function;
                {
                    if (q.Numeral.OnOptional)
                    {
                        NumeralTemplate = Delete_Optional;
                        Function = "RemoveIfExist";
                    }
                    else if (q.Numeral.OnOne)
                    {
                        NumeralTemplate = Delete_One;
                        Function = "Remove";
                    }
                    else if (q.Numeral.OnMany || q.Numeral.OnRange)
                    {
                        if (CanBePartitioned)
                        {
                            NumeralTemplate = (IndexName, Parameters, PartitionIndex, Filters, UpdateStatements) => Delete_ManyRange_Partitioned(IndexName, Filters, UpdateStatements);
                        }
                        else
                        {
                            NumeralTemplate = (IndexName, Parameters, PartitionIndex, Filters, UpdateStatements) => Delete_ManyRange(IndexName, PartitionIndex, Filters, UpdateStatements);
                        }
                        Function = "Remove";
                    }
                    else if (q.Numeral.OnAll)
                    {
                        NumeralTemplate = (IndexName, Parameters, PartitionIndex, Filters, UpdateStatements) => Delete_All(UpdateStatements);
                        Function = "RemoveAll";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                var UpdateStatements = new List<String>();
                var Keys = KeysDict[e.Name];
                foreach (var k in Keys)
                {
                    var IndexName = e.Name + GetByIndex(k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    if (q.Numeral.OnOptional || q.Numeral.OnOne)
                    {
                        var PartitionIndex = KeyCanBePartitioned[k] ? "_v_." + GetEscapedIdentifier(k.Columns.First().Name) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                        var Key = String.Join(", ", k.Columns.Select(c => "_v_." + GetEscapedIdentifier(c.Name)));
                        UpdateStatements.AddRange(Delete_UpdateStatement_OptionalOne(IndexName, PartitionIndex, Function, Key));
                    }
                    else if (q.Numeral.OnMany || q.Numeral.OnRange)
                    {
                        var PartitionIndex = KeyCanBePartitioned[k] ? "_v_." + GetEscapedIdentifier(k.Columns.First().Name) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                        var Key = String.Join(", ", k.Columns.Select(c => "_v_." + GetEscapedIdentifier(c.Name)));
                        UpdateStatements.AddRange(Delete_UpdateStatement_ManyRange(IndexName, PartitionIndex, Function, Key));
                    }
                    else if (q.Numeral.OnAll)
                    {
                        if (CanBePartitioned)
                        {
                            UpdateStatements.AddRange(Delete_UpdateStatement_All_Partitioned(IndexName, Function));
                        }
                        else
                        {
                            var PartitionIndex = "0";
                            UpdateStatements.AddRange(Delete_UpdateStatement_All(IndexName, PartitionIndex, Function));
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                {
                    var IndexName = e.Name + GetByIndex(SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var Filters = GetFilters(q, SearchKey.Columns.Count);
                    if (CanBePartitioned && (By.Count == 0))
                    {
                        Content = NumeralTemplate(IndexName, Parameters, "", Filters, UpdateStatements);
                    }
                    else
                    {
                        var PartitionIndex = CanBePartitioned ? GetEscapedIdentifier(By.First()) + " % this.Data." + GetEscapedIdentifier(IndexName) + ".NumPartition" : "0";
                        Content = NumeralTemplate(IndexName, Parameters, PartitionIndex, Filters, UpdateStatements);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
            return Query(Signature, Content);
        }

        public IEnumerable<String> GetLoads()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var or = InnerTypeDict[e.Name].Record;
                var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                var Keys = KeysDict[e.Name];
                var IndexNames = new List<String>();
                var Partitions = new List<String>();
                var Updates = new List<String>();
                foreach (var k in Keys)
                {
                    var IndexName = e.Name + GetByIndex(k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var Key = String.Join(", ", k.Columns.Select(c => "v." + GetEscapedIdentifier(c.Name)));
                    var FirstColumnName = k.Columns.First().Name;
                    var FirstColumnType = d[FirstColumnName].Type;
                    var PartitionIndex = (FirstColumnType.OnTypeRef && OS.ObjectSchemaExtensions.NameMatches(FirstColumnType.TypeRef, "Int")) ? ("v." + GetEscapedIdentifier(FirstColumnName) + " % Data." + GetEscapedIdentifier(IndexName) + ".NumPartition") : "0";
                    IndexNames.Add(IndexName);
                    Partitions.AddRange(LoadSave_Partition(PartitionIndex, IndexName));
                    Updates.AddRange(LoadSave_Update(IndexName, Key));
                }
                foreach (var f in e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity))
                {
                    var SequenceName = "SequenceOf" + e.Name + "Dot" + f.Name;
                    Updates.AddRange(LoadSave_UpdateSequence(SequenceName, f.Name));
                }
                l.AddRange(LoadSave_Load(IndexNames, Partitions, Updates, e.Name));
            }
            return l;
        }

        public IEnumerable<String> GetSaves()
        {
            var l = new List<String>();
            foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
            {
                var IndexName = e.Name + GetByIndex(e.PrimaryKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                l.AddRange(LoadSave_Save(IndexName));
            }
            return l;
        }

        public IEnumerable<String> GetComplexTypes()
        {
            var l = new List<String>();

            var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();

            var IndexAndSequenceDefinitions = GetIndexAndSequenceDefinitions();
            var IndexAndSequenceInitializations = GetIndexAndSequenceInitializations(Queries);
            var Clones = GetClones();
            var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            l.AddRange(Data(IndexAndSequenceDefinitions, IndexAndSequenceInitializations));
            l.Add("");
            l.AddRange(DataAccessBase());
            l.Add("");
            l.AddRange(DataAccessClones(Clones));
            l.Add("");

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
            l.AddRange(DataAccess(ql));
            l.Add("");

            var Loads = GetLoads();
            var Saves = GetSaves();
            l.AddRange(LoadSave(Hash, Loads, Saves));
            l.Add("");

            l.AddRange(Serializer());
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
