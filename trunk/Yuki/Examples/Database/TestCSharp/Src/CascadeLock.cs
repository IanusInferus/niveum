using System;
using System.Collections.Generic;
using System.Threading;

namespace BaseSystem
{
    /// <summary>
    /// 本类的所有方法都是线程安全的。本类包含AutoResetEvent资源，请保证Enter和Exit的匹配。
    /// </summary>
    public class CascadeLock : ICascadeLock, IDisposable
    {
        private class Node
        {
            //锁定Node本身之后使用
            public int EnterCount = 0;
            //锁定Node本身之后使用
            public Dictionary<Object, Node> Children = new Dictionary<Object, Node>();

            //无需锁定Node本身也能使用
            public ReaderWriterLockSlim Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }
        private Node Root = new Node { };

        public void Enter(LinkedList<Object> LockList)
        {
            lock (Root)
            {
                Root.EnterCount += 1;
            }
            EnterLock(Root, LockList.First);
        }

        public void Exit(LinkedList<Object> LockList)
        {
            ExitLock(Root, LockList.First);
            lock (Root)
            {
                Root.EnterCount -= 1;
            }
        }

        private void EnterLock(Node n, LinkedListNode<Object> Head)
        {
            if (Head == null)
            {
                n.Lock.EnterWriteLock();
            }
            else
            {
                var Value = Head.Value;
                var Next = Head.Next;

                n.Lock.EnterReadLock();

                Node Child = null;
                lock (n)
                {
                    if (n.Children.ContainsKey(Value))
                    {
                        Child = n.Children[Value];
                        lock (Child)
                        {
                            Child.EnterCount += 1;
                        }
                    }
                    else
                    {
                        Child = new Node { };
                        Child.EnterCount += 1;
                        n.Children.Add(Value, Child);
                    }
                }

                EnterLock(Child, Next);
            }
        }
        private void ExitLock(Node n, LinkedListNode<Object> Head)
        {
            if (Head == null)
            {
                n.Lock.ExitWriteLock();
            }
            else
            {
                var Value = Head.Value;
                var Next = Head.Next;

                Node Child = null;
                lock (n)
                {
                    Child = n.Children[Value];
                }

                ExitLock(Child, Next);

                lock (n)
                {
                    bool ToRemove;
                    lock (Child)
                    {
                        Child.EnterCount -= 1;
                        ToRemove = (Child.EnterCount == 0);
                    }
                    if (ToRemove)
                    {
                        n.Children.Remove(Value);
                        Child.Lock.Dispose();
                        Child.Lock = null;
                    }
                }

                n.Lock.ExitReadLock();
            }
        }

        private void Dispose(Node n)
        {
            Dictionary<Object, Node> Children;
            lock (n)
            {
                n.Lock.Dispose();
                n.Lock = null;
                Children = n.Children;
                n.Children = null;
            }
            foreach (var Child in Children.Values)
            {
                Dispose(Child);
            }
        }

        public void Dispose()
        {
            Dispose(Root);
        }
    }
}