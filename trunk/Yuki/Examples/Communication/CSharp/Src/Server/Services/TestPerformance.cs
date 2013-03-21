using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IApplicationServer
    {
        public TestAddReply TestAdd(TestAddRequest r)
        {
            return TestAddReply.CreateResult(r.Left + r.Right);
        }

        public TestMultiplyReply TestMultiply(TestMultiplyRequest r)
        {
            var v = r.Operand;
            var o = 0.0;
            for (int k = 1; k <= 1000000; k += 1)
            {
                o += v * (k * 0.000001);
            }
            return TestMultiplyReply.CreateResult(o);
        }

        public TestTextReply TestText(TestTextRequest r)
        {
            return TestTextReply.CreateResult(r.Text);
        }

        public TestMessageReply TestMessage(TestMessageRequest r)
        {
            var Sessions = ServerContext.Sessions;
            var m = new TestMessageReceivedEvent { Message = r.Message };
            c.SendMessageCount += 1;
            foreach (var rc in Sessions)
            {
                if (rc == c) { continue; }
                rc.SessionLock.AcquireWriterLock(int.MaxValue);
                try
                {
                    rc.ReceivedMessageCount += 1;
                    if (rc.TestMessageReceived != null)
                    {
                        rc.TestMessageReceived(m);
                    }
                }
                finally
                {
                    rc.SessionLock.ReleaseWriterLock();
                }
            }
            return TestMessageReply.CreateSuccess(Sessions.Count);
        }

        public event Action<TestMessageReceivedEvent> TestMessageReceived;
    }
}
