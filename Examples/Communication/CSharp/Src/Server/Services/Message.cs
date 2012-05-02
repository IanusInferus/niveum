using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Communication.BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IServerImplementation<SessionContext>
    {
        public SendMessageReply SendMessage(SessionContext c, SendMessageRequest r)
        {
            if (r.Content.Length > 256)
            {
                return SendMessageReply.CreateTooLong();
            }
            c.SendMessageCount += 1;
            foreach (var rc in ServerContext.GetSessions())
            {
                rc.SessionLock.AcquireWriterLock(int.MaxValue);
                try
                {
                    rc.ReceivedMessageCount += 1;
                }
                finally
                {
                    rc.SessionLock.ReleaseWriterLock();
                }
                if (MessageReceived != null)
                {
                    MessageReceived(rc, new MessageReceivedEvent { Content = r.Content });
                }
            }
            return SendMessageReply.CreateSuccess();
        }

        public event Action<SessionContext, MessageReceivedEvent> MessageReceived;

        public SendMessageAt1Reply SendMessageAt1(SessionContext c, SendMessageAt1Request r)
        {
            if (MessageReceivedAt1 != null)
            {
                MessageReceivedAt1(c, new MessageReceivedAt1Event { Title = "System Updated", Lines = new List<String> { "Please update your client to a recent version." } });
            }
            return SendMessageAt1Reply.CreateSuccess();
        }

        public event Action<SessionContext, MessageReceivedAt1Event> MessageReceivedAt1;
    }
}
