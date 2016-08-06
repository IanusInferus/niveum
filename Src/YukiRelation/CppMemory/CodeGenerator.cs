//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C++ Memory代码生成器
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

namespace Yuki.RelationSchema.CppMemory
{
    public static class CodeGenerator
    {
        public static String CompileToCppMemory(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
        {
            var w = new Writer(Schema, EntityNamespaceName, ContextNamespaceName);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        public class Writer
        {
            private static OS.ObjectSchemaTemplateInfo TemplateInfo;

            private CppPlain.CodeGenerator.Writer InnerWriter;
            private OS.CppBinary.CodeGenerator.Writer InnerBinaryWriter;

            private Schema Schema;
            private String EntityNamespaceName;
            private String NamespaceName;
            private OS.Schema InnerSchema;
            private Dictionary<String, TypeDef> TypeDict;
            private Dictionary<String, OS.TypeDef> InnerTypeDict;

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CppPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CppMemory);
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

                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int64").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int64"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

                InnerWriter = new CppPlain.CodeGenerator.Writer(Schema, NamespaceName);
                var Types = new List<OS.TypeDef>();
                Types.AddRange(InnerSchema.Types);
                var TableFields = Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).Select
                (
                    e => new OS.VariableDef
                    {
                        Name = e.Name,
                        Type = OS.TypeSpec.CreateGenericTypeSpec
                        (
                            new OS.GenericTypeSpec
                            {
                                TypeSpec = OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = "List", Version = "" }),
                                ParameterValues = new List<OS.TypeSpec>
                                {
                                    OS.TypeSpec.CreateTypeRef(new OS.TypeRef { Name = e.Name , Version = ""})
                                }
                            }
                         )
                    }
                ).ToList();
                Types.Add(OS.TypeDef.CreateRecord(new OS.RecordDef { Name = "MemoryDataTables", Version = "", GenericParameters = new List<OS.VariableDef> { }, Attributes = new List<KeyValuePair<String, List<String>>> { }, Fields = TableFields, Description = "" }));
                InnerBinaryWriter = new OS.CppBinary.CodeGenerator.Writer(new OS.Schema { Types = Types, TypeRefs = InnerSchema.TypeRefs, Imports = InnerSchema.Imports }, NamespaceName);
            }

            public List<String> GetSchema()
            {
                var Header = GetHeader();
                var Includes = Schema.Imports.Where(i => IsInclude(i)).ToList();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();
                var Contents = ComplexTypes;
                if (EntityNamespaceName == NamespaceName || EntityNamespaceName == "")
                {
                    Contents = GetTemplate("NamespaceImplementation").Substitute("EntityNamespaceName", new List<String> { }).Substitute("Contents", Contents);
                }
                else
                {
                    Contents = GetTemplate("NamespaceImplementation").Substitute("EntityNamespaceName", EntityNamespaceName).Substitute("Contents", Contents);
                }
                Contents = WrapContents(NamespaceName, Contents);
                return EvaluateEscapedIdentifiers(GetMain(Header, Includes, Primitives, new List<String> { }, new List<String> { }, Contents)).Select(Line => Line.TrimEnd(' ')).ToList();
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
                return GetTemplate("Header");
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(OS.TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }

            public List<String> GetTables()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    l.AddRange(GetTemplate("DataAccessBase_Table").Substitute("EntityName", e.Name));
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

            public List<String> GetIndices()
            {
                var l = new List<String>();
                foreach (var e in Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity))
                {
                    var NondirectionalKeys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).Select(k => k.Columns.Select(c => c.Name).ToList()).Distinct(new StringArrayComparer()).ToList();
                    foreach (var k in NondirectionalKeys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k);
                        var IndexType = GetIndexType(e, k);
                        l.AddRange(GetTemplate("DataAccessBase_Index").Substitute("IndexName", IndexName).Substitute("IndexType", IndexType));
                    }
                }
                return l;
            }

            public List<String> GetGenerates()
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
                        var IndexName = e.Name + "By" + String.Join("And", k);
                        var IndexType = GetIndexType(e, k, true);
                        var Fetches = new List<String>();
                        for (var n = 0; n < k.Count; n += 1)
                        {
                            var ParentByIndex = "";
                            if (n > 0)
                            {
                                ParentByIndex = "By" + String.Join("And", k.Take(n));
                            }
                            var RemainIndexType = GetIndexType(e, k.Skip(n + 1).ToList(), true);
                            var Column = k[n];
                            var ByIndex = "By" + String.Join("And", k.Take(n + 1));
                            Fetches.AddRange(GetTemplate("DataAccessBase_Generate_Fetch").Substitute("ParentByIndex", ParentByIndex).Substitute("RemainIndexType", RemainIndexType).Substitute("Column", Column).Substitute("ByIndex", ByIndex));
                        }
                        var Add = GetTemplate("DataAccessBase_Generate_Add").Substitute("ByIndex", "By" + String.Join("And", k));
                        l.AddRange(GetTemplate("DataAccessBase_Generate").Substitute("EntityName", e.Name).Substitute("IndexName", IndexName).Substitute("IndexType", IndexType).Substitute("Fetches", Fetches).Substitute("Add", Add));
                    }
                }
                return l;
            }

            public static String GetOrderBy(QueryDef q, String EntityName)
            {
                var l = new List<String>();
                var First = true;
                foreach (var k in q.OrderBy)
                {
                    if (First)
                    {
                        if (k.IsDescending)
                        {
                            l.Add(@">>orderby_descending([](std::shared_ptr<class [[" + EntityName + @"]]> _e_) { return _e_->[[" + k.Name + @"]]; })");
                        }
                        else
                        {
                            l.Add(@">>orderby([](std::shared_ptr<class [[" + EntityName + @"]]> _e_) { return _e_->[[" + k.Name + @"]]; })");
                        }
                        First = false;
                    }
                    else
                    {
                        if (k.IsDescending)
                        {
                            l.Add(@">>thenby_descending([](std::shared_ptr<class [[" + EntityName + @"]]> _e_) { return _e_->[[" + k.Name + @"]]; })");
                        }
                        else
                        {
                            l.Add(@">>thenby([](std::shared_ptr<class [[" + EntityName + @"]]> _e_) { return _e_->[[" + k.Name + @"]]; })");
                        }
                    }
                }
                return String.Join("", l.ToArray());
            }

            public List<String> GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;

                var Signature = InnerWriter.GetQuerySignature(q);
                var ManyName = (new QueryDef { EntityName = q.EntityName, Verb = q.Verb, Numeral = Numeral.CreateMany(), By = q.By, OrderBy = new List<KeyColumn> { } }).FriendlyName();
                var AllName = (new QueryDef { EntityName = q.EntityName, Verb = q.Verb, Numeral = Numeral.CreateAll(), By = q.By, OrderBy = new List<KeyColumn> { } }).FriendlyName();
                var Parameters = String.Join(", ", q.By.Select(c => "[[{0}]]".Formats(c)).ToArray());
                var OrderBys = GetOrderBy(q, e.Name);
                List<String> Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    List<String> WhenEmpty;
                    if (q.Numeral.OnOptional)
                    {
                        Content = GetTemplate("SelectLock_Optional").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters);
                        WhenEmpty = GetTemplate("SelectLock_Optional_WhenEmpty").Substitute("EntityName", e.Name);
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Content = GetTemplate("SelectLock_One").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters);
                        WhenEmpty = GetTemplate("SelectLock_One_WhenEmpty").Substitute("EntityName", e.Name);
                    }
                    else if (q.Numeral.OnMany)
                    {
                        Content = GetTemplate("SelectLock_Many").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters).Substitute("OrderBys", OrderBys);
                        WhenEmpty = GetTemplate("SelectLock_ManyRange_WhenEmpty").Substitute("EntityName", e.Name);
                    }
                    else if (q.Numeral.OnAll)
                    {
                        Content = GetTemplate("SelectLock_All").Substitute("AllName", AllName).Substitute("OrderBys", OrderBys);
                        WhenEmpty = null;
                    }
                    else if (q.Numeral.OnRange)
                    {
                        if (q.By.Count == 0)
                        {
                            Content = GetTemplate("SelectLock_RangeAll").Substitute("AllName", AllName).Substitute("OrderBys", OrderBys);
                        }
                        else
                        {
                            Content = GetTemplate("SelectLock_Range").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters).Substitute("OrderBys", OrderBys);
                        }
                        WhenEmpty = GetTemplate("SelectLock_ManyRange_WhenEmpty").Substitute("EntityName", e.Name);
                    }
                    else if (q.Numeral.OnCount)
                    {
                        Content = GetTemplate("SelectLock_Count").Substitute("ManyName", ManyName).Substitute("Parameters", Parameters);
                        WhenEmpty = GetTemplate("SelectLock_Count_WhenEmpty").Substitute("EntityName", e.Name);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }

                    if (q.Numeral.OnAll)
                    {
                        Content = Content.Substitute("EntityName", e.Name);
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
                        var IndexName = e.Name + "By" + String.Join("And", k);
                        var Fetches = new List<String>();
                        for (var n = 0; n < Key.Count; n += 1)
                        {
                            var ParentByIndex = "";
                            if (n > 0)
                            {
                                ParentByIndex = "By" + String.Join("And", Key.Take(n));
                            }
                            var Column = GetEscapedIdentifier(k[n]);
                            var ByIndex = "By" + String.Join("And", Key.Take(n + 1));
                            Fetches.AddRange(GetTemplate("SelectMany_Fetch").Substitute("EntityName", e.Name).Substitute("ParentByIndex", ParentByIndex).Substitute("Column", Column).Substitute("ByIndex", ByIndex).Substitute("WhenEmpty", WhenEmpty));
                        }
                        var Filters = new List<String>();
                        for (var n = Key.Count; n < k.Count; n += 1)
                        {
                            Filters.Add(@">>select_many([](" + GetIndexType(e, k.Skip(n).ToList(), true) + @"::value_type _d_) { return from(*std::get<1>(_d_)); })");
                        }
                        Content = Content.Substitute("EntityName", e.Name).Substitute("IndexName", IndexName).Substitute("Fetches", Fetches).Substitute("ByIndex", "By" + String.Join("And", Key.ToArray())).Substitute("Filters", String.Join("", Filters.ToArray()));
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

            public List<String> GetQueries()
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
                var Streams = InnerBinaryWriter.GetTemplate("Streams");
                var BinaryTranslator = InnerBinaryWriter.GetBinaryTranslator();
                var Generates = GetGenerates();
                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                var Queries = GetQueries();
                l.AddRange(GetTemplate("DataAccess").Substitute("Tables", Tables).Substitute("Indices", Indices).Substitute("Streams", Streams).Substitute("BinaryTranslator", BinaryTranslator).Substitute("Generates", Generates).Substitute("Hash", Hash).Substitute("Queries", Queries));
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
