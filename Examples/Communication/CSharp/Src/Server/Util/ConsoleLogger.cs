using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Communication.BaseSystem;

namespace Server
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private AsyncConsumer<SessionLogEntry> AsyncConsumer = null;

        public void Start()
        {
            if (AsyncConsumer != null) { throw new InvalidOperationException(); }

            AsyncConsumer = new AsyncConsumer<SessionLogEntry>();
            AsyncConsumer.Start
            (
                e =>
                {
                    if (e == null) { return false; }
                    foreach (var Line in FileLoggerSync.GetLines(e))
                    {
                        Console.WriteLine(Line);
                    }
                    return true;
                }
            );
        }

        /// <summary>只能在Start之后，Stop之前调用，线程安全</summary>
        public void Push(SessionLogEntry e)
        {
            AsyncConsumer.Push(e);
        }

        public void Dispose()
        {
            if (AsyncConsumer != null)
            {
                AsyncConsumer.Push(null);
                AsyncConsumer.Stop();
                AsyncConsumer.Dispose();
                AsyncConsumer = null;
            }
        }
    }
}
