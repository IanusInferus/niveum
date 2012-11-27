using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using MySql.Data.MySqlClient;

namespace Database.MySql
{
    public partial class MySqlDataAccess : IDataAccess
    {
        private MySqlConnection Connection;
        private MySqlTransaction Transaction;
        public MySqlDataAccess(String ConnectionString)
        {
            Connection = new MySqlConnection(ConnectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
        }

        public static IDataAccess Create(String ConnectionString)
        {
            return new MySqlDataAccess(ConnectionString);
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

        private MySqlCommand CreateTextCommand()
        {
            var cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        private static void Add(MySqlCommand cmd, String ParameterName, Boolean? Value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ParameterName;
            p.DbType = DbType.Boolean;
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
        private static void Add(MySqlCommand cmd, String ParameterName, String Value)
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
        private static void Add(MySqlCommand cmd, String ParameterName, int? Value)
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
        private static void Add(MySqlCommand cmd, String ParameterName, Double? Value)
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
        private static void Add(MySqlCommand cmd, String ParameterName, List<Byte> Value)
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
        private static Boolean GetBoolean(MySqlDataReader dr, String FieldName)
        {
            return dr.GetBoolean(dr.GetOrdinal(FieldName));
        }
        private static String GetString(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { throw new InvalidOperationException(); }
            return (String)(v);
        }
        private static int GetInt(MySqlDataReader dr, String FieldName)
        {
            return dr.GetInt32(dr.GetOrdinal(FieldName));
        }
        private static Double GetReal(MySqlDataReader dr, String FieldName)
        {
            return dr.GetFloat(dr.GetOrdinal(FieldName));
        }
        private static List<Byte> GetBinary(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { throw new InvalidOperationException(); }
            return new List<Byte>((Byte[])(v));
        }
        private static Boolean? GetBooleanNullable(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (Boolean)(v);
        }
        private static String GetStringNullable(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (String)(v);
        }
        private static int? GetIntNullable(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (int)(v);
        }
        private static Double? GetRealNullable(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return (Double)(Single)(v);
        }
        private static List<Byte> GetBinaryNullable(MySqlDataReader dr, String FieldName)
        {
            var v = dr.GetValue(dr.GetOrdinal(FieldName));
            if (v == DBNull.Value) { return null; }
            return new List<Byte>((Byte[])(v));
        }
    }
}
