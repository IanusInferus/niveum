using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public partial class ServerImplementation : IApplicationServer, IServerImplementation, IDisposable
    {
        private ServerContext ServerContext;
        private SessionContext SessionContext;

        public ServerImplementation(ServerContext ServerContext, SessionContext SessionContext)
        {
            this.ServerContext = ServerContext;
            this.SessionContext = SessionContext;
            RegisterCrossSessionEvents();
        }

        public void RaiseError(String CommandName, String Message)
        {
            if (CommandName != "")
            {
                SessionContext.EventPump.ErrorCommand(new ErrorCommandEvent { CommandName = CommandName, Message = Message });
            }
            else
            {
                SessionContext.EventPump.Error(new ErrorEvent { Message = Message });
            }
        }

        public void Dispose()
        {
            UnregisterCrossSessionEvents();
        }
    }
}
