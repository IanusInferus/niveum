using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        public void RegisterCrossSessionEvents()
        {
            SessionContext.SessionLock.AcquireWriterLock(int.MaxValue);
            try
            {
                SessionContext.EventPump = CreateEventPump(() => SessionContext.Version);
            }
            finally
            {
                SessionContext.SessionLock.ReleaseWriterLock();
            }
        }

        public void UnregisterCrossSessionEvents()
        {
            SessionContext.SessionLock.AcquireWriterLock(int.MaxValue);
            try
            {
                SessionContext.EventPump = null;
            }
            finally
            {
                SessionContext.SessionLock.ReleaseWriterLock();
            }
        }
    }
}
