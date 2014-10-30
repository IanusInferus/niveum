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
        private ManualResetEvent ThreadPoolExit;
        private AutoResetEvent WorkItemAdded;
        private ConcurrentQueue<Action> WorkItems;
        private WaitHandle[] WaitHandles;
        private Thread[] Threads;

        public CountedThreadPool(String Name, int ThreadCount)
        {
            TokenSource = new CancellationTokenSource();
            ThreadPoolExit = new ManualResetEvent(false);
            WorkItemAdded = new AutoResetEvent(false);
            WorkItems = new ConcurrentQueue<Action>();

            var Token = TokenSource.Token;
            WaitHandles = new WaitHandle[] { ThreadPoolExit, WorkItemAdded };
            Threads = Enumerable.Range(0, ThreadCount).Select((i, t) => new Thread(() =>
            {
                Thread.CurrentThread.Name = Name + "[" + i.ToString() + "]";
                while (true)
                {
                    var Result = WaitHandle.WaitAny(WaitHandles);
                    if (Result == 0) { break; }
                    int EmptyCount = 0;
                    while (true)
                    {
                        if (Token.IsCancellationRequested) { return; }
                        Action a;
                        while (WorkItems.TryDequeue(out a))
                        {
                            EmptyCount = 0;
                            a();
                        }
                        EmptyCount += 1;
                        if (EmptyCount > 128) { break; }
                        Thread.SpinWait(1 + EmptyCount);
                    }
                }
            })).ToArray();
            foreach (var t in Threads)
            {
                t.Start();
            }
        }

        public void QueueUserWorkItem(Action WorkItem)
        {
            WorkItems.Enqueue(WorkItem);
            WorkItemAdded.Set();
        }

        public void Dispose()
        {
            TokenSource.Cancel();
            ThreadPoolExit.Set();
            foreach (var t in Threads)
            {
                t.Join();
            }

            WorkItemAdded.Dispose();
            ThreadPoolExit.Dispose();
            TokenSource.Dispose();
        }
    }
}
