//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# Krustallos代码生成器
//  Version:     2014.10.21.
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
            private Dictionary<String, Dictionary<ByIndex, Key>> ByIndexDict;

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

                ByIndexDict = new Dictionary<String, Dictionary<ByIndex, Key>>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var h = new Dictionary<ByIndex, Key>();
                    {
                        var EmptyIndex = new ByIndex { Columns = new List<String> { } };
                        if (!h.ContainsKey(EmptyIndex))
                        {
                            h.Add(EmptyIndex, e.PrimaryKey);
                        }
                    }
                    foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                    {
                        var MainIndex = new ByIndex { Columns = k.Columns.Select(c => c.Name).ToList() };
                        if (!h.ContainsKey(MainIndex))
                        {
                            h.Add(MainIndex, k);
                        }
                    }
                    foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                    {
                        for (int i = 1; i < k.Columns.Count; i += 1)
                        {
                            var SubIndex = new ByIndex { Columns = k.Columns.Take(i).Select(c => c.Name).ToList() };
                            if (!h.ContainsKey(SubIndex))
                            {
                                h.Add(SubIndex, k);
                            }
                        }
                    }
                    ByIndexDict.Add(e.Name, h);
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

            public String[] GetIndices()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).ToArray();
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var IndexType = "ImmutableSortedDictionary<Key, " + e.Name + ">";
                        var KeyComparer = "new KeyComparer({0})".Formats(String.Join(", ", k.Columns.Select(c => "ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<{0}>({1}))".Formats(GetTypeString(d[c.Name].Type), c.IsDescending ? "true" : "false"))));
                        l.AddRange(GetTemplate("Data_Index").Substitute("IndexName", IndexName).Substitute("IndexType", IndexType).Substitute("KeyComparer", KeyComparer));
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

            public static String GetBy(QueryDef q, int OuterByCount)
            {
                var l = new List<String>();
                if (q.By.Count > 0)
                {
                    var Lower = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => "[[{0}]]".Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Min"))));
                    var Upper = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => "[[{0}]]".Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Max"))));
                    l.Add(".Range({0}, {1})".Formats(Lower, Upper));
                }
                l.Add(".Select(_p_ => _p_.Value)");
                return String.Join("", l.ToArray());
            }
            public static String GetByCount(QueryDef q, int OuterByCount)
            {
                var l = new List<String>();
                if (q.By.Count > 0)
                {
                    var Lower = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => "[[{0}]]".Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Min"))));
                    var Upper = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => "[[{0}]]".Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Max"))));
                    l.Add(".RangeCount({0}, {1})".Formats(Lower, Upper));
                }
                return String.Join("", l.ToArray());
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

            private class ByIndex
            {
                public List<String> Columns;

                public override bool Equals(object obj)
                {
                    var o = obj as ByIndex;
                    if (o == null) { return false; }
                    if (Columns.Count != o.Columns.Count) { return false; }
                    if (Columns.Intersect(o.Columns, StringComparer.OrdinalIgnoreCase).Count() != Columns.Count) { return false; }
                    return true;
                }

                public override int GetHashCode()
                {
                    if (Columns.Count == 0) { return 0; }
                    Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                    return Columns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
                }
            }

            public String[] GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;
                var bih = ByIndexDict[q.EntityName];

                var Signature = InnerWriter.GetQuerySignature(q);
                var ByKey = bih[new ByIndex { Columns = q.By }];
                var Parameters = String.Join(", ", q.By.ToArray());
                String[] Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    var Bys = GetBy(q, ByKey.Columns.Count);
                    var OrderBys = GetOrderBy(q);
                    String[] Template;
                    if (q.Numeral.OnOptional)
                    {
                        Template = GetTemplate("SelectLock_Optional");
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Template = GetTemplate("SelectLock_One");
                    }
                    else if (q.Numeral.OnMany || q.Numeral.OnAll || q.Numeral.OnRange)
                    {
                        Template = GetTemplate("SelectLock_ManyAllRange");
                    }
                    else if (q.Numeral.OnCount)
                    {
                        Template = GetTemplate("SelectLock_Count");
                        Bys = GetByCount(q, ByKey.Columns.Count);
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
                        foreach (var c in q.By)
                        {
                            EntityNameAndParameterAndValues.Add(@"""" + c + @"""");
                            EntityNameAndParameterAndValues.Add("[[" + c + "]]");
                        }
                        LockingStatement = GetTemplate("Lock_LockingStatement").Substitute("EntityNameAndParameterAndValues", String.Join(", ", EntityNameAndParameterAndValues.ToArray()));
                    }
                    var Filters = Bys + OrderBys;
                    if (q.Numeral.OnRange)
                    {
                        Filters = Filters + ".Skip(_Skip_).Take(_Take_)";
                    }
                    var IndexName = e.Name + "By" + String.Join("And", ByKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    Content = Template.Substitute("Function", q.Verb.OnLock ? "CheckCurrentVersioned" : "CheckReaderVersioned").Substitute("IndexName", IndexName).Substitute("LockingStatement", LockingStatement).Substitute("Parameters", Parameters).Substitute("Filters", Filters);
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    String[] Template;
                    if (q.Numeral.OnOptional || q.Numeral.OnOne)
                    {
                        Template = GetTemplate("InsertUpdateUpsert_OptionalOne");
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Template = GetTemplate("InsertUpdateUpsert_Many");
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    String Function;
                    if (q.Verb.OnInsert)
                    {
                        if (q.Numeral.OnOptional)
                        {
                            Function = "AddIfNotExist";
                        }
                        else
                        {
                            Function = "Add";
                        }
                    }
                    else if (q.Verb.OnUpdate)
                    {
                        if (q.Numeral.OnOptional)
                        {
                            Function = "SetItemIfExist";
                        }
                        else
                        {
                            Function = "SetItem";
                        }
                    }
                    else if (q.Verb.OnUpsert)
                    {
                        Function = "AddOrSetItem";
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    var UpdateStatements = new List<String>();
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).ToArray();
                    foreach (var k in Keys)
                    {
                        var Index = String.Join(", ", k.Columns.Select(c => @"""{0}""".Formats(c.IsDescending ? c.Name + "-" : c.Name)));
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                        UpdateStatements.AddRange(GetTemplate("InsertUpdateUpsert_UpdateStatement").Substitute("Index", Index).Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("Key", Key));
                    }
                    Content = Template.Substitute("UpdateStatements", UpdateStatements.ToArray());
                }
                else if (q.Verb.OnDelete)
                {
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
                        Template = GetTemplate("Delete_ManyRange");
                        Function = "RemoveRange";
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
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).ToArray();
                    foreach (var k in Keys)
                    {
                        var Index = String.Join(", ", k.Columns.Select(c => @"""{0}""".Formats(c.IsDescending ? c.Name + "-" : c.Name)));
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        if (q.Numeral.OnOptional || q.Numeral.OnOne)
                        {
                            var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_OptionalOne").Substitute("Index", Index).Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("Key", Key));
                        }
                        else if (q.Numeral.OnMany || q.Numeral.OnRange)
                        {
                            var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_ManyRange").Substitute("Index", Index).Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("Key", Key));
                        }
                        else if (q.Numeral.OnAll)
                        {
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_All").Substitute("Index", Index).Substitute("IndexName", IndexName).Substitute("Function", Function));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    {
                        var Bys = GetBy(q, ByKey.Columns.Count);
                        var OrderBys = GetOrderBy(q);
                        var Filters = Bys + OrderBys;
                        var IndexName = e.Name + "By" + String.Join("And", ByKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        Content = Template.Substitute("IndexName", IndexName).Substitute("Parameters", Parameters).Substitute("Filters", Filters).Substitute("UpdateStatements", UpdateStatements.ToArray());
                    }
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

                var Indices = GetIndices();
                var Clones = GetClones();
                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                l.AddRange(GetTemplate("Data").Substitute("Indices", Indices));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessBase"));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessClones").Substitute("Clones", Clones));
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
