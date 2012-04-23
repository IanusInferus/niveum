using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Server
{
    public class SessionLogEntry
    {
        public IPEndPoint RemoteEndPoint;
        public String Token;
        public DateTime Time;
        public String Type;
        public String Message;
    }
}
