//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# Krustallos代码生成器
//  Version:     2014.11.24.
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

namespace Yuki.RelationSchema.CSharpKrustallos
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpKrustallos(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
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
            private Dictionary<String, Key[]> KeysDict;
            private Dictionary<QueryDef, Key> QueryToSearchKey;
            private Dictionary<Key, Boolean> KeyCanBePartitioned;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpKrustallos);
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

                KeysDict = new Dictionary<String, Key[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys.Select(k => ConvertNonUniqueKeyToUniqueKey(k, e.PrimaryKey))).ToArray();
                    KeysDict.Add(e.Name, Keys);
                }

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToArray();
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
                    if (e.Fields.Where(f => f.Attribute.OnColumn).Any(f => f.Attribute.Column.IsIdentity)) { throw new InvalidOperationException("IdentitiyNotSupported: {0}".Formats(e.Name)); }
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
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

                InnerWriter = new CSharpPlain.CodeGenerator.Writer(Schema, NamespaceName);
            }

            public String[] GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports.ToArray()).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports.ToArray()).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToArray();
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

            public Key ConvertNonUniqueKeyToUniqueKey(Key NonUniqueKey, Key PrimaryKey)
            {
                return new Key { Columns = NonUniqueKey.Columns.Concat(PrimaryKey.Columns.Select(c => c.Name).Except(NonUniqueKey.Columns.Select(c => c.Name)).Select(Name => new KeyColumn { Name = Name, IsDescending = false })).ToList(), IsClustered = NonUniqueKey.IsClustered };
            }

            public String[] GetIndiceDefinitions()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    if (e.Fields.Where(f => f.Attribute.OnColumn).Any(f => f.Attribute.Column.IsIdentity)) { throw new InvalidOperationException("IdentitiyNotSupported: {0}".Formats(e.Name)); }
                    var Keys = KeysDict[e.Name];
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var IndexType = "ImmutableSortedDictionary<Key, " + e.Name + ">";
                        l.AddRange(GetTemplate("Data_IndexDefinition").Substitute("IndexName", IndexName).Substitute("IndexType", IndexType));
                    }
                }
                return l.ToArray();
            }

            public String[] GetIndiceInitializations(QueryDef[] Queries)
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    if (e.Fields.Where(f => f.Attribute.OnColumn).Any(f => f.Attribute.Column.IsIdentity)) { throw new InvalidOperationException("IdentitiyNotSupported: {0}".Formats(e.Name)); }
                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var Keys = KeysDict[e.Name];
                    foreach (var k in Keys)
                    {
                        var Index = String.Join(", ", (new String[] { e.Name }).Concat(k.Columns.Select(c => c.IsDescending ? c.Name + "-" : c.Name)).Select(v => @"""{0}""".Formats(v)));
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var IndexType = "ImmutableSortedDictionary<Key, " + e.Name + ">";
                        var KeyComparer = "new KeyComparer({0})".Formats(String.Join(", ", k.Columns.Select(c => "ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<{0}>({1}))".Formats(GetTypeString(d[c.Name].Type), c.IsDescending ? "true" : "false"))));
                        var CanBePartitioned = KeyCanBePartitioned[k];
                        var NumPartition = CanBePartitioned ? "NumPartition" : "1";
                        l.AddRange(GetTemplate("Data_IndexInitialization").Substitute("Index", Index).Substitute("IndexName", IndexName).Substitute("IndexType", IndexType).Substitute("KeyComparer", KeyComparer).Substitute("NumPartition", NumPartition));
                    }
                }
                return l.ToArray();
            }

            public String[] GetClones()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var FieldClones = new List<String>();
                    foreach (var f in e.Fields.Where(ff => ff.Attribute.OnColumn))
                    {
                        if (f.Type.OnOptional && f.Type.Optional.Value.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                        {
                            FieldClones.AddRange(GetTemplate("DataAccessClone_OptionalBinaryField").Substitute("FieldName", f.Name));
                        }
                        else if (f.Type.OnTypeRef && f.Type.TypeRef.Value.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                        {
                            FieldClones.AddRange(GetTemplate("DataAccessClone_BinaryField").Substitute("FieldName", f.Name));
                        }
                        else
                        {
                            FieldClones.AddRange(GetTemplate("DataAccessClone_Field").Substitute("FieldName", f.Name));
                        }
                    }
                    l.AddRange(GetTemplate("DataAccessClone").Substitute("EntityName", e.Name).Substitute("FieldClones", FieldClones.ToArray()));
                }
                return l.ToArray();
            }

            public static String GetFilters(QueryDef q, int OuterByCount, bool IsV = false)
            {
                var l = new List<String>();
                if (q.By.Count > 0)
                {
                    var Lower = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => (IsV ? "v.[[{0}]]" : "[[{0}]]").Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Min"))));
                    var Upper = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => (IsV ? "v.[[{0}]]" : "[[{0}]]").Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Max"))));
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

            public String[] GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;

                var Signature = InnerWriter.GetQuerySignature(q);
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
                var Parameters = String.Join(", ", By.Select(c => (IsV ? "v.[[{0}]]" : "[[{0}]]").Formats(c)).ToArray());
                String[] Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    var CanBePartitioned = KeyCanBePartitioned[SearchKey];
                    String[] Template;
                    if (q.Numeral.OnOptional)
                    {
                        Template = GetTemplate("SelectLock_Optional");
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Template = GetTemplate("SelectLock_One");
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Template = GetTemplate("SelectLock_ManyAllRange");
                    }
                    else if (q.Numeral.OnAll || q.Numeral.OnRange)
                    {
                        if (CanBePartitioned)
                        {
                            Template = GetTemplate("SelectLock_AllRange_Partitioned");
                        }
                        else
                        {
                            Template = GetTemplate("SelectLock_ManyAllRange");
                        }
                    }
                    else if (q.Numeral.OnCount)
                    {
                        if (CanBePartitioned)
                        {
                            Template = GetTemplate("SelectLock_Count_Partitioned");
                        }
                        else
                        {
                            Template = GetTemplate("SelectLock_Count");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    var LockingStatement = new String[] { };
                    if (q.Verb.OnLock)
                    {
                        var EntityNameAndParameterAndValues = new List<String>();
                        EntityNameAndParameterAndValues.Add(@"""" + q.EntityName + @"""");
                        foreach (var c in By)
                        {
                            EntityNameAndParameterAndValues.Add(@"""" + c + @"""");
                            EntityNameAndParameterAndValues.Add("[[" + c + "]]");
                        }
                        LockingStatement = GetTemplate("Lock_LockingStatement").Substitute("EntityNameAndParameterAndValues", String.Join(", ", EntityNameAndParameterAndValues.ToArray()));
                    }
                    var IndexName = e.Name + "By" + String.Join("And", SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    var Filters = GetFilters(q, SearchKey.Columns.Count);
                    if (CanBePartitioned && (By.Count == 0))
                    {
                        Content = Template.Substitute("IndexName", IndexName).Substitute("LockingStatement", LockingStatement).Substitute("Parameters", Parameters).Substitute("Filters", Filters);
                    }
                    else
                    {
                        var PartitionIndex = CanBePartitioned ? "[[" + By.First() + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                        Content = Template.Substitute("IndexName", IndexName).Substitute("LockingStatement", LockingStatement).Substitute("Parameters", Parameters).Substitute("PartitionIndex", PartitionIndex).Substitute("Filters", Filters);
                    }
                }
                else if (q.Verb.OnInsert)
                {
                    String[] Template;
                    if (q.Numeral.OnOptional || q.Numeral.OnOne)
                    {
                        Template = GetTemplate("Insert_OptionalOne");
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Template = GetTemplate("Insert_Many");
                    }
                    else
                    {
                        throw new InvalidOperationException();
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
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var PartitionIndex = KeyCanBePartitioned[k] ? "v.[[" + k.Columns.First().Name + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                        var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                        UpdateStatements.AddRange(GetTemplate("Insert_UpdateStatement").Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("PartitionIndex", PartitionIndex).Substitute("Key", Key));
                    }
                    Content = Template.Substitute("UpdateStatements", UpdateStatements.ToArray());
                }
                else if (q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    String[] Template;
                    if (q.Verb.OnUpdate)
                    {
                        if (q.Numeral.OnOptional)
                        {
                            Template = GetTemplate("Update_Optional");
                        }
                        else if (q.Numeral.OnOne)
                        {
                            Template = GetTemplate("Update_One");
                        }
                        else if (q.Numeral.OnMany)
                        {
                            Template = GetTemplate("Update_Many");
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
                            Template = GetTemplate("Upsert_One");
                        }
                        else if (q.Numeral.OnMany)
                        {
                            Template = GetTemplate("Upsert_Many");
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
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var PartitionIndex = KeyCanBePartitioned[k] ? "_v_.[[" + k.Columns.First().Name + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                        var Key = String.Join(", ", k.Columns.Select(c => "_v_.[[{0}]]".Formats(c.Name)));
                        DeleteStatements.AddRange(GetTemplate("Delete_UpdateStatement_ManyRange").Substitute("IndexName", IndexName).Substitute("Function", "Remove").Substitute("PartitionIndex", PartitionIndex).Substitute("Key", Key));
                        UpdateStatements.AddRange(GetTemplate("Insert_UpdateStatement").Substitute("IndexName", IndexName).Substitute("Function", "Add").Substitute("PartitionIndex", PartitionIndex).Substitute("Key", Key));
                    }
                    {
                        var IndexName = e.Name + "By" + String.Join("And", SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var PartitionIndex = KeyCanBePartitioned[SearchKey] ? "v.[[" + By.First() + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                        var Filters = GetFilters(new QueryDef { EntityName = q.EntityName, Verb = q.Verb, Numeral = q.Numeral, By = By, OrderBy = q.OrderBy }, SearchKey.Columns.Count, true);
                        Content = Template.Substitute("IndexName", IndexName).Substitute("Parameters", Parameters).Substitute("PartitionIndex", PartitionIndex).Substitute("Filters", Filters).Substitute("DeleteStatements", DeleteStatements.ToArray()).Substitute("UpdateStatements", UpdateStatements.ToArray());
                    }
                }
                else if (q.Verb.OnDelete)
                {
                    var CanBePartitioned = KeyCanBePartitioned[SearchKey];
                    String[] Template;
                    String Function;
                    if (q.Numeral.OnOptional)
                    {
                        Template = GetTemplate("Delete_Optional");
                        Function = "RemoveIfExist";
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Template = GetTemplate("Delete_One");
                        Function = "Remove";
                    }
                    else if (q.Numeral.OnMany || q.Numeral.OnRange)
                    {
                        if (CanBePartitioned)
                        {
                            Template = GetTemplate("Delete_ManyRange_Partitioned");
                        }
                        else
                        {
                            Template = GetTemplate("Delete_ManyRange");
                        }
                        Function = "Remove";
                    }
                    else if (q.Numeral.OnAll)
                    {
                        Template = GetTemplate("Delete_All");
                        Function = "RemoveAll";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    var UpdateStatements = new List<String>();
                    var Keys = KeysDict[e.Name];
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        if (q.Numeral.OnOptional || q.Numeral.OnOne)
                        {
                            var PartitionIndex = KeyCanBePartitioned[k] ? "_v_.[[" + k.Columns.First().Name + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                            var Key = String.Join(", ", k.Columns.Select(c => "_v_.[[{0}]]".Formats(c.Name)));
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_OptionalOne").Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("PartitionIndex", PartitionIndex).Substitute("Key", Key));
                        }
                        else if (q.Numeral.OnMany || q.Numeral.OnRange)
                        {
                            var PartitionIndex = KeyCanBePartitioned[k] ? "_v_.[[" + k.Columns.First().Name + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                            var Key = String.Join(", ", k.Columns.Select(c => "_v_.[[{0}]]".Formats(c.Name)));
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_ManyRange").Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("PartitionIndex", PartitionIndex).Substitute("Key", Key));
                        }
                        else if (q.Numeral.OnAll)
                        {
                            if (CanBePartitioned)
                            {
                                UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_All_Partitioned").Substitute("IndexName", IndexName).Substitute("Function", Function));
                            }
                            else
                            {
                                var PartitionIndex = "0";
                                UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_All").Substitute("IndexName", IndexName).Substitute("PartitionIndex", PartitionIndex).Substitute("Function", Function));
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    {
                        var IndexName = e.Name + "By" + String.Join("And", SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Filters = GetFilters(q, SearchKey.Columns.Count);
                        if (CanBePartitioned && (By.Count == 0))
                        {
                            Content = Template.Substitute("IndexName", IndexName).Substitute("Parameters", Parameters).Substitute("Filters", Filters).Substitute("UpdateStatements", UpdateStatements.ToArray());
                        }
                        else
                        {
                            var PartitionIndex = CanBePartitioned ? "[[" + By.First() + "]] % this.Data.[[" + IndexName + "]].NumPartition" : "0";
                            Content = Template.Substitute("IndexName", IndexName).Substitute("Parameters", Parameters).Substitute("PartitionIndex", PartitionIndex).Substitute("Filters", Filters).Substitute("UpdateStatements", UpdateStatements.ToArray());
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return GetTemplate("Query").Substitute("Signature", Signature).Substitute("Content", Content);
            }

            public String[] GetLoads()
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
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                        var FirstColumnName = k.Columns.First().Name;
                        var FirstColumnType = d[FirstColumnName].Type;
                        var PartitionIndex = (FirstColumnType.OnTypeRef && FirstColumnType.TypeRef.Name.Equals("Int", StringComparison.OrdinalIgnoreCase)) ? ("v.[[" + FirstColumnName + "]] % Data.[[${IndexName}]].NumPartition") : "0";
                        IndexNames.Add(IndexName);
                        Partitions.AddRange(GetTemplate("LoadSave_Partition").Substitute("PartitionIndex", PartitionIndex).Substitute("IndexName", IndexName));
                        Updates.AddRange(GetTemplate("LoadSave_Update").Substitute("IndexName", IndexName).Substitute("Key", Key));
                    }
                    l.AddRange(GetTemplate("LoadSave_Load").Substitute("IndexNames", IndexNames.ToArray()).Substitute("Partitions", Partitions.ToArray()).Substitute("Updates", Updates.ToArray()).Substitute("EntityName", e.Name));
                }
                return l.ToArray();
            }

            public String[] GetSaves()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var IndexName = e.Name + "By" + String.Join("And", e.PrimaryKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    l.AddRange(GetTemplate("LoadSave_Save").Substitute("IndexName", IndexName));
                }
                return l.ToArray();
            }

            public String[] GetComplexTypes()
            {
                var l = new List<String>();

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToArray();

                var IndiceDefinitions = GetIndiceDefinitions();
                var IndiceInitializations = GetIndiceInitializations(Queries);
                var Clones = GetClones();
                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                l.AddRange(GetTemplate("Data").Substitute("IndiceDefinitions", IndiceDefinitions).Substitute("IndiceInitializations", IndiceInitializations));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessBase"));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessClones").Substitute("Clones", Clones));
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
                l.AddRange(GetTemplate("DataAccess").Substitute("Queries", ql.ToArray()));
                l.Add("");

                var Loads = GetLoads();
                var Saves = GetSaves();
                l.AddRange(GetTemplate("LoadSave").Substitute("Hash", Hash).Substitute("Loads", Loads).Substitute("Saves", Saves));
                l.Add("");

                l.AddRange(GetTemplate("Serializer"));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessPool").Substitute("Hash", Hash));
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
            public static String[] GetLines(String Value)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.GetLines(Value);
            }
            public static String GetEscapedIdentifier(String Identifier)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.GetEscapedIdentifier(Identifier);
            }
            private String[] EvaluateEscapedIdentifiers(String[] Lines)
            {
                return OS.CSharp.Common.CodeGenerator.Writer.EvaluateEscapedIdentifiers(Lines);
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
