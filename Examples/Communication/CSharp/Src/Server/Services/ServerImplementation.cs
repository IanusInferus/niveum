using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public partial class ServerImplementation : IApplicationServer
    {
        private ServerContext ServerContext;
        private SessionContext c;

        public ServerImplementation(ServerContext ServerContext, SessionContext c)
        {
            this.ServerContext = ServerContext;
            this.c = c;
        }

        public void RaiseError(String CommandName, String Message)
        {
            if (CommandName != "")
            {
                if (ErrorCommand != null) { ErrorCommand(new ErrorCommandEvent { CommandName = CommandName }); }
                if (Error != null) { Error(new ErrorEvent { Message = CommandName + ": " + Message }); }
            }
            else
            {
                if (Error != null) { Error(new ErrorEvent { Message = Message }); }
            }
        }
    }
}
