using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace BaseSystem
{
    public class CountedThreadPool : IDisposable
    {
        private String Name;
        private ManualResetEvent ThreadPoolExit;
        private AutoResetEvent WorkItemAdded;
        private ConcurrentQueue<Action> WorkItems;
        private WaitHandle[] WaitHandles;
        private Thread[] Threads;

        public CountedThreadPool(String Name, int ThreadCount)
        {
            ThreadPoolExit = new ManualResetEvent(false);
            WorkItemAdded = new AutoResetEvent(false);
            WorkItems = new ConcurrentQueue<Action>();

            WaitHandles = new WaitHandle[] { ThreadPoolExit, WorkItemAdded };
            Threads = Enumerable.Range(0, ThreadCount).Select((i, t) => new Thread(() =>
            {
                Thread.CurrentThread.Name = Name + "[" + i.ToString() + "]";
                while (true)
                {
                    var Result = WaitHandle.WaitAny(WaitHandles);
                    if (Result == 0) { break; }
                    Action a;
                    while (WorkItems.TryDequeue(out a))
                    {
                        a();
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
            ThreadPoolExit.Set();
            foreach (var t in Threads)
            {
                t.Join();
            }

            WorkItemAdded.Dispose();
            ThreadPoolExit.Dispose();
        }
    }
}
