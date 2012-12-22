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
            c.SessionLock.AcquireWriterLock(int.MaxValue);
            try
            {
                c.MessageReceived = e => { if (MessageReceived != null) { MessageReceived(e); } };
                c.TestMessageReceived = e => { if (TestMessageReceived != null) { TestMessageReceived(e); } };
            }
            finally
            {
                c.SessionLock.ReleaseWriterLock();
            }
        }

        public void UnregisterCrossSessionEvents()
        {
            c.SessionLock.AcquireWriterLock(int.MaxValue);
            try
            {
                c.MessageReceived = null;
                c.TestMessageReceived = null;
            }
            finally
            {
                c.SessionLock.ReleaseWriterLock();
            }
        }
    }
}
