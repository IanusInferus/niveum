﻿//==========================================================================
//
//  File:        TableOperations.cs
//  Location:    Yuki.DatabaseRegenerator <Visual C#>
//  Description: 数据表操作
//  Version:     2013.06.30.
//  Copyright(C) F.R.C.
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Firefly;
using Yuki.RelationSchema;
using Yuki.RelationValue;

namespace Yuki.DatabaseRegenerator
{
    public enum DatabaseType
    {
        SqlServer,
        PostgreSQL,
        MySQL
    }
    public static class TableOperations
    {
        public static void ImportTable(Dictionary<String, EntityDef> EntityMetas, Dictionary<String, String> EnumUnderlyingTypes, IDbConnection c, IDbTransaction b, KeyValuePair<String, TableVal> t, DatabaseType Type)
        {
            Func<String, String> Escape;
            if (Type == DatabaseType.SqlServer)
            {
                Escape = s => "[" + s + "]";
            }
            else if (Type == DatabaseType.PostgreSQL)
            {
                Escape = s => "\"" + s.ToLowerInvariant() + "\"";
            }
            else if (Type == DatabaseType.MySQL)
            {
                Escape = s => "`" + s + "`";
            }
            else
            {
                throw new InvalidOperationException();
            }

            var Meta = EntityMetas[t.Key];
            var Name = Meta.Name;
            var CollectionName = Meta.CollectionName;
            var Values = t.Value.Rows;
            var Columns = Meta.Fields.Where(f => f.Attribute.OnColumn).ToArray();

            if (Type == DatabaseType.SqlServer)
            {
                if (Columns.Any(col => col.Attribute.Column.IsIdentity))
                {
                    var cmd = c.CreateCommand();
                    cmd.Transaction = b;
                    cmd.CommandText = String.Format("SET IDENTITY_INSERT {0} ON", Escape(CollectionName));
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
            try
            {
                {
                    var cmd = c.CreateCommand();
                    cmd.Transaction = b;
                    cmd.CommandText = String.Format("DELETE FROM {0}", Escape(CollectionName));
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                {
                    var ColumnStr = String.Join(", ", Columns.Select(col => Escape(col.Name)).ToArray());
                    var ParamStr = String.Join(", ", Columns.Select(col => String.Format("@{0}", col.Name)).ToArray());
                    var cmd = c.CreateCommand();
                    cmd.Transaction = b;
                    cmd.CommandText = String.Format("INSERT INTO {0}({1}) VALUES ({2})", Escape(CollectionName), ColumnStr, ParamStr);
                    cmd.CommandType = CommandType.Text;

                    foreach (var v in Values)
                    {
                        cmd.Parameters.Clear();

                        var k = 0;
                        foreach (var f in Columns)
                        {
                            var cv = v.Columns[k];

                            String TypeName;
                            Boolean IsOptional;
                            if (f.Type.OnTypeRef)
                            {
                                TypeName = f.Type.TypeRef.Value;
                                IsOptional = false;
                            }
                            else if (f.Type.OnOptional)
                            {
                                TypeName = f.Type.Optional.Value;
                                IsOptional = true;
                            }
                            else
                            {
                                throw new InvalidOperationException(String.Format("InvalidType: {0}.{1}", CollectionName, f.Name));
                            }
                            if (EnumUnderlyingTypes.ContainsKey(TypeName))
                            {
                                TypeName = EnumUnderlyingTypes[TypeName];
                            }
                            if (!((!IsOptional && cv.OnPrimitive) || (IsOptional && cv.OnOptional)))
                            {
                                throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k));
                            }
                            try
                            {
                                if (TypeName.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (Type == DatabaseType.PostgreSQL)
                                    {
                                        Object Value;
                                        if (IsOptional)
                                        {
                                            if (cv.Optional == null)
                                            {
                                                Value = DBNull.Value;
                                            }
                                            else
                                            {
                                                if (!cv.Optional.HasValue.OnBooleanValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                                Value = cv.Optional.HasValue.BooleanValue;
                                            }
                                        }
                                        else
                                        {
                                            if (!cv.Primitive.OnBooleanValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                            Value = cv.Primitive.BooleanValue;
                                        }
                                        var p = cmd.AddPostgreSqlBoolean(String.Format("@{0}", f.Name), Value);
                                    }
                                    else
                                    {
                                        var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Boolean);
                                        if (IsOptional)
                                        {
                                            if (cv.Optional == null)
                                            {
                                                p.Value = DBNull.Value;
                                            }
                                            else
                                            {
                                                if (!cv.Optional.HasValue.OnBooleanValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                                p.Value = cv.Optional.HasValue.BooleanValue;
                                            }
                                        }
                                        else
                                        {
                                            if (!cv.Primitive.OnBooleanValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                            p.Value = cv.Primitive.BooleanValue;
                                        }
                                    }
                                }
                                else if (TypeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.String);
                                    if (IsOptional)
                                    {
                                        if (cv.Optional == null)
                                        {
                                            p.Value = DBNull.Value;
                                        }
                                        else
                                        {
                                            if (!cv.Optional.HasValue.OnStringValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                            p.Value = cv.Optional.HasValue.StringValue;
                                        }
                                    }
                                    else
                                    {
                                        if (!cv.Primitive.OnStringValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                        p.Value = cv.Primitive.StringValue;
                                    }
                                }
                                else if (TypeName.Equals("Int", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Int32);
                                    if (IsOptional)
                                    {
                                        if (cv.Optional == null)
                                        {
                                            p.Value = DBNull.Value;
                                        }
                                        else
                                        {
                                            if (!cv.Optional.HasValue.OnIntValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                            p.Value = cv.Optional.HasValue.IntValue;
                                        }
                                    }
                                    else
                                    {
                                        if (!cv.Primitive.OnIntValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                        p.Value = cv.Primitive.IntValue;
                                    }
                                }
                                else if (TypeName.Equals("Real", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Single);
                                    if (IsOptional)
                                    {
                                        if (cv.Optional == null)
                                        {
                                            p.Value = DBNull.Value;
                                        }
                                        else
                                        {
                                            if (!cv.Optional.HasValue.OnRealValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                            p.Value = cv.Optional.HasValue.RealValue;
                                        }
                                    }
                                    else
                                    {
                                        if (!cv.Primitive.OnRealValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                        p.Value = cv.Primitive.RealValue;
                                    }
                                }
                                else if (TypeName.Equals("Binary", StringComparison.OrdinalIgnoreCase))
                                {
                                    var p = cmd.Add(String.Format("@{0}", f.Name), DbType.Binary);
                                    if (IsOptional)
                                    {
                                        if (cv.Optional == null)
                                        {
                                            p.Value = DBNull.Value;
                                        }
                                        else
                                        {
                                            if (!cv.Optional.HasValue.OnBinaryValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                            p.Value = cv.Optional.HasValue.BinaryValue.ToArray();
                                        }
                                    }
                                    else
                                    {
                                        if (!cv.Primitive.OnBinaryValue) { throw new InvalidOperationException(String.Format("InvalidValue: {0}.{1}[{2}]", CollectionName, f.Name, k)); }
                                        p.Value = cv.Primitive.BinaryValue.ToArray();
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

                            k += 1;
                        }

                        cmd.ExecuteNonQuery();
                    }
                }
                if (Type == DatabaseType.PostgreSQL)
                {
                    var IdentityColumns = Meta.Fields.Where(f => f.Attribute.OnColumn && f.Attribute.Column.IsIdentity).Select(f => f.Name).ToArray();
                    foreach (var ic in IdentityColumns)
                    {
                        var cmd = c.CreateCommand();
                        cmd.Transaction = b;
                        cmd.CommandText = String.Format(@"SELECT setval(pg_get_serial_sequence('{0}', '{1}'), MAX({3})) FROM {2};", CollectionName.ToLowerInvariant(), ic.ToLowerInvariant(), Escape(CollectionName), Escape(ic));
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                if (Type == DatabaseType.SqlServer)
                {
                    if (Columns.Any(col => col.Attribute.Column.IsIdentity))
                    {
                        var cmd = c.CreateCommand();
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

        private static IDataParameter AddPostgreSqlBoolean(this IDbCommand cmd, String parameterName, Object Value)
        {
            var a = System.Reflection.Assembly.GetAssembly(cmd.GetType());
            var tp = a.GetType("Npgsql.NpgsqlParameter");
            var tppt = tp.GetProperty("NpgsqlDbType");
            var tt = a.GetType("NpgsqlTypes.NpgsqlDbType");
            var dbType = System.Enum.Parse(tt, "Bit");

            var p = cmd.CreateParameter();
            p.ParameterName = parameterName;
            tppt.SetValue(p, dbType, null);
            cmd.Parameters.Add(p);

            if (Value.GetType() == typeof(Boolean))
            {
                var tbs = a.GetType("NpgsqlTypes.BitString");
                p.Value = Activator.CreateInstance(tbs, new Object[] { Value });
            }

            return p;
        }

        public class ImportTableInfo
        {
            public Dictionary<String, TableVal> Tables;
            public Dictionary<String, EntityDef> EntityMetas;
            public Dictionary<String, String> EnumUnderlyingTypes;
        }

        public static ImportTableInfo GetImportTableInfo(Schema s, RelationVal Value)
        {
            var Tables = new Dictionary<String, TableVal>(StringComparer.OrdinalIgnoreCase);
            var EntityMetas = new Dictionary<String, EntityDef>(StringComparer.OrdinalIgnoreCase);
            var EnumUnderlyingTypes = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in s.TypeRefs.Concat(s.Types))
            {
                if (t.OnEnum)
                {
                    if (!t.Enum.UnderlyingType.OnTypeRef)
                    {
                        throw new InvalidOperationException("EnumUnderlyingTypeNotTypeRef: {0}".Formats(t.Enum.Name));
                    }
                    EnumUnderlyingTypes.Add(t.Enum.Name, t.Enum.UnderlyingType.TypeRef);
                }
            }
            var k = 0;
            foreach (var t in s.Types)
            {
                if (t.OnEntity)
                {
                    Tables.Add(t.Entity.Name, Value.Tables[k]);
                    EntityMetas.Add(t.Entity.Name, t.Entity);
                    k += 1;
                }
            }

            var NotExists = Tables.Keys.Except(EntityMetas.Keys).ToArray();
            if (NotExists.Length > 0)
            {
                throw new InvalidOperationException("TableUnknown: " + String.Join(" ", NotExists));
            }

            return new ImportTableInfo { Tables = Tables, EntityMetas = EntityMetas, EnumUnderlyingTypes = EnumUnderlyingTypes };
        }
    }
}