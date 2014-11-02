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
            WorkItems = new BlockingCollection<Action>();

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
