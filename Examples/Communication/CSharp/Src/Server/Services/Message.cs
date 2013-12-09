using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IApplicationServer
    {
        public SendMessageReply SendMessage(SendMessageRequest r)
        {
            if (r.Content.Length > 256)
            {
                return SendMessageReply.CreateTooLong();
            }
            SessionContext.SendMessageCount += 1;
            var Sessions = ServerContext.Sessions.ToList();
            foreach (var rc in Sessions)
            {
                rc.SessionLock.AcquireWriterLock(int.MaxValue);
                try
                {
                    rc.ReceivedMessageCount += 1;
                    if (rc.EventPump != null)
                    {
                        rc.EventPump.MessageReceived(new MessageReceivedEvent { Content = r.Content });
                    }
                }
                finally
                {
                    rc.SessionLock.ReleaseWriterLock();
                }
            }
            return SendMessageReply.CreateSuccess();
        }

        public event Action<MessageReceivedEvent> MessageReceived;
    }
}
