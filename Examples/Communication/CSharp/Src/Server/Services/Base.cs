using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Communication.BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IServerImplementation<SessionContext>
    {
        public event Action<SessionContext, ErrorEvent> Error;

        public ServerTimeReply ServerTime(SessionContext c, ServerTimeRequest r)
        {
            return ServerTimeReply.CreateSuccess(DateTime.UtcNow.DateTimeUtcToString());
        }

        public QuitReply Quit(SessionContext c, QuitRequest r)
        {
            c.RaiseQuit();
            return QuitReply.CreateSuccess();
        }
    }
}
