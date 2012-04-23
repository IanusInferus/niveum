using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Server
{
    public class ServerContext
    {
        public Func<ICollection<SessionContext>> GetSessions;
        public ICollection<SessionContext> Sessions { get { return GetSessions(); } }
        public String SchemaHash;
    }
}
