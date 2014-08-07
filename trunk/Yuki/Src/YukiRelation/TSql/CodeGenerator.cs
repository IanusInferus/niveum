﻿//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Relation <Visual C#>
//  Description: 关系类型结构T-SQL(SQL Server)数据库代码生成器
//  Version:     2013.04.16.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.TextEncoding;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.TSql
{
    public static class CodeGenerator
    {
        public static String CompileToTSql(this Schema Schema, String DatabaseName, Boolean WithComment = false)
        {
            Writer w = new Writer(Schema, DatabaseName, WithComment);
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }

        private class Writer
        {

            private static OS.ObjectSchemaTemplateInfo TemplateInfo;
            private const int MaxNameLength = 128;

            private Schema Schema;
            private String DatabaseName;
            private Boolean WithComment;

            static Writer()
            {
                TemplateInfo = OS.ObjectSchemaTemplateInfo.FromBinary(Properties.Resources.TSql);
            }

            public Writer(Schema Schema, String DatabaseName, Boolean WithComment)
            {
                this.Schema = Schema;
                this.DatabaseName = DatabaseName;
                this.WithComment = WithComment;

                Primitives = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnPrimitive).Select(t => t.Primitive).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t.OnEnum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                Records = Schema.Types.Where(t => t.OnEntity).Select(t => t.Entity).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
            }

            private Dictionary<String, PrimitiveDef> Primitives;
            private Dictionary<String, EnumDef> Enums;
            private Dictionary<String, EntityDef> Records;
            public String[] GetSchema()
            {
                var Tables = GetTables(Schema);
                var ForeignKeys = GetForeignKeys(Schema);
                var Comments = GetComments(Schema, WithComment);

                return GetTemplate("Main").Substitute("DatabaseName", DatabaseName).Substitute("Tables", Tables).Substitute("ForeignKeys", ForeignKeys).Substitute("Comments", Comments).Select(Line => Line.TrimEnd(' ')).ToArray();
            }

            public String[] GetTables(Schema s)
            {
                var l = new List<String>();
                foreach (var t in s.Types)
                {
                    if (t.OnEntity)
                    {
                        l.AddRange(GetTable(t.Entity));
                    }
                }
                return l.ToArray();
            }
            public String[] GetForeignKeys(Schema s)
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
                                    EntityDef ThisTable = null;
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
                return l.ToArray();
            }

            public String[] GetTable(EntityDef r)
            {
                var FieldsAndKeys = new List<String[]>();
                foreach (var f in r.Fields)
                {
                    if (f.Attribute.OnColumn)
                    {
                        FieldsAndKeys.Add(GetColumnDef(f));
                    }
                }
                {
                    var Name = RelationSchemaExtensions.GetLimitedKeyName("PK", String.Format("{0}_{1}", r.CollectionName, r.PrimaryKey.Columns.FriendlyName()), MaxNameLength);
                    FieldsAndKeys.Add(GetPrimaryKey(r.PrimaryKey, Name));
                }
                foreach (var k in r.UniqueKeys)
                {
                    var Name = RelationSchemaExtensions.GetLimitedKeyName("UQ", String.Format("{0}_{1}", r.CollectionName, k.Columns.FriendlyName()), MaxNameLength);
                    FieldsAndKeys.Add(GetUniqueKey(k, Name));
                }

                var NonUniqueKeys = new List<String>();
                foreach (var k in r.NonUniqueKeys)
                {
                    var Name = RelationSchemaExtensions.GetLimitedKeyName("IX", String.Format("{0}_{1}", r.CollectionName, k.Columns.FriendlyName()), MaxNameLength);
                    NonUniqueKeys.AddRange(GetNonUniqueKey(k, Name, r.CollectionName));
                }

                return GetTemplate("Table").Substitute("Name", r.CollectionName).Substitute("FieldsAndKeys", JoinWithComma(FieldsAndKeys.ToArray())).Substitute("NonUniqueKeys", NonUniqueKeys.ToArray());
            }

            public String[] GetColumnDef(VariableDef f)
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
                    if (!TemplateInfo.PrimitiveMappings.ContainsKey(TypeName))
                    {
                        throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                    }
                    DbTypeName = TemplateInfo.PrimitiveMappings[TypeName].PlatformName;
                }
                else if (Enums.ContainsKey(TypeName))
                {
                    var e = Enums[TypeName];
                    if (!e.UnderlyingType.OnTypeRef)
                    {
                        throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                    }
                    if (!TemplateInfo.PrimitiveMappings.ContainsKey(e.UnderlyingType.TypeRef.Value))
                    {
                        throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                    }
                    DbTypeName = TemplateInfo.PrimitiveMappings[e.UnderlyingType.TypeRef.Value].PlatformName;
                }
                else
                {
                    throw new InvalidOperationException(String.Format("未知类型'{0}': {1}", TypeName, f.Name));
                }
                var Type = DbTypeName;
                if (f.Attribute.Column.TypeParameters != "")
                {
                    Type = String.Format("{0}({1})", DbTypeName, f.Attribute.Column.TypeParameters);
                }
                else
                {
                    if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                    {
                        Type = String.Format("{0}(max)", DbTypeName);
                    }
                    else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                    {
                        Type = String.Format("{0}(max)", DbTypeName);
                    }
                }

                var l = new List<String>();
                l.Add(String.Format("[{0}]", f.Name));
                l.Add(Type);
                if (f.Attribute.Column.IsIdentity)
                {
                    l.Add("IDENTITY(1,1)");
                }
                if (IsNullable)
                {
                    l.Add("NULL");
                }
                else
                {
                    l.Add("NOT NULL");
                }

                return new String[] { String.Join(" ", l.ToArray()) };
            }

            public String[] GetPrimaryKey(Key k, String Name)
            {
                return GetTemplate("Key").Substitute("KeyKeyword", "PRIMARY KEY").Substitute("Name", Name).Substitute("ClusterKeyword", GetClusterKeyword(k.IsClustered)).Substitute("Columns", GetColumns(k.Columns));
            }

            public String[] GetUniqueKey(Key k, String Name)
            {
                return GetTemplate("Key").Substitute("KeyKeyword", "UNIQUE").Substitute("Name", Name).Substitute("ClusterKeyword", GetClusterKeyword(k.IsClustered)).Substitute("Columns", GetColumns(k.Columns));
            }

            public String[] GetNonUniqueKey(Key k, String Name, String TableName)
            {
                return GetTemplate("NonUniqueKey").Substitute("Name", Name).Substitute("TableName", TableName).Substitute("ClusterKeyword", GetClusterKeyword(k.IsClustered)).Substitute("Columns", GetColumns(k.Columns));
            }

            public String[] GetClusterKeyword(Boolean IsClustered)
            {
                if (IsClustered)
                {
                    return new String[] { "CLUSTERED" };
                }
                else
                {
                    return new String[] { "NONCLUSTERED" };
                }
            }

            public String[] GetForeignKey(String Name, String ThisTableName, IEnumerable<String> ThisKeyColumns, String OtherTableName, IEnumerable<String> OtherKeyColumns)
            {
                return GetTemplate("ForeignKey").Substitute("Name", Name).Substitute("ThisTableName", ThisTableName).Substitute("ThisKeyColumns", GetForeignColumns(ThisKeyColumns)).Substitute("OtherTableName", OtherTableName).Substitute("OtherKeyColumns", GetForeignColumns(OtherKeyColumns));
            }

            public String[] GetForeignColumns(IEnumerable<String> Columns)
            {
                return JoinWithComma(Columns.Select(c => new String[] { String.Format("[{0}]", c) }).ToArray());
            }
            public String[] GetColumns(IEnumerable<KeyColumn> Columns)
            {
                return JoinWithComma(Columns.Select(c => new String[] { c.IsDescending ? String.Format("[{0}] DESC", c.Name) : String.Format("[{0}]", c.Name) }).ToArray());
            }

            public String[] GetComments(Schema s, Boolean WithComment)
            {
                if (!WithComment) { return new String[] { }; }
                var l = new List<String>();
                foreach (var t in s.Types.Where(Type => Type.OnEntity).Select(Type => Type.Entity))
                {
                    l.AddRange(GetTemplate("TableComment").Substitute("Name", t.CollectionName).Substitute("Description", GetSqlStringLiteral(t.Description)));
                    foreach (var c in t.Fields.Where(Field => Field.Attribute.OnColumn))
                    {
                        l.AddRange(GetTemplate("ColumnComment").Substitute("TableName", t.CollectionName).Substitute("ColumnName", c.Name).Substitute("Description", GetSqlStringLiteral(c.Description)));
                    }
                }
                return l.ToArray();
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

            public String[] GetTemplate(String Name)
            {
                return GetLines(TemplateInfo.Templates[Name].Value);
            }
            public static String[] GetLines(String Value)
            {
                return Value.UnifyNewLineToLf().Split('\n');
            }
            public String[] JoinWithComma(params String[][] LinesList)
            {
                if (LinesList.Length == 0) { return new String[] { }; }
                var l = new List<String>();
                foreach (var Lines in LinesList.Take(LinesList.Length - 1))
                {
                    if (Lines.Length == 0) { continue; }
                    l.AddRange(Lines.Take(Lines.Length - 1));
                    l.Add(Lines[Lines.Length - 1] + ",");
                }
                l.AddRange(LinesList[LinesList.Length - 1]);
                return l.ToArray();
            }
        }

        private static String[] Substitute(this String[] Lines, String Parameter, String Value)
        {
            var ParameterString = "${" + Parameter + "}";
            var LowercaseParameterString = "${" + LowercaseCamelize(Parameter) + "}";
            var LowercaseValue = LowercaseCamelize(Value);

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
        private static String LowercaseCamelize(String PascalName)
        {
            var l = new List<Char>();
            foreach (var c in PascalName)
            {
                if (Char.IsLower(c))
                {
                    break;
                }

                l.Add(Char.ToLower(c));
            }

            return new String(l.ToArray()) + new String(PascalName.Skip(l.Count).ToArray());
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