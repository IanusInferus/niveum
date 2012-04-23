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
    public class FileLogger : IDisposable
    {
        private AsyncConsumer<SessionLogEntry> AsyncConsumer = null;
        private Action Unbind = null;

        /// <param name="Path"></param>
        /// <param name="Bind">需要是线程安全的</param>
        /// <param name="Unbind">需要是线程安全的</param>
        public void Start(String Path, Action<Action<SessionLogEntry>> Bind, Action<Action<SessionLogEntry>> Unbind)
        {
            if (AsyncConsumer != null) { throw new InvalidOperationException(); }

            AsyncConsumer = new AsyncConsumer<SessionLogEntry>();
            Bind(AsyncConsumer.Push);
            this.Unbind = () => Unbind(AsyncConsumer.Push);
            AsyncConsumer.Start
            (
                e =>
                {
                    if (e == null) { return false; }
                    FileLoggerSync.WriteLog(Path, e);
                    return true;
                }
            );
        }

        public void Stop()
        {
            AsyncConsumer.Push(null);
            if (AsyncConsumer != null)
            {
                AsyncConsumer.Stop();
            }
            if (Unbind != null)
            {
                Unbind();
                Unbind = null;
            }
            if (AsyncConsumer != null)
            {
                AsyncConsumer.Dispose();
                AsyncConsumer = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
