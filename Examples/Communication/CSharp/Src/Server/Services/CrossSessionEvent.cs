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
                SessionContext.MessageReceived = e => { if (MessageReceived != null) { MessageReceived(e); } };
                SessionContext.TestMessageReceived = e => { if (TestMessageReceived != null) { TestMessageReceived(e); } };
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
                SessionContext.MessageReceived = null;
                SessionContext.TestMessageReceived = null;
            }
            finally
            {
                SessionContext.SessionLock.ReleaseWriterLock();
            }
        }
    }
}
