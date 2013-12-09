using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Net;
using Algorithms;
using Communication;

namespace Server
{
    public class SessionContext : ISessionContext
    {
        //单线程访问
        public void Dispose()
        {
        }

        //跨线程共享只读访问

        public event Action Quit; //跨线程事件(订阅者需要保证线程安全)
        public void RaiseQuit()
        {
            if (Quit != null) { Quit(); }
        }
        public event Action Authenticated; //跨线程事件(订阅者需要保证线程安全)
        public void RaiseAuthenticated()
        {
            if (Authenticated != null) { Authenticated(); }
        }

        public IPEndPoint RemoteEndPoint { get; set; }

        public Byte[] SessionToken = { };
        public String SessionTokenString { get { return Cryptography.BytesToHexString(SessionToken); } }

        public ReaderWriterLock SessionLock = new ReaderWriterLock(); //跨线程共享读写访问变量锁


        //跨线程共享读写访问，读写必须通过SessionLock

        public int ReceivedMessageCount = 0; //跨线程变量

        public String Version = "";
        public IEventPump EventPump;

        //单线程访问

        public DateTime RequestTime { get; set; }
        public int SendMessageCount = 0;
    }
}
