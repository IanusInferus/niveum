using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseSystem
{
    /// <summary>
    /// 本接口的所有方法都应实现为线程安全的。
    /// </summary>
    public interface ICascadeLock
    {
        void Enter(LinkedList<Object> LockList);
        void Exit(LinkedList<Object> LockList);
    }

    /// <summary>
    /// 本类的所有方法都不是线程安全的。
    /// </summary>
    public class TransactionLock
    {
        private class Node
        {
            public List<LinkedList<Object>> ExclusiveLockLists = new List<LinkedList<Object>>();
            public SortedSet<Object> ChildrenOrder = new SortedSet<Object>();
            public Dictionary<Object, Node> Children = new Dictionary<Object, Node>();
        }
        private Node Root = new Node { };
        private HashSet<LinkedList<Object>> LockLists = new HashSet<LinkedList<Object>>();

        private ICascadeLock CascadeLock;
        public TransactionLock(ICascadeLock CascadeLock)
        {
            this.CascadeLock = CascadeLock;
        }

        //多次重复锁定同一块区域不会对内部CascadeLock的那个区域加锁
        public void Enter(IEnumerable<Object> LockPath)
        {
            var LockList = new LinkedList<Object>(LockPath);
            if (EnterLock(Root, LockList, LockList.First))
            {
                CascadeLock.Enter(LockList);
            }
        }

        public void Exit(IEnumerable<Object> LockPath)
        {
            var LockList = new LinkedList<Object>(LockPath);
            if (ExitLock(Root, LockList.First))
            {
                CascadeLock.Exit(LockList);
            }
        }

        private bool EnterLock(Node n, LinkedList<Object> LockList, LinkedListNode<Object> Head)
        {
            bool NeedEnterCascadeLock;
            if (Head == null)
            {
                if (n == Root) { throw new InvalidOperationException(); }
                if (n.Children.Count != 0)
                {
                    throw new InvalidOperationException("EnterLockConflictWithSubLock");
                }
                if (!LockLists.Add(LockList))
                {
                    throw new InvalidOperationException();
                }
                NeedEnterCascadeLock = n.ExclusiveLockLists.Count == 0;
                n.ExclusiveLockLists.Add(LockList);
            }
            else
            {
                var Value = Head.Value;
                var Next = Head.Next;

                Node Child = null;
                if (n.Children.ContainsKey(Value))
                {
                    var Max = n.ChildrenOrder.Max;
                    var Result = n.ChildrenOrder.Comparer.Compare(Value, Max);
                    if (Result != 0)
                    {
                        throw new InvalidOperationException("InvalidLockingOrder");
                    }
                    Child = n.Children[Value];
                    NeedEnterCascadeLock = EnterLock(Child, LockList, Next);
                }
                else
                {
                    Child = new Node { };
                    if (n.Children.Count > 0)
                    {
                        var Max = n.ChildrenOrder.Max;
                        var Result = n.ChildrenOrder.Comparer.Compare(Value, Max);
                        if (Result == 0) { throw new InvalidOperationException(); }
                        if (Result < 0)
                        {
                            throw new InvalidOperationException("InvalidLockingOrder");
                        }
                    }
                    NeedEnterCascadeLock = EnterLock(Child, LockList, Next);
                    n.ChildrenOrder.Add(Value);
                    n.Children.Add(Value, Child);
                }
            }
            return NeedEnterCascadeLock;
        }
        private bool ExitLock(Node n, LinkedListNode<Object> Head)
        {
            bool NeedExitCascadeLock;
            if (Head == null)
            {
                if (n == Root) { throw new InvalidOperationException(); }
                if (n.Children.Count != 0)
                {
                    throw new InvalidOperationException("ExitLockConflictWithSubLock");
                }
                if (n.ExclusiveLockLists.Count == 0)
                {
                    throw new InvalidOperationException("ExitLockNotMatched");
                }
                var l = n.ExclusiveLockLists.Last();
                if (!LockLists.Remove(l))
                {
                    throw new InvalidOperationException();
                }
                n.ExclusiveLockLists.RemoveAt(n.ExclusiveLockLists.Count - 1);
                NeedExitCascadeLock = n.ExclusiveLockLists.Count == 0;
            }
            else
            {
                var Value = Head.Value;
                var Next = Head.Next;

                Node Child = null;
                if (!n.Children.ContainsKey(Value))
                {
                    throw new InvalidOperationException("ExitLockNotMatched");
                }
                Child = n.Children[Value];
                NeedExitCascadeLock = ExitLock(Child, Next);
                if ((Child.ExclusiveLockLists.Count == 0) && (Child.Children.Count == 0))
                {
                    if (!n.ChildrenOrder.Remove(Value))
                    {
                        throw new InvalidOperationException();
                    }
                    n.Children.Remove(Value);
                }
            }
            return NeedExitCascadeLock;
        }

        public void ExitAll()
        {
            var LockLists = this.LockLists.ToList();
            foreach (var LockList in LockLists)
            {
                if (ExitLock(Root, LockList.First))
                {
                    CascadeLock.Exit(LockList);
                }
            }
        }

        public void Dispose()
        {
            ExitAll();
        }
    }
}