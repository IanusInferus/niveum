using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        public void RegisterCrossSessionEvents()
        {
            SessionContext.SessionLock.EnterWriteLock();
            try
            {
                SessionContext.EventPump = CreateEventPump(() => SessionContext.Version);
            }
            finally
            {
                SessionContext.SessionLock.ExitWriteLock();
            }
        }

        public void UnregisterCrossSessionEvents()
        {
            SessionContext.SessionLock.EnterWriteLock();
            try
            {
                SessionContext.EventPump = null;
            }
            finally
            {
                SessionContext.SessionLock.ExitWriteLock();
            }
        }
    }
}
