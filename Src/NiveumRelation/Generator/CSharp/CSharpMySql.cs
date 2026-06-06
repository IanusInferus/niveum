//==========================================================================
//
//  File:        CSharpMySql.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构C# MySQL代码生成器
//  Version:     2026.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using OS = Niveum.ObjectSchema;

namespace Niveum.RelationSchema.CSharpMySql
{
    public static class CodeGenerator
    {
        public static String CompileToCSharpMySql(this Schema Schema, String EntityNamespaceName, String ContextNamespaceName)
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

        public Templates(Schema Schema, String EntityNamespaceName, String NamespaceName)
        {
            this.Schema = Schema;
            this.EntityNamespaceName = EntityNamespaceName;
            this.NamespaceName = NamespaceName;
            InnerSchema = PlainObjectSchemaGenerator.Generate(Schema, EntityNamespaceName);
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

        public IEnumerable<String> GetEnum(EnumDef e)
        {
            return DataAccessEnum(e.Name);
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

        public IEnumerable<String> GetQuery(QueryDef q)
        {
            var e = TypeDict[q.EntityName].Entity;

            var Signature = Inner.GetQuerySignature(q);
            IEnumerable<String> Content;
            if (q.Verb.OnSelect || q.Verb.OnLock)
            {
                Func<String, IEnumerable<String>, String, IEnumerable<String>, IEnumerable<String>, IEnumerable<String>> NumeralTemplate;
                if (q.Numeral.OnOptional)
                {
                    NumeralTemplate = SelectLock_Optional;
                }
                else if (q.Numeral.OnOne)
                {
                    NumeralTemplate = SelectLock_One;
                }
                else if (q.Numeral.OnMany || q.Numeral.OnAll || q.Numeral.OnRange)
                {
                    NumeralTemplate = SelectLock_ManyAllRange;
                }
                else if (q.Numeral.OnCount)
                {
                    NumeralTemplate = (EntityName, LockingStatement, SQL, ParameterAdds, ResultSets) => SelectLock_Count(EntityName, LockingStatement, SQL, ParameterAdds);
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
                    foreach (var c in q.By)
                    {
                        EntityNameAndParameterAndValues.Add(@"""" + c + @"""");
                        EntityNameAndParameterAndValues.Add(GetEscapedIdentifier(c));
                    }
                    LockingStatement = Lock_LockingStatement(String.Join(", ", EntityNameAndParameterAndValues.ToArray()));
                }
                var SQL = GetQueryString(q);
                var ParameterAdds = new List<String>();
                var ResultSets = new List<String>();
                foreach (var c in q.By)
                {
                    ParameterAdds.AddRange(SelectLockDelete_ParameterAdd(c));
                }
                if (q.Numeral.OnRange)
                {
                    ParameterAdds.AddRange(SelectLockDelete_ParameterAdd("_Skip_"));
                    ParameterAdds.AddRange(SelectLockDelete_ParameterAdd("_Take_"));
                }
                var Columns = e.Fields.Where(f => f.Attribute.OnColumn).ToList();
                int k = 0;
                foreach (var c in Columns)
                {
                    if (k == Columns.Count - 1)
                    {
                        ResultSets.AddRange(SelectLock_ResultSet_Last(c.Name, GetTypeGetName(c.Type)));
                    }
                    else
                    {
                        ResultSets.AddRange(SelectLock_ResultSet(c.Name, GetTypeGetName(c.Type)));
                    }
                    k += 1;
                }
                Content = NumeralTemplate(q.EntityName, LockingStatement, SQL, ParameterAdds, ResultSets);
            }
            else if (q.Verb.OnInsert || q.Verb.OnUpdate || q.Verb.OnUpsert)
            {
                Func<String, IEnumerable<String>, IEnumerable<String>, IEnumerable<String>> NumeralTemplate;
                var IdentityColumns = e.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).ToList();
                if (q.Verb.OnInsert && (IdentityColumns.Count != 0))
                {
                    if (q.Numeral.OnOne)
                    {
                        NumeralTemplate = InsertWithIdentity_One;
                    }
                    else if (q.Numeral.OnMany)
                    {
                        NumeralTemplate = InsertWithIdentity_Many;
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
                        NumeralTemplate = (SQL, ParameterAdds, ResultSets) => Upsert_One(SQL, ParameterAdds);
                    }
                    else if (q.Numeral.OnMany)
                    {
                        NumeralTemplate = (SQL, ParameterAdds, ResultSets) => Upsert_Many(SQL, ParameterAdds);
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
                        NumeralTemplate = (SQL, ParameterAdds, ResultSets) => InsertUpdate_Optional(SQL, ParameterAdds);
                    }
                    else if (q.Numeral.OnOne)
                    {
                        NumeralTemplate = (SQL, ParameterAdds, ResultSets) => InsertUpdate_One(SQL, ParameterAdds);
                    }
                    else if (q.Numeral.OnMany)
                    {
                        NumeralTemplate = (SQL, ParameterAdds, ResultSets) => InsertUpdate_Many(SQL, ParameterAdds);
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
                        ParameterAdds.AddRange(InsertUpdateUpsert_ParameterAdd(c));
                    }
                }
                else if (q.Verb.OnUpdate || q.Verb.OnUpsert)
                {
                    foreach (var c in e.Fields.Where(f => f.Attribute.OnColumn).Select(f => f.Name))
                    {
                        ParameterAdds.AddRange(InsertUpdateUpsert_ParameterAdd(c));
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var ResultSets = new List<String>();
                if (q.Verb.OnInsert && (IdentityColumns.Count != 0))
                {
                    foreach (var c in IdentityColumns)
                    {
                        ResultSets.AddRange(Insert_ResultSet(c.Name, GetTypeGetName(c.Type)));
                    }
                }
                Content = NumeralTemplate(SQL, ParameterAdds, ResultSets);
            }
            else if (q.Verb.OnDelete)
            {
                Func<String, IEnumerable<String>, IEnumerable<String>> NumeralTemplate;
                if (q.Numeral.OnOptional)
                {
                    NumeralTemplate = Delete_Optional;
                }
                else if (q.Numeral.OnOne)
                {
                    NumeralTemplate = Delete_One;
                }
                else if (q.Numeral.OnMany || q.Numeral.OnAll)
                {
                    NumeralTemplate = Delete_ManyAll;
                }
                else
                {
                    throw new InvalidOperationException();
                }
                var SQL = GetQueryString(q);
                var ParameterAdds = new List<String>();
                foreach (var c in q.By)
                {
                    ParameterAdds.AddRange(SelectLockDelete_ParameterAdd(c));
                }
                Content = NumeralTemplate(SQL, ParameterAdds);
            }
            else
            {
                throw new InvalidOperationException();
            }
            return Query(Signature, Content);
        }

        public IEnumerable<String> GetComplexTypes()
        {
            var l = new List<String>();

            var Hash = Schema.Hash().ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
            l.AddRange(DataAccessBase());
            l.Add("");

            var Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToList();
            if (Enums.Count > 0)
            {
                var el = new List<String>();
                foreach (var e in Enums)
                {
                    el.AddRange(GetEnum(e));
                    el.Add("");
                }
                if (el.Count > 0)
                {
                    el = el.Take(el.Count - 1).ToList();
                }
                l.AddRange(DataAccessEnums(el));
                l.Add("");
            }

            var ql = new List<String>();
            foreach (var q in Schema.Types.Where(t => t.OnQueryList).SelectMany(t => t.QueryList.Queries))
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
