using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Firefly;
using Firefly.Mapping;
using Firefly.Mapping.Binary;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Krustallos;
using Database.Database;
using Boolean = System.Boolean;
using String = System.String;
using Type = System.Type;
using Int = System.Int32;
using Real = System.Double;
using Byte = System.Byte;
using Int64 = System.Int64;
using Version = Krustallos.Version;

namespace Database.Krustallos
{
    public partial class KrustallosDataAccess : IDataAccess
    {
        public List<String> FromMailAttachmentSelectManyForNameById(Int64 Id)
        {
            return Transaction.CheckReaderVersioned(this.Data.MailAttachmentByIdAndName, 0, _d_ => _d_.Range(new Key(Id, KeyCondition.Min), new Key(Id, KeyCondition.Max)).Select(_p_ => _p_.Value).Select(_e_ => _e_.Name)).ToList();
        }
    }
}
