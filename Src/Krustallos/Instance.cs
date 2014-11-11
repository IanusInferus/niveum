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
        private Version MinReaderVersion = new Version(0);
        private SortedDictionary<Version, int> ReaderCounts = new SortedDictionary<Version, int>();
        private Object WriterAllocateLockee = new Object();
        private Version CurrentWriterVersion = new Version(0);
        private Version MinWriterVersion = new Version(0);
        private Version MaxCommittedWriterVersion = new Version(0);
        private ImmutableSortedDictionary<Version, Unit> WriterExists = new ImmutableSortedDictionary<Version, Unit>();
        private SortedDictionary<Version, int> WriterReferenceCounts = new SortedDictionary<Version, int>();
        private Object ToBeRemovedLockee = new Object();
        private SortedDictionary<Version, HashSet<IVersionedPartition>> ToBeRemoved = new SortedDictionary<Version, HashSet<IVersionedPartition>>();
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
            var oMinReaderVersion = Optional<Version>.Empty;
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
                        oMinReaderVersion = MinReaderVersion;
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
            if (oMinReaderVersion.OnHasValue)
            {
                var CurrentMinReaderVersion = oMinReaderVersion.Value;
                Version CurrentMinWriterVersion;
                lock (WriterAllocateLockee)
                {
                    CurrentMinWriterVersion = MinWriterVersion;
                }
                RemoveOldVersions(CurrentMinReaderVersion, CurrentMinWriterVersion);
            }
        }
        public ImmutableSortedDictionary<Version, Unit> TakePendingWriterVesions()
        {
            lock (WriterAllocateLockee)
            {
                foreach (var v in WriterExists)
                {
                    WriterReferenceCounts[v.Key] += 1;
                }
                return WriterExists;
            }
        }
        public void ReturnPendingWriterVesions(ImmutableSortedDictionary<Version, Unit> WriterExists)
        {
            Version CurrentMinWriterVersion;
            lock (WriterAllocateLockee)
            {
                foreach (var v in WriterExists)
                {
                    var Count = WriterReferenceCounts[v.Key];
                    if (Count > 1)
                    {
                        WriterReferenceCounts[v.Key] = Count - 1;
                    }
                    else
                    {
                        WriterReferenceCounts.Remove(v.Key);
                    }
                }
                if (WriterReferenceCounts.Count > 0)
                {
                    MinWriterVersion = WriterReferenceCounts.First().Key;
                }
                else
                {
                    MinWriterVersion = CurrentWriterVersion;
                }
                CurrentMinWriterVersion = MinWriterVersion;
            }
            Version CurrentMinReaderVersion;
            lock (ReaderAllocateLockee)
            {
                CurrentMinReaderVersion = MinReaderVersion;
            }
            RemoveOldVersions(CurrentMinReaderVersion, CurrentMinWriterVersion);
        }

        private void RemoveOldVersions(Version CurrentMinReaderVersion, Version CurrentMinWriterVersion)
        {
            var Version = (CurrentMinWriterVersion - 1 < CurrentMinReaderVersion) ? (CurrentMinWriterVersion - 1) : CurrentMinReaderVersion;

            var Stores = new HashSet<IVersionedPartition>();
            lock (ToBeRemovedLockee)
            {
                var ToRemove = new List<Version>();
                foreach (var p in ToBeRemoved)
                {
                    if (p.Key >= Version) { break; }
                    foreach (var s in p.Value)
                    {
                        if (!Stores.Contains(s))
                        {
                            Stores.Add(s);
                        }
                    }
                    ToRemove.Add(p.Key);
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
                WriterExists = WriterExists.Add(CurrentWriterVersion, default(Unit));
                WriterReferenceCounts.Add(CurrentWriterVersion, 1);
                return CurrentWriterVersion;
            }
        }
        public void RevertWriterVersion(Version CommittingVersion)
        {
            lock (WriterAllocateLockee)
            {
                WriterExists = WriterExists.Remove(CommittingVersion);
                var Count = WriterReferenceCounts[CommittingVersion];
                if (Count > 1)
                {
                    WriterReferenceCounts[CommittingVersion] = Count - 1;
                }
                else
                {
                    WriterReferenceCounts.Remove(CommittingVersion);
                }
                if (WriterReferenceCounts.Count > 0)
                {
                    MinWriterVersion = WriterReferenceCounts.First().Key;
                }
                else
                {
                    MinWriterVersion = CurrentWriterVersion;
                }
            }
        }
        public void CommitWriterVersion(Version CommittingVersion, HashSet<IVersionedPartition> ToBeRemoved)
        {
            lock (ToBeRemovedLockee)
            {
                this.ToBeRemoved.Add(CommittingVersion, ToBeRemoved);
            }
            Version CurrentMaxCommittedWriterVersion;
            Version CurrentMinWriterVersion;
            lock (WriterAllocateLockee)
            {
                WriterExists = WriterExists.Remove(CommittingVersion);
                var Count = WriterReferenceCounts[CommittingVersion];
                if (Count > 1)
                {
                    WriterReferenceCounts[CommittingVersion] = Count - 1;
                }
                else
                {
                    WriterReferenceCounts.Remove(CommittingVersion);
                }
                MaxCommittedWriterVersion = CommittingVersion > MaxCommittedWriterVersion ? CommittingVersion : MaxCommittedWriterVersion;
                if (WriterReferenceCounts.Count > 0)
                {
                    MinWriterVersion = WriterReferenceCounts.First().Key;
                }
                else
                {
                    MinWriterVersion = CurrentWriterVersion;
                }
                CurrentMaxCommittedWriterVersion = MaxCommittedWriterVersion;
                CurrentMinWriterVersion = MinWriterVersion;
            }
            Version CurrentMinReaderVersion;
            lock (ReaderAllocateLockee)
            {
                CurrentReaderVersion = CurrentMaxCommittedWriterVersion;
                if (ReaderCounts.Count > 0)
                {
                    MinReaderVersion = ReaderCounts.First().Key;
                }
                else
                {
                    MinReaderVersion = CurrentReaderVersion;
                }
                CurrentMinReaderVersion = MinReaderVersion;
            }
            RemoveOldVersions(CurrentMinReaderVersion, CurrentMinWriterVersion);
        }
    }
}
