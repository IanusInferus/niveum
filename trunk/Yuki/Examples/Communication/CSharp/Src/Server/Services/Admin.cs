using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    public partial class ServerImplementation : IApplicationServer
    {
        public ShutdownReply Shutdown(ShutdownRequest r)
        {
            ServerContext.RaiseShutdown();
            return ShutdownReply.CreateSuccess();
        }
    }
}
