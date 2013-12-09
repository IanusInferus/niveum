using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IApplicationServer
    {
        public event Action<ErrorEvent> Error;
        public event Action<ErrorCommandEvent> ErrorCommand;

        public ServerTimeReply ServerTime(ServerTimeRequest r)
        {
            return ServerTimeReply.CreateSuccess(DateTime.UtcNow.DateTimeUtcToString());
        }

        public QuitReply Quit(QuitRequest r)
        {
            SessionContext.RaiseQuit();
            return QuitReply.CreateSuccess();
        }

        public CheckSchemaVersionReply CheckSchemaVersion(CheckSchemaVersionRequest r)
        {
            if (r.Hash == ServerContext.HeadCommunicationSchemaHash)
            {
                SessionContext.SessionLock.AcquireWriterLock(int.MaxValue);
                try
                {
                    SessionContext.Version = "";
                }
                finally
                {
                    SessionContext.SessionLock.ReleaseWriterLock();
                }
                return CheckSchemaVersionReply.CreateHead();
            }
            if (ServerContext.CommunicationSchemaHashToVersion.ContainsKey(r.Hash))
            {
                String Version = ServerContext.CommunicationSchemaHashToVersion[r.Hash];
                SessionContext.SessionLock.AcquireWriterLock(int.MaxValue);
                try
                {
                    SessionContext.Version = Version;
                }
                finally
                {
                    SessionContext.SessionLock.ReleaseWriterLock();
                }
                return CheckSchemaVersionReply.CreateSupported();
            }
            return CheckSchemaVersionReply.CreateNotSupported();
        }

        public event Action<ServerShutdownEvent> ServerShutdown;
    }
}
