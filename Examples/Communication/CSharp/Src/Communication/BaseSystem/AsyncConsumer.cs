using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BaseSystem
{
    /// <summary>
    /// 本类的所有公共成员均是线程安全的。
    /// </summary>
    public class AsyncConsumer<T> : IDisposable
    {
        private Action<Action> QueueUserWorkItem;
        private Func<T, Boolean> DoConsume;
        private int MaxConsumerCount;
        private Queue<T> Entries = new Queue<T>();
        private int RunningCount = 0;
        private Boolean IsExited = false;

        public AsyncConsumer(Action<Action> QueueUserWorkItem, Func<T, Boolean> DoConsume, int MaxConsumerCount)
        {
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.DoConsume = DoConsume;
            this.MaxConsumerCount = MaxConsumerCount;
        }

        public void Push(T Entry)
        {
            lock (Entries)
            {
                if (IsExited)
                {
                    return;
                }
                Entries.Enqueue(Entry);
                if (RunningCount < MaxConsumerCount)
                {
                    RunningCount += 1;
                    QueueUserWorkItem(Run);
                }
            }
        }

        private void Run()
        {
            T e;
            lock (Entries)
            {
                if (IsExited)
                {
                    RunningCount -= 1;
                    return;
                }
                if (Entries.Count > 0)
                {
                    e = Entries.Dequeue();
                }
                else
                {
                    RunningCount -= 1;
                    return;
                }
            }
            if (!DoConsume(e))
            {
                lock (Entries)
                {
                    IsExited = true;
                    RunningCount -= 1;
                }
                return;
            }
            QueueUserWorkItem(Run);
        }

        public void DoOne()
        {
            T e;
            lock (Entries)
            {
                if (IsExited)
                {
                    return;
                }
                if (Entries.Count > 0)
                {
                    e = Entries.Dequeue();
                }
                else
                {
                    return;
                }
                if (!DoConsume(e))
                {
                    IsExited = true;
                }
            }
        }

        public void Dispose()
        {
            lock (Entries)
            {
                while (!IsExited && Entries.Count > 0)
                {
                    var e = Entries.Dequeue();
                    if (!DoConsume(e))
                    {
                        IsExited = true;
                        return;
                    }
                }
                IsExited = true;
            }
        }
    }
}
