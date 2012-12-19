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
            lock (c.SessionLock)
            {
                c.MessageReceived = e => { if (MessageReceived != null) { MessageReceived(e); } };
                c.TestMessageReceived = e => { if (TestMessageReceived != null) { TestMessageReceived(e); } };
            }
        }

        public void UnregisterCrossSessionEvents()
        {
            lock (c.SessionLock)
            {
                c.MessageReceived = null;
                c.TestMessageReceived = null;
            }
        }
    }
}
