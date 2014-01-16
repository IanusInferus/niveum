using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using BaseSystem;

namespace Server
{
    /// <summary>
    /// 本类的公共成员均是线程安全的。
    /// </summary>
    public class SessionStateMachine<TRead, TWrite>
    {
        private class Context
        {
            //0 初始
            //1 空闲
            //2 写入中
            //3 执行中
            //4 结束
            public int State;

            public Boolean IsReadEnabled;
            public Boolean IsWriteEnabled;
            public Queue<TRead> Reads;
            public Queue<TWrite> Writes;
            public Boolean IsInRawRead;
            public Boolean IsReadShutDown;
            public Boolean IsWriteShutDown;
        }

        private class SessionActionQueue
        {
            public Boolean IsRunning;
            public LinkedList<Action> Queue;
        }

        private LockedVariable<Context> c;
        private LockedVariable<SessionActionQueue> ActionQueue = new LockedVariable<SessionActionQueue>(new SessionActionQueue { IsRunning = false, Queue = new LinkedList<Action>() });
        private Func<Exception, Boolean> IsKnownException;
        private Action<Exception, StackTrace> OnCriticalError;
        private Action OnShutdownRead;
        private Action OnShutdownWrite;
        private Action<TWrite, Action, Action> OnWrite;
        private Action<TRead, Action, Action> OnExecute;
        private Action<Action<TRead[]>, Action> OnStartRawRead;
        private Action OnExit;

        public SessionStateMachine(Func<Exception, Boolean> IsKnownException, Action<Exception, StackTrace> OnCriticalError, Action OnShutdownRead, Action OnShutdownWrite, Action<TWrite, Action, Action> OnWrite, Action<TRead, Action, Action> OnExecute, Action<Action<TRead[]>, Action> OnStartRawRead, Action OnExit)
        {
            this.IsKnownException = IsKnownException;
            this.OnCriticalError = OnCriticalError;
            this.OnShutdownRead = OnShutdownRead;
            this.OnShutdownWrite = OnShutdownWrite;
            this.OnWrite = OnWrite;
            this.OnExecute = OnExecute;
            this.OnStartRawRead = OnStartRawRead;
            this.OnExit = OnExit;
            c = new LockedVariable<Context>(new Context
            {
                State = 0,
                IsReadEnabled = true,
                IsWriteEnabled = true,
                Reads = new Queue<TRead>(),
                Writes = new Queue<TWrite>(),
                IsInRawRead = false,
                IsReadShutDown = false,
                IsWriteShutDown = false
            });
        }

        public void Start()
        {
            AddToActionQueue(Check);
        }

        private void Check()
        {
            Action AfterAction = null;
            c.DoAction(cc =>
            {
                if (cc.State == 0)
                {
                    cc.State = 1;
                    AfterAction = () => AddToActionQueue(Check);
                    return;
                }
                if (cc.State == 1)
                {
                    if (cc.IsWriteEnabled)
                    {
                        if (cc.Writes.Count > 0)
                        {
                            cc.State = 2;
                            var w = cc.Writes.Dequeue();
                            AfterAction = () => AddToActionQueue(() => Write(w));
                            return;
                        }
                    }
                    if (cc.IsReadEnabled)
                    {
                        if (cc.Reads.Count > 0)
                        {
                            cc.State = 3;
                            var r = cc.Reads.Dequeue();
                            AfterAction = () => AddToActionQueue(() => Execute(r));
                            return;
                        }
                        if (!cc.IsInRawRead)
                        {
                            cc.IsInRawRead = true;
                            AfterAction = () => OnStartRawRead(NotifyStartRawReadSuccess, NotifyStartRawReadFailure);
                            return;
                        }
                    }
                    else
                    {
                        if (!cc.IsReadShutDown)
                        {
                            cc.IsReadShutDown = true;
                            AfterAction = () =>
                            {
                                OnShutdownRead();
                                AddToActionQueue(Check);
                            };
                            return;
                        }
                        if (cc.IsWriteEnabled)
                        {
                            if (!cc.IsWriteShutDown)
                            {
                                cc.IsWriteEnabled = false;
                                cc.IsWriteShutDown = true;
                                AfterAction = () =>
                                {
                                    OnShutdownWrite();
                                    AddToActionQueue(Check);
                                };
                                return;
                            }
                        }
                        else
                        {
                            if (cc.IsReadShutDown && cc.IsWriteShutDown && !cc.IsInRawRead)
                            {
                                cc.State = 4;
                                AfterAction = OnExit;
                                return;
                            }
                        }
                    }
                }
            });

            if (AfterAction != null)
            {
                AfterAction();
            }
        }

        private void Write(TWrite w)
        {
            Action OnSuccess = () =>
            {
                c.DoAction(cc =>
                {
                    if (cc.State == 2)
                    {
                        cc.State = 1;
                    }
                });
                AddToActionQueue(Check);
            };
            Action OnFailure = NotifyFailure;
            OnWrite(w, OnSuccess, OnFailure);
        }
        private void Execute(TRead r)
        {
            Action OnSuccess = () =>
            {
                c.DoAction(cc =>
                {
                    if (cc.State == 3)
                    {
                        cc.State = 1;
                    }
                });
                AddToActionQueue(Check);
            };
            Action OnFailure = NotifyFailure;
            OnExecute(r, OnSuccess, OnFailure);
        }
        public void NotifyWrite(TWrite w)
        {
            c.DoAction(cc =>
            {
                if (cc.IsWriteEnabled)
                {
                    cc.Writes.Enqueue(w);
                }
            });
            AddToActionQueue(Check);
        }
        private void NotifyStartRawReadSuccess(TRead[] Reads)
        {
            c.DoAction(cc =>
            {
                if (cc.IsReadEnabled)
                {
                    foreach (var r in Reads)
                    {
                        cc.Reads.Enqueue(r);
                    }
                }
                cc.IsInRawRead = false;
            });
            AddToActionQueue(Check);
        }

        public void NotifyFailure()
        {
            var IsReadShutDown = false;
            var IsWriteShutDown = false;
            c.DoAction(cc =>
            {
                if (cc.State != 4)
                {
                    cc.State = 1;
                }
                cc.IsReadEnabled = false;
                cc.IsWriteEnabled = false;
                IsReadShutDown = cc.IsReadShutDown;
                cc.IsReadShutDown = true;
                IsWriteShutDown = cc.IsWriteShutDown;
                cc.IsWriteShutDown = true;
            });
            if (!IsReadShutDown)
            {
                OnShutdownRead();
            }
            if (!IsWriteShutDown)
            {
                OnShutdownWrite();
            }
            AddToActionQueue(Check);
        }
        private void NotifyStartRawReadFailure()
        {
            var IsReadShutDown = false;
            var IsWriteShutDown = false;
            c.DoAction(cc =>
            {
                if (cc.State != 4)
                {
                    cc.State = 1;
                }
                cc.IsReadEnabled = false;
                cc.IsWriteEnabled = false;
                IsReadShutDown = cc.IsReadShutDown;
                cc.IsReadShutDown = true;
                IsWriteShutDown = cc.IsWriteShutDown;
                cc.IsWriteShutDown = true;
                cc.IsInRawRead = false;
            });
            if (!IsReadShutDown)
            {
                OnShutdownRead();
            }
            if (!IsWriteShutDown)
            {
                OnShutdownWrite();
            }
            AddToActionQueue(Check);
        }

        public void NotifyExit()
        {
            c.DoAction(cc =>
            {
                if (cc.State != 4)
                {
                    cc.State = 1;
                }
                cc.IsReadEnabled = false;
            });
            AddToActionQueue(Check);
        }

        public Boolean IsExited()
        {
            return c.Check(cc => cc.State == 4);
        }

        public void AddToActionQueue(Action Action)
        {
            ActionQueue.DoAction
            (
                q =>
                {
                    q.Queue.AddLast(Action);
                    if (!q.IsRunning)
                    {
                        q.IsRunning = true;
                        ThreadPool.QueueUserWorkItem(o => ExecuteActionQueue());
                    }
                }
            );
        }
        private void ExecuteActionQueue()
        {
            while (true)
            {
                int Count = 16;
                Action a = null;
                ActionQueue.DoAction
                (
                    q =>
                    {
                        if (q.Queue.Count > 0)
                        {
                            a = q.Queue.First.Value;
                            q.Queue.RemoveFirst();
                        }
                        else
                        {
                            q.IsRunning = false;
                        }
                    }
                );
                if (a == null)
                {
                    return;
                }
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    a();
                }
                else
                {
                    try
                    {
                        a();
                    }
                    catch (Exception ex)
                    {
                        if (!IsKnownException(ex))
                        {
                            OnCriticalError(ex, new StackTrace(true));
                        }
                        NotifyFailure();
                    }
                }
                Count -= 1;
                if (Count == 0) { break; }
            }
            ThreadPool.QueueUserWorkItem(o => ExecuteActionQueue());
        }
    }
}
