using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Server.Services
{
    public partial class ServerImplementation
    {
        private void RegisterCrossSessionEvent()
        {
            lock (c.SessionLock)
            {
                c.MessageReceived += e => { if (MessageReceived != null) { MessageReceived(e); } };
                c.TestMessageReceived += e => { if (TestMessageReceived != null) { TestMessageReceived(e); } };
            }
        }
    }
}
