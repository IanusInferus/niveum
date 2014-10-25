//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# Krustallos代码生成器
//  Version:     2014.10.25.
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
                    if (e.Fields.Where(f => f.Attribute.OnColumn).Any(f => f.Attribute.Column.IsIdentity)) { throw new InvalidOperationException("IdentitiyNotSupported: {0}".Formats(e.Name)); }
                    var or = InnerTypeDict[e.Name].Record;
                    var d = or.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).ToArray();
                    foreach (var k in Keys)
                    {
                        var Index = String.Join(", ", (new String[] { e.Name }).Concat(k.Columns.Select(c => c.IsDescending ? c.Name + "-" : c.Name)).Select(v => @"""{0}""".Formats(v)));
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var IndexType = "ImmutableSortedDictionary<Key, " + e.Name + ">";
                        var KeyComparer = "new KeyComparer({0})".Formats(String.Join(", ", k.Columns.Select(c => "ConcurrentComparer.AsObjectComparer(ConcurrentComparer.CreateDefault<{0}>({1}))".Formats(GetTypeString(d[c.Name].Type), c.IsDescending ? "true" : "false"))));
                        l.AddRange(GetTemplate("Data_Index").Substitute("Index", Index).Substitute("IndexName", IndexName).Substitute("IndexType", IndexType).Substitute("KeyComparer", KeyComparer));
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

            public static String GetFilters(QueryDef q, int OuterByCount)
            {
                var l = new List<String>();
                if (q.By.Count > 0)
                {
                    var Lower = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => "[[{0}]]".Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Min"))));
                    var Upper = "new Key({0})".Formats(String.Join(", ", q.By.Select(k => "[[{0}]]".Formats(k)).Concat(Enumerable.Range(0, OuterByCount - q.By.Count).Select(i => "KeyCondition.Max"))));
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
                var ByColumns = new HashSet<String>(q.By, StringComparer.OrdinalIgnoreCase);
                var ActualOrderBy = q.OrderBy.Where(c => !ByColumns.Contains(c.Name)).ToList();
                Key SearchKey = null;
                foreach (var k in (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys))
                {
                    if (k.Columns.Count < q.By.Count + ActualOrderBy.Count) { continue; }
                    if (!k.Columns.Take(q.By.Count).Zip(q.By, (Left, Right) => Left.Name.Equals(Right, StringComparison.OrdinalIgnoreCase)).Any(f => !f))
                    {
                        if (!k.Columns.Skip(q.By.Count).Take(ActualOrderBy.Count).Zip(ActualOrderBy, (Left, Right) => Left.Name.Equals(Right.Name) && (Left.IsDescending == Right.IsDescending)).Any(f => !f))
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
                var Parameters = String.Join(", ", q.By.ToArray());
                String[] Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
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
                    var Filters = GetFilters(q, SearchKey.Columns.Count);
                    var IndexName = e.Name + "By" + String.Join("And", SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                    Content = Template.Substitute("Function", q.Verb.OnLock ? "CheckCurrentVersioned" : "CheckReaderVersioned").Substitute("IndexName", IndexName).Substitute("LockingStatement", LockingStatement).Substitute("Parameters", Parameters).Substitute("Filters", Filters);
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
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).ToArray();
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Key = String.Join(", ", k.Columns.Select(c => "v.[[{0}]]".Formats(c.Name)));
                        UpdateStatements.AddRange(GetTemplate("Insert_UpdateStatement").Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("Key", Key));
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
                    var Keys = (new Key[] { e.PrimaryKey }).Concat(e.UniqueKeys).Concat(e.NonUniqueKeys).ToArray();
                    foreach (var k in Keys)
                    {
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        var Key = String.Join(", ", k.Columns.Select(c => "_v_.[[{0}]]".Formats(c.Name)));
                        DeleteStatements.AddRange(GetTemplate("Delete_UpdateStatement_ManyRange").Substitute("IndexName", IndexName).Substitute("Function", "RemoveRange").Substitute("Key", Key));
                        UpdateStatements.AddRange(GetTemplate("Insert_UpdateStatement").Substitute("IndexName", IndexName).Substitute("Function", "Add").Substitute("Key", Key));
                    }
                    {
                        var Filters = GetFilters(q, SearchKey.Columns.Count);
                        var IndexName = e.Name + "By" + String.Join("And", SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        Content = Template.Substitute("IndexName", IndexName).Substitute("Parameters", Parameters).Substitute("Filters", Filters).Substitute("DeleteStatements", DeleteStatements.ToArray()).Substitute("UpdateStatements", UpdateStatements.ToArray());
                    }
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
                        var IndexName = e.Name + "By" + String.Join("And", k.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
                        if (q.Numeral.OnOptional || q.Numeral.OnOne)
                        {
                            var Key = String.Join(", ", k.Columns.Select(c => "_v_.[[{0}]]".Formats(c.Name)));
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_OptionalOne").Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("Key", Key));
                        }
                        else if (q.Numeral.OnMany || q.Numeral.OnRange)
                        {
                            var Key = String.Join(", ", k.Columns.Select(c => "_v_.[[{0}]]".Formats(c.Name)));
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_ManyRange").Substitute("IndexName", IndexName).Substitute("Function", Function).Substitute("Key", Key));
                        }
                        else if (q.Numeral.OnAll)
                        {
                            UpdateStatements.AddRange(GetTemplate("Delete_UpdateStatement_All").Substitute("IndexName", IndexName).Substitute("Function", Function));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    {
                        var Filters = GetFilters(q, SearchKey.Columns.Count);
                        var IndexName = e.Name + "By" + String.Join("And", SearchKey.Columns.Select(c => c.IsDescending ? c.Name + "Desc" : c.Name));
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
