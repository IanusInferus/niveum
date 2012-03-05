//==========================================================================
//
//  File:        CodeGenerator.cs
//  Location:    Yuki.Core <Visual C#>
//  Description: 对象类型结构Sql数据库代码生成器
//  Version:     2012.02.27.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Streaming;
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using OS = Yuki.ObjectSchema;

namespace Yuki.RelationSchema.SqlDatabase
{
    public static class CodeGenerator
    {
        public static String CompileToSqlDatabase(this Schema Schema, String DatabaseName, Boolean WithComment = false)
        {
            Writer w = new Writer() { Schema = Schema, DatabaseName = DatabaseName, WithComment = WithComment };
            var a = w.GetSchema();
            return String.Join("\r\n", a);
        }
        public static String CompileToSqlDatabase(this OS.Schema Schema, String DatabaseName, Boolean WithComment = false)
        {
            return CompileToSqlDatabase(RelationSchemaTranslator.Translate(Schema), DatabaseName, WithComment);
        }

        private class TemplateInfo
        {
            public HashSet<String> Keywords;
            public Dictionary<String, OS.PrimitiveMapping> PrimitiveMappings;
            public Dictionary<String, OS.Template> Templates;

            public TemplateInfo(OS.ObjectSchemaTemplate Template)
            {
                Keywords = new HashSet<String>(Template.Keywords, StringComparer.Ordinal);
                PrimitiveMappings = Template.PrimitiveMappings.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
                Templates = Template.Templates.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        private class Writer
        {

            private static TemplateInfo TemplateInfo;

            public Schema Schema;
            public String DatabaseName;
            public Boolean WithComment;

            static Writer()
            {
                var b = Properties.Resources.SqlDatabase;
                XElement x;
                using (ByteArrayStream s = new ByteArrayStream(b))
                {
                    using (var sr = Txt.CreateTextReader(s.AsNewReading(), TextEncoding.Default, true))
                    {
                        x = TreeFile.ReadFile(sr);
                    }
                }

                XmlSerializer xs = new XmlSerializer();
                var t = xs.Read<OS.ObjectSchemaTemplate>(x);
                TemplateInfo = new TemplateInfo(t);
            }

            private Dictionary<String, Primitive> Primitives;
            private Dictionary<String, Enum> Enums;
            private Dictionary<String, Record> Records;
            public String[] GetSchema()
            {
                Primitives = Schema.TypeRefs.Concat(Schema.Types).Where(t => t._Tag == TypeDefTag.Primitive).Select(t => t.Primitive).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                Enums = Schema.TypeRefs.Concat(Schema.Types).Where(t => t._Tag == TypeDefTag.Enum).Select(t => t.Enum).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                Records = Schema.Types.Where(t => t._Tag == TypeDefTag.Record).Select(t => t.Record).ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

                var Tables = GetTables(Schema);
                var ForeignKeys = GetForeignKeys(Schema);
                var Comments = GetComments(Schema, WithComment);

                return GetTemplate("Main").Substitute("DatabaseName", DatabaseName).Substitute("Tables", Tables).Substitute("ForeignKeys", ForeignKeys).Substitute("Comments", Comments);
            }

            public String[] GetTables(Schema s)
            {
                var l = new List<String>();
                foreach (var t in s.Types)
                {
                    if (t._Tag == TypeDefTag.Record)
                    {
                        l.AddRange(GetTable(t.Record));
                    }
                }
                return l.ToArray();
            }
            private class ForeignKey
            {
                public String ThisTableName;
                public String[] ThisKeyColumns;
                public String OtherTableName;
                public String[] OtherKeyColumns;

                public override bool Equals(object obj)
                {
                    var o = obj as ForeignKey;
                    if (o == null) { return false; }
                    if (!ThisTableName.Equals(o.ThisTableName, StringComparison.OrdinalIgnoreCase)) { return false; }
                    if (!OtherTableName.Equals(o.OtherTableName, StringComparison.OrdinalIgnoreCase)) { return false; }
                    if (ThisKeyColumns.Length != o.ThisKeyColumns.Length) { return false; }
                    if (OtherKeyColumns.Length != o.OtherKeyColumns.Length) { return false; }
                    if (ThisKeyColumns.Intersect(o.ThisKeyColumns, StringComparer.OrdinalIgnoreCase).Count() != ThisKeyColumns.Length) { return false; }
                    if (OtherKeyColumns.Intersect(o.OtherKeyColumns, StringComparer.OrdinalIgnoreCase).Count() != OtherKeyColumns.Length) { return false; }
                    return true;
                }

                public override int GetHashCode()
                {
                    Func<String, int> h = StringComparer.OrdinalIgnoreCase.GetHashCode;
                    return h(ThisTableName) ^ h(OtherTableName) ^ ThisKeyColumns.Select(k => h(k)).Aggregate((a, b) => a ^ b) ^ OtherKeyColumns.Select(k => h(k)).Aggregate((a, b) => a ^ b);
                }
            }
            public String[] GetForeignKeys(Schema s)
            {
                var h = new HashSet<ForeignKey>();

                var l = new List<String>();
                foreach (var t in s.Types)
                {
                    if (t._Tag == TypeDefTag.Record)
                    {
                        foreach (var f in t.Record.Fields)
                        {
                            if (f.Attribute._Tag == FieldAttributeTag.Navigation)
                            {
                                if (!f.Attribute.Navigation.IsUnique) { continue; }

                                if (f.Attribute.Navigation.IsReverse)
                                {
                                    Record ThisTable = null;
                                    if (f.Type._Tag == TypeSpecTag.TypeRef)
                                    {
                                        ThisTable = Records[f.Type.TypeRef.Value];
                                    }
                                    else if (f.Type._Tag == TypeSpecTag.List)
                                    {
                                        ThisTable = Records[f.Type.List.ElementType.TypeRef.Value];
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException();
                                    }
                                    var fk = new ForeignKey { ThisTableName = ThisTable.CollectionName, ThisKeyColumns = f.Attribute.Navigation.OtherKey, OtherTableName = t.Record.CollectionName, OtherKeyColumns = f.Attribute.Navigation.ThisKey };
                                    if (!h.Contains(fk))
                                    {
                                        var Name = String.Format("FK_{0}_{1}__{2}_{3}", fk.ThisTableName, String.Join("_", fk.ThisKeyColumns), fk.OtherTableName, String.Join("_", fk.OtherKeyColumns));
                                        l.AddRange(GetForeignKey(Name, fk.ThisTableName, fk.ThisKeyColumns, fk.OtherTableName, fk.OtherKeyColumns));
                                        h.Add(fk);
                                    }
                                }
                                else
                                {
                                    var fk = new ForeignKey { ThisTableName = t.Record.CollectionName, ThisKeyColumns = f.Attribute.Navigation.ThisKey, OtherTableName = Records[f.Type.TypeRef.Value].CollectionName, OtherKeyColumns = f.Attribute.Navigation.OtherKey };
                                    if (!h.Contains(fk))
                                    {
                                        var Name = String.Format("FK_{0}_{1}__{2}_{3}", fk.ThisTableName, String.Join("_", fk.ThisKeyColumns), fk.OtherTableName, String.Join("_", fk.OtherKeyColumns));
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

            public String[] GetTable(Record r)
            {
                var FieldsAndKeys = new List<String[]>();
                foreach (var f in r.Fields)
                {
                    if (f.Attribute._Tag == FieldAttributeTag.Column)
                    {
                        FieldsAndKeys.Add(GetColumnDef(f));
                    }
                }
                {
                    var Name = String.Format("PK_{0}_{1}", r.CollectionName, String.Join("_", r.PrimaryKey.Columns.Select(c => c.Name).ToArray()));
                    FieldsAndKeys.Add(GetPrimaryKey(r.PrimaryKey, Name));
                }
                foreach (var k in r.UniqueKeys)
                {
                    var Name = String.Format("UQ_{0}_{1}", r.CollectionName, String.Join("_", k.Columns.Select(c => c.Name).ToArray()));
                    FieldsAndKeys.Add(GetUniqueKey(k, Name));
                }

                var NonUniqueKeys = new List<String>();
                foreach (var k in r.NonUniqueKeys)
                {
                    var Name = String.Format("IX_{0}_{1}", r.CollectionName, String.Join("_", k.Columns.Select(c => c.Name).ToArray()));
                    NonUniqueKeys.AddRange(GetNonUniqueKey(k, Name, r.CollectionName));
                }

                return GetTemplate("Table").Substitute("Name", r.CollectionName).Substitute("FieldsAndKeys", JoinWithComma(FieldsAndKeys.ToArray())).Substitute("NonUniqueKeys", NonUniqueKeys.ToArray());
            }

            public String[] GetColumnDef(Field f)
            {
                if (f.Type._Tag != TypeSpecTag.TypeRef)
                {
                    throw new InvalidOperationException(String.Format("列必须是简单类型: {0}", f.Name));
                }
                var TypeName = f.Type.TypeRef.Value;
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

                var l = new List<String>();
                l.Add(String.Format("[{0}]", f.Name));
                l.Add(Type);
                if (f.Attribute.Column.IsIdentity)
                {
                    l.Add("IDENTITY(1,1)");
                }
                if (f.Attribute.Column.IsNullable)
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

            public String[] GetForeignKey(String Name, String ThisTableName, String[] ThisKeyColumns, String OtherTableName, String[] OtherKeyColumns)
            {
                return GetTemplate("ForeignKey").Substitute("Name", Name).Substitute("ThisTableName", ThisTableName).Substitute("ThisKeyColumns", GetForeignColumns(ThisKeyColumns)).Substitute("OtherTableName", OtherTableName).Substitute("OtherKeyColumns", GetForeignColumns(OtherKeyColumns));
            }

            public String[] GetForeignColumns(String[] Columns)
            {
                return JoinWithComma(Columns.Select(c => new String[] { String.Format("[{0}]", c) }).ToArray());
            }
            public String[] GetColumns(KeyColumn[] Columns)
            {
                return JoinWithComma(Columns.Select(c => new String[] { c.IsDescending ? String.Format("[{0}] DESC", c.Name) : String.Format("[{0}]", c.Name) }).ToArray());
            }

            public String[] GetComments(Schema s, Boolean WithComment)
            {
                if (!WithComment) { return new String[] { }; }
                var l = new List<String>();
                foreach (var t in s.Types.Where(Type => Type.OnRecord).Select(Type => Type.Record))
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
            public String[] GetLines(String Value)
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

            List<String> l = new List<String>();
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
            List<String> l = new List<String>();
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
