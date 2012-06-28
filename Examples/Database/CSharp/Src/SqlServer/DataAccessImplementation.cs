using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;

namespace Database.SqlServer
{
    public partial class SqlServerDataAccess : IDataAccess
    {
        private SqlConnection Connection;
        private SqlTransaction Transaction;
        public SqlServerDataAccess(String ConnectionString)
        {
            Connection = new SqlConnection(ConnectionString);
            Connection.Open();
            Transaction = Connection.BeginTransaction();
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

        private SqlCommand CreateTextCommand()
        {
            var cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }

        private static void Add(SqlCommand cmd, String ParameterName, Boolean? Value)
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
        private static void Add(SqlCommand cmd, String ParameterName, String Value)
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
        private static void Add(SqlCommand cmd, String ParameterName, int? Value)
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
        private static void Add(SqlCommand cmd, String ParameterName, Double? Value)
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
        private static void Add(SqlCommand cmd, String ParameterName, List<Byte> Value)
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
        private static Boolean GetBoolean(SqlDataReader dr, String FieldName)
        {
            return dr.GetBoolean(dr.GetOrdinal(FieldName));
        }
        private static String GetString(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlString(dr.GetOrdinal(FieldName));
            if (v.IsNull) { throw new InvalidOperationException(); }
            return v.Value;
        }
        private static int GetInt(SqlDataReader dr, String FieldName)
        {
            return dr.GetInt32(dr.GetOrdinal(FieldName));
        }
        private static Double GetReal(SqlDataReader dr, String FieldName)
        {
            return dr.GetFloat(dr.GetOrdinal(FieldName));
        }
        private static List<Byte> GetBinary(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlBinary(dr.GetOrdinal(FieldName));
            if (v.IsNull) { throw new InvalidOperationException(); }
            return new List<Byte>(v.Value);
        }
        private static Boolean? GetBooleanNullable(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlBoolean(dr.GetOrdinal(FieldName));
            if (v.IsNull) { return null; }
            return v.Value;
        }
        private static String GetStringNullable(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlString(dr.GetOrdinal(FieldName));
            if (v.IsNull) { return null; }
            return v.Value;
        }
        private static int? GetIntNullable(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlInt32(dr.GetOrdinal(FieldName));
            if (v.IsNull) { return null; }
            return v.Value;
        }
        private static Double? GetRealNullable(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlSingle(dr.GetOrdinal(FieldName));
            if (v.IsNull) { return null; }
            return (Double)(v.Value);
        }
        private static List<Byte> GetBinaryNullable(SqlDataReader dr, String FieldName)
        {
            var v = dr.GetSqlBinary(dr.GetOrdinal(FieldName));
            if (v.IsNull) { return null; }
            return new List<Byte>(v.Value);
        }
    }
}
