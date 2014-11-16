using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace BaseSystem
{
    public class CountedThreadPool : IDisposable
    {
        private CancellationTokenSource TokenSource;
        private BlockingCollection<Action> WorkItems;
        private Thread[] Threads;

        public CountedThreadPool(String Name, int ThreadCount)
        {
            TokenSource = new CancellationTokenSource();

            //ConcurrentQueue在同一个线程既可以作为生产者又可以作为消费者，且调用的函数处理的内容很少时，存在scalability不好的问题
            //http://download.microsoft.com/download/B/C/F/BCFD4868-1354-45E3-B71B-B851CD78733D/PerformanceCharacteristicsOfThreadSafeCollection.pdf
            WorkItems = new BlockingCollection<Action>(new ConcurrentBag<Action>());

            var Token = TokenSource.Token;
            Threads = Enumerable.Range(0, ThreadCount).Select((i, t) => new Thread(() =>
            {
                Thread.CurrentThread.Name = Name + "[" + i.ToString() + "]";
                while (true)
                {
                    if (Token.IsCancellationRequested) { return; }
                    Action a;
                    try
                    {
                        a = WorkItems.Take(Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    a();
                }
            })).ToArray();
            foreach (var t in Threads)
            {
                t.Start();
            }
        }

        public void QueueUserWorkItem(Action WorkItem)
        {
            WorkItems.Add(WorkItem);
        }

        public void Dispose()
        {
            TokenSource.Cancel();
            foreach (var t in Threads)
            {
                t.Join();
            }

            WorkItems.Dispose();
            TokenSource.Dispose();
        }
    }
}
