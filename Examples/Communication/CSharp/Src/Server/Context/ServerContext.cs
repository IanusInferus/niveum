using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Server
{
    public class ServerContext
    {
        //跨线程共享只读访问

        public event Action Shutdown; //跨线程事件(订阅者需要保证线程安全)
        public void RaiseShutdown()
        {
            if (Shutdown != null) { Shutdown(); }
        }

        public Func<ICollection<SessionContext>> GetSessions;
        public ICollection<SessionContext> Sessions { get { return GetSessions(); } }
        public String SchemaHash;
    }
}
