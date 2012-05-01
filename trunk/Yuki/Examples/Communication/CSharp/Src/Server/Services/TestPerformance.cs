using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
using Communication.BaseSystem;

namespace Server.Services
{
    public partial class ServerImplementation : IServerImplementation<SessionContext>
    {
        public TestAddReply TestAdd(SessionContext c, TestAddRequest r)
        {
            return TestAddReply.CreateResult(r.Left + r.Right);
        }

        public TestMultiplyReply TestMultiply(SessionContext c, TestMultiplyRequest r)
        {
            var v = r.Operand;
            var o = 0.0;
            for (int k = 1; k <= 1000000; k += 1)
            {
                o += v * (k * 0.000001);
            }
            return TestMultiplyReply.CreateResult(o);
        }

        public TestTextReply TestText(SessionContext c, TestTextRequest r)
        {
            return TestTextReply.CreateResult(r.Text);
        }

        public TestMessageReply TestMessage(SessionContext c, TestMessageRequest r)
        {
            var Sessions = ServerContext.Sessions;
            var m = new TestMessageReceivedEvent { Message = r.Message };
            foreach (var psc in Sessions)
            {
                if (psc == c) { continue; }
                if (TestMessageReceived != null)
                {
                    TestMessageReceived(psc, m);
                }
            }
            return TestMessageReply.CreateSuccess(Sessions.Count);
        }

        public event Action<SessionContext, TestMessageReceivedEvent> TestMessageReceived;
    }
}
