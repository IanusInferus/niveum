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
                        Version NewMinReaderVersion;
                        if (ReaderCounts.Count > 0)
                        {
                            NewMinReaderVersion = ReaderCounts.First().Key;
                        }
                        else
                        {
                            NewMinReaderVersion = CurrentReaderVersion;
                        }
                        if (NewMinReaderVersion < MinReaderVersion) { throw new InvalidOperationException(); }
                        MinReaderVersion = NewMinReaderVersion;
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
            if (oMinReaderVersion.OnSome)
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
            //CurrentMinWriterVersion可能回滚，所以需要保留到CurrentMinWriterVersion前的一个版本
            var Version = (CurrentMinWriterVersion - 1 < CurrentMinReaderVersion) ? (CurrentMinWriterVersion - 1) : CurrentMinReaderVersion;

            var Partitions = new HashSet<IVersionedPartition>();
            lock (ToBeRemovedLockee)
            {
                var ToRemove = new List<Version>();
                foreach (var Pair in ToBeRemoved)
                {
                    if (Pair.Key >= Version) { break; }
                    foreach (var p in Pair.Value)
                    {
                        if (!Partitions.Contains(p))
                        {
                            Partitions.Add(p);
                        }
                    }
                    ToRemove.Add(Pair.Key);
                }
                foreach (var v in ToRemove)
                {
                    ToBeRemoved.Remove(v);
                }
            }
            foreach (var p in Partitions)
            {
                p.RemovePreviousVersions(Version);
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
                CurrentReaderVersion = CurrentMaxCommittedWriterVersion > CurrentReaderVersion ? CurrentMaxCommittedWriterVersion : CurrentReaderVersion;
                Version NewMinReaderVersion;
                if (ReaderCounts.Count > 0)
                {
                    NewMinReaderVersion = ReaderCounts.First().Key;
                }
                else
                {
                    NewMinReaderVersion = CurrentReaderVersion;
                }
                if (NewMinReaderVersion < MinReaderVersion) { throw new InvalidOperationException(); }
                MinReaderVersion = NewMinReaderVersion;
                CurrentMinReaderVersion = MinReaderVersion;
            }
            RemoveOldVersions(CurrentMinReaderVersion, CurrentMinWriterVersion);
        }
    }
}
