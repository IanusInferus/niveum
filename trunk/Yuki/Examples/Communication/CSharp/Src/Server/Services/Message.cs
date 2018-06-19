using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

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
            if (r.Content == "login")
            {
                SessionContext.RaiseAuthenticated();
                return SendMessageReply.CreateSuccess();
            }
            else if (r.Content == "secure")
            {
                //生成测试用确定Key
                var ServerToken = Enumerable.Range(0, 41).Select(i => (Byte)(i)).ToArray();
                var ClientToken = Enumerable.Range(0, 41).Select(i => (Byte)(40 - i)).ToArray();
                SessionContext.RaiseSecureConnectionRequired(new SecureContext { ServerToken = ServerToken, ClientToken = ClientToken });
                return SendMessageReply.CreateSuccess();
            }
            SessionContext.SendMessageCount += 1;
            var Sessions = ServerContext.Sessions.ToList();
            foreach (var rc in Sessions)
            {
                rc.SessionLock.EnterWriteLock();
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
                    rc.SessionLock.ExitWriteLock();
                }
            }
            return SendMessageReply.CreateSuccess();
        }

        public event Action<MessageReceivedEvent> MessageReceived;
    }
}
