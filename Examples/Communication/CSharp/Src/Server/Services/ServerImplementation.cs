using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public partial class ServerImplementation : IServerImplementation<SessionContext>
    {
        private ServerContext ServerContext;

        public ServerImplementation(ServerContext ServerContext)
        {
            this.ServerContext = ServerContext;
        }
        public void RaiseError(SessionContext c, String CommandName, String Message)
        {
            if (Error != null) { Error(c, new ErrorEvent { CommandName = CommandName, Message = Message }); }
        }
    }
}
