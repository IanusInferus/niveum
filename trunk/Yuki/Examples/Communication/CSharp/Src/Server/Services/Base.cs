using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Communication.BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IApplicationServer
    {
        public event Action<ErrorEvent> Error;

        public ServerTimeReply ServerTime(ServerTimeRequest r)
        {
            return ServerTimeReply.CreateSuccess(DateTime.UtcNow.DateTimeUtcToString());
        }

        public QuitReply Quit(QuitRequest r)
        {
            c.RaiseQuit();
            return QuitReply.CreateSuccess();
        }
    }
}
