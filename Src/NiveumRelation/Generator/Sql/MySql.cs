//==========================================================================
//
//  File:        MySql.cs
//  Location:    Niveum.Relation <Visual C#>
//  Description: 关系类型结构MySQL数据库代码生成器
//  Version:     2026.06.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;

namespace Niveum.RelationSchema.MySql
{
    public static class CodeGenerator
    {
        public static String CompileToMySql(this Schema Schema, String DatabaseName, Boolean WithComment = false)
        {
            var t = new Templates(Schema, DatabaseName, WithComment);
            var a = t.GetSchema();
            return String.Join("\r\n", a);
        }
    }

    public partial class Templates
    {
        private const int MaxNameLength = 64;

        private Schema Schema;
        private String DatabaseName;
        private Boolean WithComment;

        private Dictionary<String, PrimitiveDef> Primitives;
        private Dictionary<String, EnumDef> Enums;
        private Dictionary<String, EntityDef> Records;

        private Dictionary<String, String> PrimitiveMapping = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            {"Boolean", "bit"},
            {"String", "varchar"},
            {"Int", "int"},
            {"Int64", "bigint"},
            {"Real", "float"},
            {"Binary", "varbinary"}
        };

        public Templates(Schema Schema, String DatabaseName, Boolean WithComment)
        {
            this.Schema = Schema;
            this.DatabaseName = DatabaseName;
            this.WithComment = WithComment;

            Primitives = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive).Select(t => t.Primitive).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            Records = Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        }

        public String GetEscapedIdentifier(String Identifier)
        {
            return Identifier;
        }

        public List<String> GetSchema()
        {
            var Tables = GetTables(Schema);
            var ForeignKeys = GetForeignKeys(Schema);

            return Main(DatabaseName, Tables, ForeignKeys).Select(Line => Line.TrimEnd(' ')).ToList();
        }

        public IEnumerable<String> GetTables(Schema s)
        {
            var l = new List<String>();
            foreach (var t in s.Types)
            {
                if (t.OnEntity)
                {
                    l.AddRange(GetTable(t.Entity));
                }
            }
            return l;
        }

        public IEnumerable<String> GetForeignKeys(Schema s)
        {
            var h = new HashSet<ForeignKey>();

            var l = new List<String>();
            foreach (var t in s.Types)
            {
                if (t.OnEntity)
                {
                    foreach (var f in t.Entity.Fields)
                    {
                        if (f.Attribute.OnNavigation)
                        {
                            if (!f.Attribute.Navigation.IsUnique) { continue; }

                            if (f.Attribute.Navigation.IsReverse)
                            {
                                EntityDef? ThisTable = null;
                                if (f.Type.OnTypeRef)
                                {
                                    ThisTable = Records[f.Type.TypeRef.Value];
                                }
                                else if (f.Type.OnOptional)
                                {
                                    ThisTable = Records[f.Type.Optional.Value];
                                }
                                else if (f.Type.OnList)
                                {
                                    ThisTable = Records[f.Type.List.Value];
                                }
                                else
                                {
                                    throw new InvalidOperationException();
                                }
                                var fk = new ForeignKey { ThisTableName = ThisTable.CollectionName, ThisKeyColumns = f.Attribute.Navigation.OtherKey, OtherTableName = t.Entity.CollectionName, OtherKeyColumns = f.Attribute.Navigation.ThisKey };
                                if (!h.Contains(fk))
                                {
                                    var Name = fk.GetLimitedName(MaxNameLength);
                                    l.AddRange(GetForeignKey(Name, fk.ThisTableName, fk.ThisKeyColumns, fk.OtherTableName, fk.OtherKeyColumns));
                                    h.Add(fk);
                                }
                            }
                            else
                            {
                                var fk = new ForeignKey { ThisTableName = t.Entity.CollectionName, ThisKeyColumns = f.Attribute.Navigation.ThisKey, OtherTableName = Records[f.Type.TypeRef.Value].CollectionName, OtherKeyColumns = f.Attribute.Navigation.OtherKey };
                                if (!h.Contains(fk))
                                {
                                    var Name = fk.GetLimitedName(MaxNameLength);
                                    l.AddRange(GetForeignKey(Name, fk.ThisTableName, fk.ThisKeyColumns, fk.OtherTableName, fk.OtherKeyColumns));
                                    h.Add(fk);
                                }
                            }
                        }
                    }
                }
            }
            return l;
        }

        private class ByIndex
        {
            public required List<String> Columns { get; init; }

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

        public IEnumerable<String> GetTable(EntityDef r)
        {
            var DuplicatedInMySqlViewPoint = new HashSet<Key>();
            var NondirectionalKeys = new HashSet<ByIndex>();
            foreach (var k in (new Key[] { r.PrimaryKey }).Concat(r.UniqueKeys).Concat(r.NonUniqueKeys))
            {
                var Index = new ByIndex { Columns = k.Columns.Select(c => c.Name).ToList() };
                if (!NondirectionalKeys.Contains(Index))
                {
                    NondirectionalKeys.Add(Index);
                }
                else
                {
                    DuplicatedInMySqlViewPoint.Add(k);
                }
            }

            var FieldsAndKeys = new List<List<String>>();
            foreach (var f in r.Fields)
            {
                if (f.Attribute.OnColumn)
                {
                    FieldsAndKeys.Add(GetColumnDef(f).ToList());
                }
            }
            var FieldDict = r.Fields.ToDictionary(f => f.Name);
            {
                var Name = RelationSchemaExtensions.GetLimitedKeyName("PK", String.Format("{0}_{1}", r.CollectionName, r.PrimaryKey.Columns.FriendlyName()), MaxNameLength);
                foreach (var c in r.PrimaryKey.Columns)
                {
                    var f = FieldDict[c.Name];
                    if (f.Type.OnOptional)
                    {
                        throw new InvalidOperationException(String.Format("MySQL主键中不支持NULL字段'{0}': {1}", Name, f.Name));
                    }
                }
                FieldsAndKeys.Add(GetPrimaryKey(r.PrimaryKey, Name).ToList());
            }
            foreach (var k in r.UniqueKeys)
            {
                if (DuplicatedInMySqlViewPoint.Contains(k)) { continue; }
                var Name = RelationSchemaExtensions.GetLimitedKeyName("UQ", String.Format("{0}_{1}", r.CollectionName, k.Columns.FriendlyName()), MaxNameLength);
                FieldsAndKeys.Add(GetUniqueKey(k, Name).ToList());
            }

            var Options = new List<List<String>>();
            if (WithComment)
            {
                if (r.Description != "")
                {
                    Options.Add(new List<String> { "COMMENT " + GetSqlStringLiteral(r.Description) });
                }
            }

            var NonUniqueKeys = new List<String>();
            foreach (var k in r.NonUniqueKeys)
            {
                if (DuplicatedInMySqlViewPoint.Contains(k)) { continue; }
                var Name = RelationSchemaExtensions.GetLimitedKeyName("IX", String.Format("{0}_{1}", r.CollectionName, k.Columns.FriendlyName()), MaxNameLength);
                NonUniqueKeys.AddRange(GetNonUniqueKey(k, Name, r.CollectionName));
            }

            return Table(r.CollectionName, JoinWithComma(FieldsAndKeys.ToArray()), JoinWithComma(Options.ToArray()), NonUniqueKeys).ToList();
        }

        public IEnumerable<String> GetColumnDef(VariableDef f)
        {
            String TypeName;
            Boolean IsNullable;
            if (f.Type.OnTypeRef)
            {
                TypeName = f.Type.TypeRef.Value;
                IsNullable = false;
            }
            else if (f.Type.OnOptional)
            {
                TypeName = f.Type.Optional.Value;
                IsNullable = true;
            }
            else
            {
                throw new InvalidOperationException(String.Format("列必须是简单类型: {0}", f.Name));
            }
            var DbTypeName = "";
            if (Primitives.ContainsKey(TypeName))
            {
                if (!PrimitiveMapping.ContainsKey(TypeName))
                {
                    throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                }
                DbTypeName = PrimitiveMapping[TypeName];
            }
            else if (Enums.ContainsKey(TypeName))
            {
                var e = Enums[TypeName];
                if (!e.UnderlyingType.OnTypeRef)
                {
                    throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                }
                if (!PrimitiveMapping.ContainsKey(e.UnderlyingType.TypeRef.Value))
                {
                    throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                }
                DbTypeName = PrimitiveMapping[e.UnderlyingType.TypeRef.Value];
            }
            else
            {
                throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
            }
            var Type = DbTypeName;
            if (f.Attribute.Column.TypeParameters != "")
            {
                if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase) && f.Attribute.Column.TypeParameters.Equals("max", StringComparison.OrdinalIgnoreCase))
                {
                    Type = "longtext";
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase) && f.Attribute.Column.TypeParameters.Equals("max", StringComparison.OrdinalIgnoreCase))
                {
                    Type = "longblob";
                }
                else
                {
                    Type = String.Format("{0}({1})", DbTypeName, f.Attribute.Column.TypeParameters);
                }
            }
            else
            {
                if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    Type = "longtext";
                }
                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                {
                    Type = "longblob";
                }
            }

            var l = new List<String>();
            l.Add(String.Format("`{0}`", f.Name));
            l.Add(Type);
            if (f.Attribute.Column.IsIdentity)
            {
                l.Add("AUTO_INCREMENT");
            }
            if (IsNullable)
            {
                l.Add("NULL");
            }
            else
            {
                l.Add("NOT NULL");
            }

            if (WithComment)
            {
                if (f.Description != "")
                {
                    l.Add("COMMENT " + GetSqlStringLiteral(f.Description));
                }
            }

            return new List<String> { String.Join(" ", l.ToArray()) };
        }

        public IEnumerable<String> GetPrimaryKey(Key k, String Name)
        {
            return Key("PRIMARY KEY", Name, GetClusterKeyword(k.IsClustered), GetColumns(k.Columns));
        }

        public IEnumerable<String> GetUniqueKey(Key k, String Name)
        {
            return Key("UNIQUE", Name, GetClusterKeyword(k.IsClustered), GetColumns(k.Columns));
        }

        public IEnumerable<String> GetNonUniqueKey(Key k, String Name, String TableName)
        {
            return NonUniqueKey(Name, TableName, GetClusterKeyword(k.IsClustered), GetColumns(k.Columns));
        }

        public String GetClusterKeyword(Boolean IsClustered)
        {
            return IsClustered ? "CLUSTERED" : "NONCLUSTERED";
        }

        public IEnumerable<String> GetForeignKey(String Name, String ThisTableName, IEnumerable<String> ThisKeyColumns, String OtherTableName, IEnumerable<String> OtherKeyColumns)
        {
            return ForeignKey(Name, ThisTableName, GetForeignColumns(ThisKeyColumns), OtherTableName, GetForeignColumns(OtherKeyColumns));
        }

        public IEnumerable<String> GetForeignColumns(IEnumerable<String> Columns)
        {
            return JoinWithComma(Columns.Select(c => new List<String> { String.Format("`{0}`", c) }).ToArray());
        }
        public IEnumerable<String> GetColumns(IEnumerable<KeyColumn> Columns)
        {
            return JoinWithComma(Columns.Select(c => new List<String> { String.Format("`{0}`", c.Name) }).ToArray());
        }

        private Regex rControlChar = new Regex(@"[\u0000-\u001F]");
        private Regex rSingleQuote = new Regex(@"\'");
        public String GetSqlStringLiteral(String s)
        {
            var l = new List<String>();
            var sl = new List<Char>();
            foreach (var c in s)
            {
                var cs = Convert.ToString(c);
                if (rControlChar.Match(cs).Success)
                {
                    if (sl.Count != 0)
                    {
                        l.Add(String.Format(@"'{0}'", new String(sl.ToArray())));
                        sl.Clear();
                    }
                    l.Add(String.Format("CHAR({0})", Convert.ToInt32(c).ToInvariantString()));
                }
                else if (rSingleQuote.Match(cs).Success)
                {
                    sl.Add(c);
                    sl.Add(c);
                }
                else
                {
                    sl.Add(c);
                }
            }
            if (sl.Count != 0)
            {
                l.Add(String.Format(@"'{0}'", new String(sl.ToArray())));
                sl.Clear();
            }
            if (l.Count == 0)
            {
                return "''";
            }
            return String.Join(" + ", l.ToArray());
        }

        public IEnumerable<String> JoinWithComma(params List<String>[] LinesList)
        {
            if (LinesList.Length == 0) { return new List<String> { }; }
            var l = new List<String>();
            foreach (var Lines in LinesList.Take(LinesList.Length - 1))
            {
                if (Lines.Count == 0) { continue; }
                l.AddRange(Lines.Take(Lines.Count - 1));
                l.Add(Lines[Lines.Count - 1] + ",");
            }
            l.AddRange(LinesList[LinesList.Length - 1]);
            return l;
        }
    }
}
