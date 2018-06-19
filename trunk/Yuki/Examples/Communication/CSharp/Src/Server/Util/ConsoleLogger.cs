using System;
using BaseSystem;

namespace Server
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private Action<Action> QueueUserWorkItem;
        private AsyncConsumer<SessionLogEntry> AsyncConsumer = null;

        public ConsoleLogger(Action<Action> QueueUserWorkItem)
        {
            this.QueueUserWorkItem = QueueUserWorkItem;
        }

        public void Start()
        {
            if (AsyncConsumer != null) { throw new InvalidOperationException(); }

            AsyncConsumer = new AsyncConsumer<SessionLogEntry>
            (
                QueueUserWorkItem,
                e =>
                {
                    if (e == null) { return false; }
                    try
                    {
                        foreach (var Line in FileLoggerSync.GetLines(e))
                        {
                            Console.WriteLine(Line);
                        }
                    }
                    catch
                    {
                    }
                    return true;
                },
                1
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
                AsyncConsumer.Dispose();
                AsyncConsumer = null;
            }
        }
    }
}
