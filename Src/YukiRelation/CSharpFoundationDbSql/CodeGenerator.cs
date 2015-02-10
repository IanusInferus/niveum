﻿//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构C# FoundationDB SQL代码生成器
//  Version:     2015.02.05.
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

namespace Yuki.RelationSchema.CSharpFoundationDbSql
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpFoundationDbSql(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
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

            static Writer()
            {
                var OriginalTemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpPlain);
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.CSharpFoundationDbSql);
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

            public String[] GetEnum(EnumDef e)
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
                        l.Add("SELECT " + String.Join(", ", e.Fields.Where(f => f.Attribute.OnColumn).Select(f => @"""{0}""".Formats(f.Name.ToLowerInvariant()))));
                    }
                    l.Add(@"FROM ""{0}""".Formats(e.CollectionName.ToLowerInvariant()));
                    if (q.By.Count != 0)
                    {
                        l.Add("WHERE " + String.Join(" AND ", q.By.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant()))));
                    }
                    if (q.OrderBy.Count != 0)
                    {
                        l.Add("ORDER BY " + String.Join(", ", q.OrderBy.Select(c => (c.IsDescending ? @"""{0}"" DESC" : @"""{0}""").Formats(c.Name.ToLowerInvariant()))));
                    }
                    if (q.Numeral.OnRange)
                    {
                        l.Add("LIMIT @_take_ OFFSET @_skip_");
                    }
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    if (q.Verb.OnInsert || q.Verb.OnUpsert)
                    {
                        if (q.Numeral.OnOptional && q.Verb.OnInsert)
                        {
                            throw new NotSupportedException("InsertOptional");
                        }

                        var NonPrimaryKeyColumns = e.Fields.Where(f => f.Attribute.OnColumn).Select(f => f.Name).Except(e.PrimaryKey.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase).ToArray();
                        var PrimaryKeyColumns = e.PrimaryKey.Columns.Select(c => c.Name).ToArray();
                        var NonIdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && !f.Attribute.Column.IsIdentity).Select(f => f.Name).ToArray();
                        var IdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Select(f => f.Name).ToArray();

                        if (q.Verb.OnUpsert)
                        {
                            l.Add(@"UPDATE ""{0}""".Formats(e.CollectionName.ToLowerInvariant()));
                            l.Add("SET {0}".Formats(String.Join(", ", NonPrimaryKeyColumns.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant())).ToArray())));
                            l.Add("WHERE {0};".Formats(String.Join(" AND ", PrimaryKeyColumns.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant())).ToArray())));
                        }
                        l.Add("INSERT");
                        l.Add(@"INTO ""{0}""".Formats(e.CollectionName.ToLowerInvariant()));
                        l.Add("({0})".Formats(String.Join(", ", NonIdentityColumns.Select(c => @"""{0}""".Formats(c.ToLowerInvariant())).ToArray())));
                        if (q.Verb.OnUpsert)
                        {
                            l.Add("SELECT {0}".Formats(String.Join(", ", NonIdentityColumns.Select(c => "@{0}".Formats(c.ToLowerInvariant())).ToArray())));
                            l.Add(@"WHERE NOT EXISTS (SELECT 1 FROM ""{0}"" WHERE {1})".Formats(e.CollectionName.ToLowerInvariant(), String.Join(" AND ", PrimaryKeyColumns.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant())))));
                        }
                        else
                        {
                            l.Add("VALUES ({0})".Formats(String.Join(", ", NonIdentityColumns.Select(c => "@{0}".Formats(c.ToLowerInvariant())).ToArray())));
                            if (IdentityColumns.Length != 0)
                            {
                                l.Add(@"RETURNING ""{0}""".Formats(IdentityColumns.Single().ToLowerInvariant()));
                            }
                        }
                    }
                    else if (q.Verb.OnUpdate)
                    {
                        l.Add(@"UPDATE ""{0}""".Formats(e.CollectionName.ToLowerInvariant()));

                        var NonPrimaryKeyColumns = e.Fields.Where(f => f.Attribute.OnColumn).Select(f => f.Name).Except(e.PrimaryKey.Columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase).ToArray();
                        var PrimaryKeyColumns = e.PrimaryKey.Columns.Select(c => c.Name).ToArray();
                        l.Add("SET {0}".Formats(String.Join(", ", NonPrimaryKeyColumns.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant())).ToArray())));
                        l.Add("WHERE {0}".Formats(String.Join(" AND ", PrimaryKeyColumns.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant())).ToArray())));
                    }
                }
                else if (q.Verb.OnDelete)
                {
                    l.Add("DELETE");
                    l.Add(@"FROM ""{0}""".Formats(e.CollectionName.ToLowerInvariant()));
                    if (q.By.Count != 0)
                    {
                        l.Add("WHERE " + String.Join(" AND ", q.By.Select(c => @"""{0}"" = @{0}".Formats(c.ToLowerInvariant()))));
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                return String.Join(" ", l.ToArray());
            }

            public String[] GetQuery(QueryDef q)
            {
                var e = TypeDict[q.EntityName].Entity;

                var Signature = InnerWriter.GetQuerySignature(q);
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
                    var Columns = e.Fields.Where(f => f.Attribute.OnColumn).ToArray();
                    int k = 0;
                    foreach (var c in Columns)
                    {
                        if (k == Columns.Length - 1)
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet_Last").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        else
                        {
                            ResultSets.AddRange(GetTemplate("SelectLock_ResultSet").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        k += 1;
                    }
                    Content = Template.Substitute("EntityName", q.EntityName).Substitute("LockingStatement", LockingStatement).Substitute("SQL", SQL.Replace(@"""", @"""""")).Substitute("ParameterAdds", ParameterAdds.ToArray()).Substitute("ResultSets", ResultSets.ToArray());
                }
                else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    String[] Template;
                    var IdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).ToArray();
                    if (IdentityColumns.Length != 0)
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
                    else
                    {
                        if (q.Numeral.OnOptional)
                        {
                            if (q.Verb.OnUpsert) { throw new InvalidOperationException(); }
                            Template = GetTemplate("InsertUpdate_Optional");
                        }
                        else if (q.Numeral.OnOne)
                        {
                            Template = GetTemplate("InsertUpdateUpsert_One");
                        }
                        else if (q.Numeral.OnMany)
                        {
                            Template = GetTemplate("InsertUpdateUpsert_Many");
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
                    if (IdentityColumns.Length != 0)
                    {
                        var ResultSets = new List<String>();
                        foreach (var c in IdentityColumns)
                        {
                            ResultSets.AddRange(GetTemplate("Insert_ResultSet").Substitute("ParameterName", c.Name).Substitute("TypeGet", GetTypeGetName(c.Type)));
                        }
                        Content = Template.Substitute("SQL", SQL.Replace(@"""", @"""""")).Substitute("ParameterAdds", ParameterAdds.ToArray()).Substitute("ResultSets", ResultSets.ToArray());
                    }
                    else
                    {
                        Content = Template.Substitute("SQL", SQL.Replace(@"""", @"""""")).Substitute("ParameterAdds", ParameterAdds.ToArray());
                    }
                }
                else if (q.Verb.OnDelete)
                {
                    String[] Template;
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
                    Content = Template.Substitute("SQL", SQL.Replace(@"""", @"""""")).Substitute("ParameterAdds", ParameterAdds.ToArray());
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

                var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
                l.AddRange(GetTemplate("DataAccessBase"));
                l.Add("");

                var Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToArray();
                var el = new List<String>();
                if (Enums.Length > 0)
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
                    l.AddRange(GetTemplate("DataAccessEnums").Substitute("Enums", el.ToArray()));
                    l.Add("");
                }

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
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + ToLowercase(Parameter) + "}";
            var LowercaseValue = ToLowercase(Value);

            var l = new List<String>();
            foreach (var Line in Lines)
            {
                var NewLine = Line;

                if (Line.Contains(ParameterString))
                {
                    NewLine = NewLine.Replace(ParameterString, Value);
                }

                if (Line.Contains(LowercaseParameterString))
                {
                    NewLine = NewLine.Replace(LowercaseParameterString, LowercaseValue);
                }

                l.Add(NewLine);
            }
            return l.ToArray();
        }
        private static String ToLowercase(String PascalName)
        {
            return PascalName.ToLowerInvariant();
        }
        private static String[] Substitute(this String[] Lines, String Parameter, String[] Value)
        {
            var l = new List<String>();
            foreach (var Line in Lines)
            {
                var ParameterString = "${" + Parameter + "}";
                if (Line.Contains(ParameterString))
                {
                    foreach (var vLine in Value)
                    {
                        l.Add(Line.Replace(ParameterString, vLine));
                    }
                }
                else
                {
                    l.Add(Line);
                }
            }
            return l.ToArray();
        }
    }
}