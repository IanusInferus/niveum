//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# MySQL代码生成器
//  Version:     2016.10.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.CSharpMySql
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpMySql(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
        {
            var w = new Writer(Schema, EntityNamespaceName, ContextNamespaceName);
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

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpMySql);
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

                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Unit").Any()) { throw new InvalidOperationException("PrimitiveMissing: Unit"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Boolean").Any()) { throw new InvalidOperationException("PrimitiveMissing: Boolean"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "String").Any()) { throw new InvalidOperationException("PrimitiveMissing: String"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Int64").Any()) { throw new InvalidOperationException("PrimitiveMissing: Int64"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Real").Any()) { throw new InvalidOperationException("PrimitiveMissing: Real"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Byte").Any()) { throw new InvalidOperationException("PrimitiveMissing: Byte"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "Optional").Any()) { throw new InvalidOperationException("PrimitiveMissing: Optional"); }
                if (!Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive && t.Primitive.Name == "List").Any()) { throw new InvalidOperationException("PrimitiveMissing: List"); }

                InnerWriter = new CSharpPlain.CodeGenerator.Writer(Schema, NamespaceName);
            }

            public List<String> GetSchema()
            {
                var Header = GetHeader();
                var Primitives = GetPrimitives();
                var ComplexTypes = GetComplexTypes();

                if (NamespaceName != "")
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithNamespace").Substitute("Header", Header).Substitute("NamespaceName", NamespaceName).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
                else
                {
                    return EvaluateEscapedIdentifiers(GetTemplate("MainWithoutNamespace").Substitute("Header", Header).Substitute("Imports", Schema.Imports).Substitute("Primitives", Primitives).Substitute("ComplexTypes", ComplexTypes)).Select(Line => Line.TrimEnd(' ')).ToList();
                }
            }

            public List<String> GetHeader()
            {
                if (EntityNamespaceName == NamespaceName || EntityNamespaceName == "")
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", new List<String> { });
                }
                else
                {
                    return GetTemplate("Header").Substitute("EntityNamespaceName", EntityNamespaceName);
                }
            }

            public List<String> GetPrimitives()
            {
                return InnerWriter.GetPrimitives();
            }

            public String GetTypeString(OS.TypeSpec Type)
            {
                return InnerWriter.GetTypeString(Type);
            }

            public List<String> GetEnum(EnumDef e)
            {
                return GetTemplate("DataAccessEnum").Substitute("EnumName", e.Name);
            }

            public String GetTypeGetName(TypeSpec t)
            {
                if (t.OnTypeRef)
                {
                    return "Get" + t.TypeRef.Value;
                }
                else if (t.OnOptional)
                {
                    return "GetOptionalOf" + t.Optional.Value;
                }
                else if (t.OnList)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public String GetQueryString(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;
                var l = new List<String>();
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    if (q.Numeral.OnCount)
                    {
                        l.Add("SELECT COUNT(*)");
                    }
                    else
                    {
                        l.Add("SELECT " + String.Join(", ", e.Fields.Where(f => f.Attribute.OnColumn).Select(f => "`{0}`".Formats(f.Name))));
                    }
                    l.Add("FROM `{0}`".Formats(e.CollectionName));
                    if (q.By.Count != 0)
                    {
                        l.Add("WHERE " + String.Join(" AND ", q.By.Select(c => "`{0}` = @{0}".Formats(c))));
                    }
                    if (q.OrderBy.Count != 0)
                    {
                        l.Add("ORDER BY " + String.Join(", ", q.OrderBy.Select(c => (c.IsDescending ? "`{0}` DESC" : "`{0}`").Formats(c.Name))));
                    }
                    if (q.Numeral.OnRange)
                    {
                        l.Add("LIMIT @_Skip_, @_Take_");
                    }
                    if (q.Verb.OnLock)
                    {
                        l.Add("FOR UPDATE");
                    }
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    if (q.Verb.OnInsert || q.Verb.OnUpsert)
                    {
                        l.Add("INSERT");
                        if (q.Numeral.OnOptional)
                        {
                            l.Add("IGNORE");
                        }
                        l.Add("INTO `{0}`".Formats(e.CollectionName));

                        var NonIdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && !f.Attribute.Column.IsIdentity).Select(f => f.Name).ToList();
                        var IdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Select(f => f.Name).ToList();
                        l.Add("({0}) VALUES ({1})".Formats(String.Join(", ", NonIdentityColumns.Select(c => "`{0}`".Formats(c)).ToArray()), String.Join(", ", NonIdentityColumns.Select(c => "@{0}".Formats(c)).ToArray())));
                        if (q.Verb.OnUpsert)
                        {
                            var NonPrimaryKeyColumns = e.Fields.Where(f => f.Attribute.OnColumn).Select(f => f.Name).Except(e.PrimaryKey.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase).ToList();
                            l.Add("ON DUPLICATE KEY UPDATE {0}".Formats(String.Join(", ", NonIdentityColumns.Select(c => "`{0}` = @{0}".Formats(c)).ToArray())));
                        }
                        if (IdentityColumns.Count != 0)
                        {
                            return "{0}; SELECT LAST_INSERT_ID() AS `{1}`".Formats(String.Join(" ", l.ToArray()), IdentityColumns.Single());
                        }
                    }
                    else if (q.Verb.OnUpdate)
                    {
                        l.Add("UPDATE `{0}`".Formats(e.CollectionName));

                        var NonPrimaryKeyColumns = e.Fields.Where(f => f.Attribute.OnColumn).Select(f => f.Name).Except(e.PrimaryKey.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase).ToList();
                        var PrimaryKeyColumns = e.PrimaryKey.Columns.Select(c => c.Name).ToList();
                        l.Add("SET {0}".Formats(String.Join(", ", NonPrimaryKeyColumns.Select(c => "`{0}` = @{0}".Formats(c)).ToArray())));
                        l.Add("WHERE {0}".Formats(String.Join(" AND ", PrimaryKeyColumns.Select(c => "`{0}` = @{0}".Formats(c)).ToArray())));
                    }
                }
                else if (q.Verb.OnDelete)
                {
                    l.Add("DELETE");
                    l.Add("FROM `{0}`".Formats(e.CollectionName));
                    if (q.By.Count != 0)
                    {
                        l.Add("WHERE " + String.Join(" AND ", q.By.Select(c => "`{0}` = @{0}".Formats(c))));
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return String.Join(" ", l.ToArray());
            }

            public List<String> GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;

                var Signature = InnerWriter.GetQuerySignature(q);
                List<String> Content;
                if (q.Verb.OnSelect || q.Verb.OnLock)
                {
                    List<String> Template;
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
                    var LockingStatement = new List<String> { };
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
                    var SQL = GetQueryString(q);
                    var ParameterAdds = new List<String>();
                    var ResultSets = new List<String>();
                    foreach (var c in q.By)
                    {
                        ParameterAdds.AddRange(GetTemplate("SelectLockDelete_ParameterAdd").Substitute("ParameterName", c));
                    }
                    if (q.Numeral.OnRange)
                    {
                        ParameterAdds.AddRange(GetTemplate("SelectLockDelete_ParameterAdd").Substitute("ParameterName", "_Skip_"));
                        ParameterAdds.AddRange(GetTemplate("SelectLockDelete_ParameterAdd").Substitute("ParameterName", "_Take_"));
                    }
                    var Columns = e.Fields.Where(f => f.Attribute.OnColumn).ToList();
                    int k = 0;
                    foreach (var c in Columns)
                    {
                        if (k == Columns.Count - 1)
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet_Last").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        else
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        k += 1;
                    }
                    Content = Template.Substitute("EntityName", q.EntityName).Substitute("LockingStatement", LockingStatement).Substitute("SQL", SQL).Substitute("ParameterAdds", ParameterAdds).Substitute("ResultSets", ResultSets);
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    List<String> Template;
                    var IdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).ToList();
                    if (q.Verb.OnInsert && (IdentityColumns.Count != 0))
                    {
                        if (q.Numeral.OnOne)
                        {
                            Template = GetTemplate("InsertWithIdentity_One");
                        }
                        else if (q.Numeral.OnMany)
                        {
                            Template = GetTemplate("InsertWithIdentity_Many");
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
                        if (q.Numeral.OnOptional)
                        {
                            Template = GetTemplate("InsertUpdate_Optional");
                        }
                        else if (q.Numeral.OnOne)
                        {
                            Template = GetTemplate("InsertUpdate_One");
                        }
                        else if (q.Numeral.OnMany)
                        {
                            Template = GetTemplate("InsertUpdate_Many");
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    var SQL = GetQueryString(q);
                    var ParameterAdds = new List<String>();
                    if (q.Verb.OnInsert)
                    {
                        foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn && !f.Attribute.Column.IsIdentity).Select(f => f.Name))
                        {
                            ParameterAdds.AddRange(GetTemplate("InsertUpdateUpsert_ParameterAdd").Substitute("ParameterName", c));
                        }
                    }
                    else if (q.Verb.OnUpdate || q.Verb.OnUpsert)
                    {
                        foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn).Select(f => f.Name))
                        {
                            ParameterAdds.AddRange(GetTemplate("InsertUpdateUpsert_ParameterAdd").Substitute("ParameterName", c));
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    if (q.Verb.OnInsert && (IdentityColumns.Count != 0))
                    {
                        var ResultSets = new List<String>();
                        foreach (var c in IdentityColumns)
                        {
                            ResultSets.AddRange(GetTemplate("Insert_ResultSet").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        Content = Template.Substitute("SQL", SQL).Substitute("ParameterAdds", ParameterAdds).Substitute("ResultSets", ResultSets);
                    }
                    else
                    {
                        Content = Template.Substitute("SQL", SQL).Substitute("ParameterAdds", ParameterAdds);
                    }
                }
                else if (q.Verb.OnDelete)
                {
                    List<String> Template;
                    if (q.Numeral.OnOptional)
                    {
                        Template = GetTemplate("Delete_Optional");
                    }
                    else if (q.Numeral.OnOne)
                    {
                        Template = GetTemplate("Delete_One");
                    }
                    else if (q.Numeral.OnMany || q.Numeral.OnAll)
                    {
                        Template = GetTemplate("Delete_ManyAll");
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                    var SQL = GetQueryString(q);
                    var ParameterAdds = new List<String>();
                    foreach (var c in q.By)
                    {
                        ParameterAdds.AddRange(GetTemplate("SelectLockDelete_ParameterAdd").Substitute("ParameterName", c));
                    }
                    Content = Template.Substitute("SQL", SQL).Substitute("ParameterAdds", ParameterAdds);
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return GetTemplate("Query").Substitute("Signature", Signature).Substitute("Content", Content);
            }

            public List<String> GetComplexTypes()
            {
                var l = new List<String>();

                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                l.AddRange(GetTemplate("DataAccessBase"));
                l.Add("");

                var Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToList();
                var el = new List<String>();
                if (Enums.Count > 0)
                {
                    foreach (var e in Enums)
                    {
                        el.AddRange(GetEnum(e));
                        el.Add("");
                    }
                    if (el.Count > 0)
                    {
                        el = el.Take(el.Count - 1).ToList();
                    }
                    l.AddRange(GetTemplate("DataAccessEnums").Substitute("Enums", el));
                    l.Add("");
                }

                var Queries = Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries).ToList();
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
                l.AddRange(GetTemplate("DataAccess").Substitute("Queries", ql));
                l.Add("");
                l.AddRange(GetTemplate("DataAccessPool").Substitute("Hash", Hash));
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
                return InnerWriter.GetLines(Value);
            }
            public String GetEscapedIdentifier(String Identifier)
            {
                return InnerWriter.GetEscapedIdentifier(Identifier);
            }
            private List<String> EvaluateEscapedIdentifiers(List<String> Lines)
            {
                return InnerWriter.EvaluateEscapedIdentifiers(Lines);
            }
        }

        private static List<String> Substitute(this List<String> Lines, String Parameter, String Value)
        {
            return CSharpPlain.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
        private static List<String> Substitute(this List<String> Lines, String Parameter, List<String> Value)
        {
            return CSharpPlain.CodeGenerator.Substitute(Lines, Parameter, Value);
        }
    }
}
