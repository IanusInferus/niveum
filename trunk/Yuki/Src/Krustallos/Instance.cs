using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Krustallos
{
    /// <summary>
    /// Krustallos 内存事务数据库
    /// 支持只读事务多版本镜像访问(MVCC)
    /// 支持读写事务两阶段提交(2PL)
    /// 支持悲观并发控制(Pessimistic Concurrency Control)
    /// 写需要锁定整个事务写入的所有表
    /// 不支持自增字段
    /// </summary>
    public class Instance
    {
        private Object ReaderAllocateLockee = new Object();
        private Version CurrentReaderVersion = new Version(0);
        private SortedDictionary<Version, int> ReaderCounts = new SortedDictionary<Version, int>();
        private Object WriterAllocateLockee = new Object();
        private Version CurrentWriterVersion = new Version(0);
        private SortedDictionary<Version, Unit> WriterExists = new SortedDictionary<Version, Unit>();
        private Object ToBeRemovedLockee = new Object();
        private SortedDictionary<Version, HashSet<IVersionedStore>> ToBeRemoved = new SortedDictionary<Version, HashSet<IVersionedStore>>();
        public Version TakeReaderVersion()
        {
            lock (ReaderAllocateLockee)
            {
                if (ReaderCounts.ContainsKey(CurrentReaderVersion))
                {
                    ReaderCounts[CurrentReaderVersion] += 1;
                }
                else
                {
                    ReaderCounts.Add(CurrentReaderVersion, 1);
                }
                return CurrentReaderVersion;
            }
        }
        public void ReturnReaderVersion(Version CommittedVersion)
        {
            var MinReaderVersion = Optional<Version>.Empty;
            lock (ReaderAllocateLockee)
            {
                if (ReaderCounts.ContainsKey(CommittedVersion))
                {
                    var Count = ReaderCounts[CommittedVersion];
                    Count -= 1;
                    if (Count <= 0)
                    {
                        ReaderCounts.Remove(CommittedVersion);
                        if (ReaderCounts.Count > 0)
                        {
                            MinReaderVersion = ReaderCounts.First().Key;
                        }
                        else
                        {
                            MinReaderVersion = CurrentReaderVersion;
                        }
                    }
                    else
                    {
                        ReaderCounts[CommittedVersion] = Count;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            if (MinReaderVersion.OnHasValue)
            {
                RemoveOldVersions(MinReaderVersion.Value);
            }
        }

        private void RemoveOldVersions(Krustallos.Version Version)
        {
            var Stores = new HashSet<IVersionedStore>();
            lock (ToBeRemovedLockee)
            {
                var ToRemove = new List<Version>();
                foreach (var p in ToBeRemoved)
                {
                    foreach (var s in p.Value)
                    {
                        if (!Stores.Contains(s))
                        {
                            Stores.Add(s);
                        }
                    }
                    ToRemove.Add(p.Key);
                    if (p.Key == Version) { break; }
                }
                foreach (var v in ToRemove)
                {
                    ToBeRemoved.Remove(v);
                }
            }
            foreach (var s in Stores)
            {
                s.RemovePreviousVersions(Version);
            }
        }
        public Version CreateWriterVersion()
        {
            lock (WriterAllocateLockee)
            {
                CurrentWriterVersion += 1;
                WriterExists.Add(CurrentWriterVersion, default(Unit));
                return CurrentWriterVersion;
            }
        }
        public void RevertWriterVersion(Version CommittingVersion)
        {
            Version MaxCommittedVersion;
            lock (WriterAllocateLockee)
            {
                WriterExists.Remove(CurrentWriterVersion);
                if (WriterExists.Count > 0)
                {
                    MaxCommittedVersion = WriterExists.First().Key - 1;
                }
                else
                {
                    MaxCommittedVersion = CurrentWriterVersion;
                }
            }
            lock (ReaderAllocateLockee)
            {
                CurrentReaderVersion = MaxCommittedVersion;
            }
        }
        public void CommitWriterVersion(Version CommittingVersion, HashSet<IVersionedStore> ToBeRemoved)
        {
            lock (ToBeRemovedLockee)
            {
                this.ToBeRemoved.Add(CommittingVersion, ToBeRemoved);
            }
            Version MaxCommittedVersion;
            lock (WriterAllocateLockee)
            {
                WriterExists.Remove(CurrentWriterVersion);
                if (WriterExists.Count > 0)
                {
                    MaxCommittedVersion = WriterExists.First().Key - 1;
                }
                else
                {
                    MaxCommittedVersion = CurrentWriterVersion;
                }
            }
            var MinReaderVersion = Optional<Version>.Empty;
            lock (ReaderAllocateLockee)
            {
                CurrentReaderVersion = MaxCommittedVersion;
                if (ReaderCounts.Count > 0)
                {
                    MinReaderVersion = ReaderCounts.First().Key;
                }
                else
                {
                    MinReaderVersion = CurrentReaderVersion;
                }
            }
            if (MinReaderVersion.OnHasValue)
            {
                RemoveOldVersions(MinReaderVersion.Value);
            }
        }
    }
}
