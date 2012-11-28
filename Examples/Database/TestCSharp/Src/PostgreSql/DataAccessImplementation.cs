using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Npgsql;
using NpgsqlTypes;
using Database.Database;

namespace Database.PostgreSql
{
    public partial class PostgreSqlDataAccess : IDataAccess
    {
        private NpgsqlConnection Connection;
        private NpgsqlTransaction Transaction;
        public PostgreSqlDataAccess(String ConnectionString)
        {
            Connection = new NpgsqlConnection(ConnectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
        }

        public static IDataAccess Create(String ConnectionString)
        {
            return new PostgreSqlDataAccess(ConnectionString);
        }

        public void Dispose()
        {
            if (Transaction != null)
            {
                Transaction.Rollback();
                Transaction.Dispose();
                Transaction = null;
            }
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }
        }

        public void Complete()
        {
            if (Transaction != null)
            {
                Transaction.Commit();
                Transaction.Dispose();
                Transaction = null;
            }
            else
            {
                throw new InvalidOperationException();
            }
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private NpgsqlCommand CreateTextCommand()
        {
            var cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        private static void Add(NpgsqlCommand cmd, String ParameterName, Boolean? Value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ParameterName;
            p.NpgsqlDbType = NpgsqlDbType.Bit;
            if (Value.HasValue)
            {
                p.Value = new BitString(Value.Value);
            }
            else
            {
                p.Value = DBNull.Value;
            }
            cmd.Parameters.Add(p);
        }
        private static void Add(NpgsqlCommand cmd, String ParameterName, String Value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ParameterName;
            p.DbType = DbType.String;
            if (Value != null)
            {
                p.Value = Value;
            }
            else
            {
                p.Value = DBNull.Value;
            }
            cmd.Parameters.Add(p);
        }
        private static void Add(NpgsqlCommand cmd, String ParameterName, int? Value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ParameterName;
            p.DbType = DbType.Int32;
            if (Value.HasValue)
            {
                p.Value = Value;
            }
            else
            {
                p.Value = DBNull.Value;
            }
            cmd.Parameters.Add(p);
        }
        private static void Add(NpgsqlCommand cmd, String ParameterName, Double? Value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ParameterName;
            p.DbType = DbType.Single;
            if (Value.HasValue)
            {
                p.Value = (Single)(Value);
            }
            else
            {
                p.Value = DBNull.Value;
            }
            cmd.Parameters.Add(p);
        }
        private static void Add(NpgsqlCommand cmd, String ParameterName, List<Byte> Value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ParameterName;
            p.DbType = DbType.Binary;
            if (Value != null)
            {
                p.Value = Value.ToArray();
            }
            else
            {
                p.Value = DBNull.Value;
            }
            cmd.Parameters.Add(p);
        }
        private static Boolean GetBoolean(NpgsqlDataReader dr, String FieldName)
        {
            return dr.GetBitString(dr.GetOrdinal(FieldName)).Single();
        }
        private static String GetString(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { throw new InvalidOperationException(); }
            return (String)(v);
        }
        private static int GetInt(NpgsqlDataReader dr, String FieldName)
        {
            return dr.GetInt32(dr.GetOrdinal(FieldName));
        }
        private static Double GetReal(NpgsqlDataReader dr, String FieldName)
        {
            return dr.GetFloat(dr.GetOrdinal(FieldName));
        }
        private static List<Byte> GetBinary(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { throw new InvalidOperationException(); }
            return new List<Byte>((Byte[])(v));
        }
        private static Boolean? GetBooleanNullable(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return ((BitString)(v)).Single();
        }
        private static String GetStringNullable(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (String)(v);
        }
        private static int? GetIntNullable(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (int)(v);
        }
        private static Double? GetRealNullable(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (Double)(Single)(v);
        }
        private static List<Byte> GetBinaryNullable(NpgsqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return new List<Byte>((Byte[])(v));
        }
    }
}
