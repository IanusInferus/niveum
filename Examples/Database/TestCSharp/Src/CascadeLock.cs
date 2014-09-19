using System;
using System.Collections.Generic;
using System.Threading;

namespace BaseSystem
{
    /// <summary>
    /// 本类的所有方法都是线程安全的。本类包含AutoResetEvent资源，请保证Enter和Exit的匹配。
    /// </summary>
    public class CascadeLock : ICascadeLock
    {
        private class Node
        {
            //除Waiter变量外，其他变量均应在锁定之后使用
            public int EnterCount;
            public bool IsExclusive;
            public Dictionary<Object, Node> Children = new Dictionary<Object, Node>();

            public AutoResetEvent Waiter = new AutoResetEvent(false);
        }
        private Node Root = new Node { EnterCount = 1, IsExclusive = false };

        public void Enter(LinkedList<Object> LockList)
        {
            EnterLock(Root, LockList.First, false);
        }

        public void Exit(LinkedList<Object> LockList)
        {
            ExitLock(Root, LockList.First);
        }

        private void EnterLock(Node n, LinkedListNode<Object> Head, Boolean Taken)
        {
            if (Head == null)
            {
                if (n == Root) { throw new InvalidOperationException(); }
                var Locked = false;
                while (true)
                {
                    var Success = false;
                    lock (n)
                    {
                        if (!n.IsExclusive || Taken)
                        {
                            n.IsExclusive = true;
                            Locked = true;
                        }
                        if (Locked)
                        {
                            Success = n.Children.Count == 0;
                        }
                    }
                    if (Success)
                    {
                        break;
                    }
                    else
                    {
                        n.Waiter.WaitOne();
                    }
                }
            }
            else
            {
                var Value = Head.Value;
                var Next = Head.Next;

                while (true)
                {
                    Node Child = null;
                    var ChildTaken = false;

                    lock (n)
                    {
                        if (!n.IsExclusive || Taken)
                        {
                            if (n.Children.ContainsKey(Value))
                            {
                                var c = n.Children[Value];
                                lock (c)
                                {
                                    if (!c.IsExclusive)
                                    {
                                        c.EnterCount += 1;
                                        Child = c;
                                    }
                                }
                            }
                            else
                            {
                                Child = new Node { EnterCount = 1, IsExclusive = true };
                                n.Children.Add(Value, Child);
                                ChildTaken = true;
                            }
                        }
                    }

                    if (Child == null)
                    {
                        n.Waiter.WaitOne();
                    }
                    else
                    {
                        EnterLock(Child, Next, ChildTaken);
                        if (Taken)
                        {
                            lock (n)
                            {
                                n.IsExclusive = false;
                                n.Waiter.Set();
                            }
                        }
                        break;
                    }
                }
            }
        }
        private bool ExitLock(Node n, LinkedListNode<Object> Head)
        {
            if (Head == null)
            {
                if (n == Root) { throw new InvalidOperationException(); }
                lock (n)
                {
                    if (!n.IsExclusive) { throw new InvalidOperationException(); }
                    n.IsExclusive = false;
                    n.EnterCount -= 1;
                    if (n.EnterCount < 0) { throw new InvalidOperationException(); }
                    if (n.EnterCount == 0)
                    {
                        n.Waiter.Dispose();
                        n.Waiter = null;
                        return true;
                    }
                    else
                    {
                        n.Waiter.Set();
                        return false;
                    }
                }
            }
            else
            {
                var Value = Head.Value;
                var Next = Head.Next;

                lock (n)
                {
                    if (!n.Children.ContainsKey(Value)) { throw new InvalidOperationException(); }
                    var c = n.Children[Value];
                    var NeedRemove = ExitLock(c, Next);
                    if (NeedRemove)
                    {
                        n.Children.Remove(Value);
                    }
                    if (n == Root) { return false; }
                    n.EnterCount -= 1;
                    if (n.EnterCount < 0) { throw new InvalidOperationException(); }
                    if (n.EnterCount == 0)
                    {
                        n.Waiter.Dispose();
                        n.Waiter = null;
                        return true;
                    }
                    else
                    {
                        n.Waiter.Set();
                        return false;
                    }
                }
            }
        }
    }
}