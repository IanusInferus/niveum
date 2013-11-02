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
            foreach (var rc in ServerContext.Sessions)
            {
                rc.SessionLock.AcquireWriterLock(int.MaxValue);
                try
                {
                    rc.ReceivedMessageCount += 1;
                    if (rc.MessageReceived != null)
                    {
                        rc.MessageReceived(new MessageReceivedEvent { Content = r.Content });
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

        public SendMessageAt1Reply SendMessageAt1(SendMessageAt1Request r)
        {
            if (MessageReceivedAt1 != null)
            {
                MessageReceivedAt1(new MessageReceivedAt1Event { Title = "System Updated", Lines = new List<String> { "Please update your client to a recent version." } });
            }
            return SendMessageAt1Reply.CreateSuccess();
        }

        public event Action<MessageReceivedAt1Event> MessageReceivedAt1;
    }
}
