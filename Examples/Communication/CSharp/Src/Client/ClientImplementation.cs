using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Communication.Json;

namespace Client
{
    public class ClientImplementation : IClientImplementation<ClientContext>
    {
        public void Error(ClientContext c, ErrorEvent e)
        {
            c.DequeueCallback(e.CommandName);
            var m = "调用'" + e.CommandName + "'发生错误:" + e.Message;
            Console.WriteLine(m);
        }

        public void MessageReceived(ClientContext c, MessageReceivedEvent e)
        {
            Console.WriteLine(e.Content);
        }

        public void MessageReceivedAt1(ClientContext c, MessageReceivedAt1Event e)
        {
            throw new NotImplementedException();
        }
    }
}
