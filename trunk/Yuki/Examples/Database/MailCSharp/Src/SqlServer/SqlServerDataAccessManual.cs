using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Database.Database;
using Boolean = System.Boolean;
using String = System.String;
using Type = System.Type;
using Int = System.Int32;
using Real = System.Double;
using Byte = System.Byte;

namespace Database.SqlServer
{
    public partial class SqlServerDataAccess : IDataAccess
    {
        public List<String> SelectManyMailAttachmentForNameById(Int Id)
        {
            var cmd = CreateTextCommand();
            cmd.CommandText = @"SELECT [Name] FROM [MailAttachments] WHERE [Id] = @Id";
            Add(cmd, "Id", Id);
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
