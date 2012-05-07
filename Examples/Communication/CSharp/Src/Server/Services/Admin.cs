using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Communication.BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IServerImplementation<SessionContext>
    {
        public ShutdownReply Shutdown(SessionContext c, ShutdownRequest r)
        {
            ServerContext.RaiseShutdown();
            return ShutdownReply.CreateSuccess();
        }
    }
}
