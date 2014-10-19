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
        private ImmutableSortedDictionary<Version, int> ReaderCounts = new ImmutableSortedDictionary<Version, int>();
        private Object WriterAllocateLockee = new Object();
        private Version CurrentWriterVersion = new Version(0);
        private ImmutableSortedDictionary<Version, Unit> WriterExists = new ImmutableSortedDictionary<Version, Unit>();
        private Object ToBeRemovedLockee = new Object();
        private ImmutableSortedDictionary<Version, HashSet<IVersionedStore>> ToBeRemoved = new ImmutableSortedDictionary<Version, HashSet<IVersionedStore>>();
        public Version TakeReaderVersion()
        {
            lock (ReaderAllocateLockee)
            {
                if (ReaderCounts.ContainsKey(CurrentReaderVersion))
                {
                    ReaderCounts = ReaderCounts.SetItem(CurrentReaderVersion, ReaderCounts.TryGetValue(CurrentReaderVersion).Value + 1);
                }
                else
                {
                    ReaderCounts = ReaderCounts.Add(CurrentReaderVersion, 1);
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
                    var Count = ReaderCounts.TryGetValue(CommittedVersion).Value;
                    Count -= 1;
                    if (Count <= 0)
                    {
                        ReaderCounts = ReaderCounts.Remove(CommittedVersion);
                        var oPair = ReaderCounts.TryGetMinPair();
                        if (oPair.OnHasValue)
                        {
                            MinReaderVersion = oPair.Value.Key;
                        }
                        else
                        {
                            MinReaderVersion = CurrentReaderVersion;
                        }
                    }
                    else
                    {
                        ReaderCounts = ReaderCounts.SetItem(CommittedVersion, Count);
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
                foreach (var p in ToBeRemoved.Range(Optional<Version>.Empty, Version))
                {
                    foreach (var s in p.Value)
                    {
                        if (!Stores.Contains(s))
                        {
                            Stores.Add(s);
                        }
                    }
                    ToBeRemoved = ToBeRemoved.Remove(p.Key);
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
                WriterExists = WriterExists.Add(CurrentWriterVersion, default(Unit));
                return CurrentWriterVersion;
            }
        }
        public void RevertWriterVersion(Version CommittingVersion)
        {
            Version MaxCommittedVersion;
            lock (WriterAllocateLockee)
            {
                WriterExists = WriterExists.Remove(CurrentWriterVersion);
                var oPair = WriterExists.TryGetMinPair();
                if (oPair.OnHasValue)
                {
                    MaxCommittedVersion = oPair.Value.Key - 1;
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
                this.ToBeRemoved = this.ToBeRemoved.Add(CommittingVersion, ToBeRemoved);
            }
            Version MaxCommittedVersion;
            lock (WriterAllocateLockee)
            {
                WriterExists = WriterExists.Remove(CurrentWriterVersion);
                var oPair = WriterExists.TryGetMinPair();
                if (oPair.OnHasValue)
                {
                    MaxCommittedVersion = oPair.Value.Key - 1;
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
                var oPair = ReaderCounts.TryGetMinPair();
                if (oPair.OnHasValue)
                {
                    MinReaderVersion = oPair.Value.Key;
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
