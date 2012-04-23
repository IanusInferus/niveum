using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Communication.BaseSystem
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public class AsyncConsumer<T> : IDisposable
    {
        private ConcurrentQueue<T> Entries = new ConcurrentQueue<T>();
        private AutoResetEvent CheckHandle = new AutoResetEvent(false);
        private LockedVariable<Task> TaskValue = new LockedVariable<Task>(null);

        public AsyncConsumer()
        {
        }

        public void Push(T Entry)
        {
            Entries.Enqueue(Entry);
            CheckHandle.Set();
        }

        public void Start(Func<T, Boolean> SyncConsume)
        {
            TaskValue.Update
            (
                Task =>
                {
                    if (Task != null) { throw new InvalidOperationException(); }

                    var tt = new Task
                    (
                        () =>
                        {
                            while (true)
                            {
                                CheckHandle.WaitOne();
                                T e;
                                var DoExit = false;
                                while (Entries.TryDequeue(out e))
                                {
                                    if (!SyncConsume(e))
                                    {
                                        DoExit = true;
                                    }
                                }
                                if (DoExit)
                                {
                                    return;
                                }
                            }
                        },
                        TaskCreationOptions.LongRunning
                    );
                    tt.Start();
                    return tt;
                }
            );
        }

        public void Stop()
        {
            TaskValue.Update
            (
                Task =>
                {
                    if (Task == null) { return null; }
                    Task.Wait();
                    return null;
                }
            );
        }

        public void Dispose()
        {
            Stop();
            CheckHandle.Dispose();
        }
    }
}
