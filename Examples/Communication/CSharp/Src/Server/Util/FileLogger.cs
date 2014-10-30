using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseSystem;

namespace Server
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public class FileLogger : ILogger
    {
        private Action<Action> QueueUserWorkItem;
        private String Path;
        private AsyncConsumer<SessionLogEntry> AsyncConsumer = null;

        public FileLogger(Action<Action> QueueUserWorkItem, String Path)
        {
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.Path = Path;
        }

        /// <param name="Path"></param>
        /// <param name="Bind">需要是线程安全的</param>
        /// <param name="Unbind">需要是线程安全的</param>
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
                        FileLoggerSync.WriteLog(Path, e);
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
