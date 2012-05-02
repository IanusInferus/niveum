using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Communication;
using Communication.Json;

namespace Client
{
    public class ClientImplementation : IClientImplementation<ClientContext>
    {
        public void Error(ClientContext c, ErrorEvent e)
        {
            try
            {
                c.DequeueCallback(e.CommandName);
            }
            catch (Exception)
            {
            }
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

        public void TestMessageReceived(ClientContext c, TestMessageReceivedEvent e)
        {
            var Done = false;
            lock (c.Lockee)
            {
                c.Sum += Int32.Parse(e.Message);
                c.Num -= 1;
                Done = c.Num == 0;
            }
            if (Done)
            {
                c.Completed();
            }
        }
    }
}
