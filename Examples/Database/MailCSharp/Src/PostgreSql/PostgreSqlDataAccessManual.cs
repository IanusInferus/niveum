using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using Database.Database;
using Boolean = System.Boolean;
using String = System.String;
using Type = System.Type;
using Int = System.Int32;
using Real = System.Double;
using Byte = System.Byte;

namespace Database.PostgreSql
{
    public partial class PostgreSqlDataAccess : IDataAccess
    {
        public List<String> SelectManyMailAttachmentForNameById(Int Id)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT ""name"" FROM ""mailattachments"" WHERE ""id"" = @id";
            Add(cmd, "id", Id);
            var l = new List<String>();
            using (var dr = cmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    var v = GetString(dr, "Name");
                    l.Add(v);
                }
            }
            return l;
        }
    }
}
