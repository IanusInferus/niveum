//==========================================================================
//
//  File:        TableOperations.cs
//  Location:    Yuki.DatabaseRegenerator <Visual C#>
//  Description: 数据表操作
//  Version:     2012.03.06.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Data;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting.TreeFormat;
using Firefly.Texting.TreeFormat.Semantics;
using Yuki.ObjectSchema;
using OS = Yuki.ObjectSchema;
using Yuki.RelationSchema;
using Yuki.RelationSchema.SqlDatabase;

namespace Yuki.DatabaseRegenerator
{
    public static class TableOperations
    {
        public static void ImportTable(Dictionary<String, RelationSchema.Record> TableMetas, Dictionary<String, Dictionary<String, Int64>> EnumMetas, IDbConnection c, IDbTransaction b, KeyValuePair<string, List<Node>> t, Boolean IsMySql = false)
        {
            Func<String, String> Escape;
            if (!IsMySql)
            {
                Escape = s => "[" + s + "]";
            }
            else
            {
                Escape = s => "`" + s + "`";
            }

            var CollectionName = t.Key;
            var Meta = TableMetas[CollectionName];
            var Name = Meta.Name;
            var Values = t.Value;
            var Columns = Meta.Fields.Where(f => f.Attribute.OnColumn).ToArray();

            if (!IsMySql)
            {
                if (Columns.Any(col => col.Attribute.Column.IsIdentity))
                {
                    IDbCommand cmd = c.CreateCommand();
                    cmd.Transaction = b;
                    cmd.CommandText = String.Format("SET IDENTITY_INSERT {0} ON", Escape(CollectionName));
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
            try
            {
                {
                    IDbCommand cmd = c.CreateCommand();
                    cmd.Transaction = b;
                    cmd.CommandText = String.Format("DELETE FROM {0}", Escape(CollectionName));
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                {
                    var ColumnStr = String.Join(", ", Columns.Select(col => Escape(col.Name)).ToArray());
                    var ParamStr = String.Join(", ", Columns.Select(col => String.Format("@{0}", col.Name)).ToArray());
                    IDbCommand cmd = c.CreateCommand();
                    cmd.Transaction = b;
                    cmd.CommandText = String.Format("INSERT INTO {0}({1}) VALUES ({2})", Escape(CollectionName), ColumnStr, ParamStr);
                    cmd.CommandType = CommandType.Text;

                    foreach (var v in Values)
                    {
                        cmd.Parameters.Clear();

                        foreach (var f in Columns)
                        {
                            var cvs = v.Stem.Children.Where(col => col._Tag == NodeTag.Stem && col.Stem.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                            if (cvs.Length != 1)
                            {
                                throw new InvalidOperationException(String.Format("InvalidData: {0}.{1}", CollectionName, f.Name));
                            }

                            var cv = cvs.Single().Stem.Children.Single().Leaf;
                            if (!f.Type.OnTypeRef)
                            {
                                throw new InvalidOperationException(String.Format("InvalidType: {0}.{1}", CollectionName, f.Name));
                            }
                            var TypeName = f.Type.TypeRef.Value;
                            try
                            {
                                if (EnumMetas.ContainsKey(TypeName))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Int64);
                                    if (f.Attribute.Column.IsNullable && cv == "-")
                                    {
                                        p.Value = DBNull.Value;
                                    }
                                    else
                                    {
                                        var e = EnumMetas[TypeName];
                                        Int64 ev = 0;
                                        if (e.ContainsKey(cv))
                                        {
                                            ev = e[cv];
                                        }
                                        else
                                        {
                                            ev = Int64.Parse(cv);
                                        }
                                        p.Value = ev;
                                    }
                                }
                                else if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Boolean);
                                    if (f.Attribute.Column.IsNullable && cv == "-")
                                    {
                                        p.Value = DBNull.Value;
                                    }
                                    else
                                    {
                                        p.Value = Boolean.Parse(cv);
                                    }
                                }
                                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.String);
                                    if (f.Attribute.Column.IsNullable && cv == "-")
                                    {
                                        p.Value = DBNull.Value;
                                    }
                                    else
                                    {
                                        p.Value = cv;
                                    }
                                }
                                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Int32);
                                    if (f.Attribute.Column.IsNullable && cv == "-")
                                    {
                                        p.Value = DBNull.Value;
                                    }
                                    else
                                    {
                                        p.Value = Int32.Parse(cv);
                                    }
                                }
                                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Single);
                                    if (f.Attribute.Column.IsNullable && cv == "-")
                                    {
                                        p.Value = DBNull.Value;
                                    }
                                    else
                                    {
                                        p.Value = Double.Parse(cv);
                                    }
                                }
                                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Binary);
                                    if (f.Attribute.Column.IsNullable && cv == "-")
                                    {
                                        p.Value = DBNull.Value;
                                    }
                                    else
                                    {
                                        p.Value = Regex.Split(cv.Trim(" \t\r\n".ToCharArray()), "( |\t|\r|\n)+", RegexOptions.ExplicitCapture).Select(s => Byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToArray();
                                    }
                                }
                                else
                                {
                                    throw new InvalidOperationException("InvalidType");
                                }
                            }
                            catch (Exception e)
                            {
                                throw new InvalidOperationException(String.Format("InvalidField: {0}.{1} = {2}", CollectionName, f.Name, cv), e);
                            }
                        }

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                if (!IsMySql)
                {
                    if (Columns.Any(col => col.Attribute.Column.IsIdentity))
                    {
                        IDbCommand cmd = c.CreateCommand();
                        cmd.Transaction = b;
                        cmd.CommandText = String.Format("SET IDENTITY_INSERT {0} OFF", Escape(CollectionName));
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private static IDataParameter Add(this IDbCommand cmd, String parameterName, DbType dbType)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = parameterName;
            p.DbType = dbType;
            cmd.Parameters.Add(p);
            return p;
        }

        public class ImportTableMetas
        {
            public Dictionary<String, List<Node>> Tables;
            public Dictionary<String, RelationSchema.Record> TableMetas;
            public Dictionary<String, Dictionary<String, Int64>> EnumMetas;
        }

        public static ImportTableMetas GetImportTableMetas(RelationSchema.Schema s, String DataDir)
        {
            var Tables = new Dictionary<String, List<Node>>();
            foreach (var f in Directory.EnumerateFiles(DataDir, "*.tree", SearchOption.AllDirectories))
            {
                var Result = TreeFile.ReadDirect(f, new TreeFormatParseSetting(), new TreeFormatEvaluateSetting());
                foreach (var n in Result.Value.Nodes)
                {
                    if (n._Tag != NodeTag.Stem) { continue; }
                    if (!Tables.ContainsKey(n.Stem.Name))
                    {
                        Tables.Add(n.Stem.Name, new List<Node>());
                    }
                    var t = Tables[n.Stem.Name];
                    t.AddRange(n.Stem.Children);
                }
            }

            var TableMetas = new Dictionary<String, RelationSchema.Record>(StringComparer.OrdinalIgnoreCase);
            var EnumMetas = new Dictionary<String, Dictionary<String, Int64>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnEnum)
                {
                    if (!EnumMetas.ContainsKey(t.Enum.Name))
                    {
                        var d = new Dictionary<String, Int64>(StringComparer.OrdinalIgnoreCase);
                        var eh = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                        foreach (var l in t.Enum.Literals)
                        {
                            if (!eh.Contains(l.Name))
                            {
                                if (d.ContainsKey(l.Name))
                                {
                                    eh.Add(l.Name);
                                    d.Remove(l.Name);
                                }
                                else
                                {
                                    d.Add(l.Name, l.Value);
                                }
                            }
                            if (!eh.Contains(l.Description))
                            {
                                if (d.ContainsKey(l.Description))
                                {
                                    eh.Add(l.Description);
                                    d.Remove(l.Description);
                                }
                                else
                                {
                                    d.Add(l.Description, l.Value);
                                }
                            }
                        }
                        EnumMetas.Add(t.Enum.Name, d);
                    }
                }
            }
            foreach (var t in s.Types)
            {
                if (t.OnRecord)
                {
                    TableMetas.Add(t.Record.CollectionName, t.Record);
                }
            }

            var NotExists = Tables.Keys.Except(TableMetas.Keys).ToArray();
            if (NotExists.Length > 0)
            {
                throw new InvalidOperationException("TableUnknown: " + String.Join(" ", NotExists));
            }

            return new ImportTableMetas { Tables = Tables, TableMetas = TableMetas, EnumMetas = EnumMetas };
        }
    }
}
