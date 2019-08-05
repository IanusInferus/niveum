using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Data;

namespace Krustallos
{
    public class Transaction : IDisposable
    {
        private Instance Instance;
        private Optional<Version> ReaderVersion;
        private Optional<Version> WriterVersion;
        private Optional<ImmutableSortedDictionary<Version, Unit>> PendingWriterVesions;
        private SortedDictionary<String[], SortedDictionary<int, UpdatePartitionInfo>> UpdateStores;
        private List<Func<Version, Boolean>> Updates;

        private class UpdatePartitionInfo
        {
            public IVersionedPartition Partition;
            public Object CurrentStateFromReaderVersion;
            public Action<Version> Revert;
        }

        private class StringArrayComparer : IComparer<String[]>
        {
            public int Compare(String[] x, String[] y)
            {
                var CommonLength = Math.Min(x.Length, y.Length);
                for (int k = 0; k < CommonLength; k += 1)
                {
                    var r = StringComparer.Ordinal.Compare(x[k], y[k]);
                    if (r != 0) { return r; }
                }
                if (x.Length < y.Length) { return -1; }
                if (x.Length > y.Length) { return 1; }
                return 0;
            }
        }

        public Transaction(Instance Instance, IsolationLevel IsolationLevel)
        {
            this.Instance = Instance;

            //支持的级别为Snapshot，需要配合predicative lock按照pessimistic concurrency模型使用，否则不能保证写入时原始数据的版本和读取数据时数据的版本一致
            //如果两个事务读取重叠的数据而做出不重叠的修改，可能导致事务无法看作序列化的(Serializable)，此时可以通过将部分读取数据手动加predicative lock视为写来保持数据的一致性
            //如果隔离级别比Snapshot低，则看作Snapshot
            if (!((IsolationLevel == System.Data.IsolationLevel.Snapshot)
                || (IsolationLevel == System.Data.IsolationLevel.RepeatableRead)
                || (IsolationLevel == System.Data.IsolationLevel.ReadCommitted)
                || (IsolationLevel == System.Data.IsolationLevel.ReadUncommitted)
                || (IsolationLevel == System.Data.IsolationLevel.Chaos)
                || (IsolationLevel == System.Data.IsolationLevel.Unspecified))
                )
            {
                throw new NotSupportedException();
            }

            UpdateStores = new SortedDictionary<String[], SortedDictionary<int, UpdatePartitionInfo>>(new StringArrayComparer());
            Updates = new List<Func<Version, Boolean>>();
        }

        public void Revert()
        {
            if (WriterVersion.OnSome)
            {
                Instance.RevertWriterVersion(WriterVersion.Value);
                WriterVersion = Optional<Version>.Empty;
            }
            if (ReaderVersion.OnSome)
            {
                Instance.ReturnReaderVersion(ReaderVersion.Value);
                ReaderVersion = Optional<Version>.Empty;
            }
            if (PendingWriterVesions.OnSome)
            {
                Instance.ReturnPendingWriterVesions(PendingWriterVesions.Value);
                PendingWriterVesions = Optional<ImmutableSortedDictionary<Version, Unit>>.Empty;
            }
        }

        public void Commit()
        {
            try
            {
                if (Updates.Count > 0)
                {
                    var Success = false;
                    var Locked = new List<Object>();
                    try
                    {
                        foreach (var ps in UpdateStores)
                        {
                            foreach (var pp in ps.Value)
                            {
                                var o = pp.Value.Partition;
                                Monitor.Enter(o);
                                Locked.Add(o);
                            }
                        }
                        if (WriterVersion.OnNone)
                        {
                            WriterVersion = Instance.CreateWriterVersion();
                        }
                        var WriterVersionValue = WriterVersion.Value;
                        foreach (var u in Updates)
                        {
                            u(WriterVersionValue);
                        }
                        Success = true;
                    }
                    finally
                    {
                        if (Success)
                        {
                            if (WriterVersion.OnSome)
                            {
                                Instance.CommitWriterVersion(WriterVersion.Value, new HashSet<IVersionedPartition>(UpdateStores.SelectMany(p => p.Value).Select(p => p.Value.Partition)));
                                WriterVersion = Optional<Version>.Empty;
                            }
                        }
                        else
                        {
                            if (WriterVersion.OnSome)
                            {
                                var WriterVersionValue = WriterVersion.Value;
                                foreach (var ps in UpdateStores)
                                {
                                    foreach (var pp in ps.Value)
                                    {
                                        var Revert = pp.Value.Revert;
                                        Revert(WriterVersionValue);
                                    }
                                }
                                Instance.RevertWriterVersion(WriterVersionValue);
                                WriterVersion = Optional<Version>.Empty;
                            }
                        }
                        foreach (var o in Locked)
                        {
                            Monitor.Exit(o);
                        }
                    }
                }
            }
            finally
            {
                if (ReaderVersion.OnSome)
                {
                    Instance.ReturnReaderVersion(ReaderVersion.Value);
                    ReaderVersion = Optional<Version>.Empty;
                }
                if (PendingWriterVesions.OnSome)
                {
                    Instance.ReturnPendingWriterVesions(PendingWriterVesions.Value);
                    PendingWriterVesions = Optional<ImmutableSortedDictionary<Version, Unit>>.Empty;
                }
            }
        }

        public void Dispose()
        {
            Revert();
        }

        public Version GetReaderVersion()
        {
            if (ReaderVersion.OnNone)
            {
                ReaderVersion = Instance.TakeReaderVersion();
            }
            return ReaderVersion.Value;
        }
        public ImmutableSortedDictionary<Version, Unit> GetPendingWriterVersions()
        {
            if (PendingWriterVesions.OnNone)
            {
                PendingWriterVesions = Instance.TakePendingWriterVesions();
            }
            return PendingWriterVesions.Value;
        }

        public TRet CheckReaderVersioned<T, TRet>(VersionedStore<T> Store, int PartitionIndex, Func<T, TRet> Selector)
        {
            var StorePath = Store.Path;
            if (UpdateStores.ContainsKey(StorePath))
            {
                var s = UpdateStores[StorePath];
                if (s.ContainsKey(PartitionIndex))
                {
                    var upi = s[PartitionIndex];
                    return Selector((T)(upi.CurrentStateFromReaderVersion));
                }
            }
            var Partition = Store.GetPartition(PartitionIndex);
            var ReaderVersion = GetReaderVersion();
            var Content = Partition.GetVersionContent(ReaderVersion, GetPendingWriterVersions());
            return Selector(Content);
        }
        public void UpdateVersioned<T>(VersionedStore<T> Store, int PartitionIndex, Func<T, T> Transformer)
        {
            var StorePath = Store.Path;
            var Partition = Store.GetPartition(PartitionIndex);
            Func<Version, Boolean> Update = WriterVersion =>
            {
                var Content = Partition.GetLastVersionContent();
                return Partition.TryPutLastVersion(WriterVersion, Transformer(Content));
            };
            if (UpdateStores.ContainsKey(StorePath))
            {
                var s = UpdateStores[StorePath];
                if (s.ContainsKey(PartitionIndex))
                {
                    var upi = s[PartitionIndex];
                    var ReaderVersionContent = (T)(upi.CurrentStateFromReaderVersion);
                    var CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                    upi.CurrentStateFromReaderVersion = CurrentStateFromReaderVersion;
                    Updates.Add(Update);
                    return;
                }
            }

            {
                var ReaderVersion = GetReaderVersion();
                var ReaderVersionContent = Partition.GetVersionContent(ReaderVersion);
                var CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                var upi = new UpdatePartitionInfo
                {
                    Partition = Partition,
                    CurrentStateFromReaderVersion = CurrentStateFromReaderVersion,
                    Revert = WriterVersion =>
                    {
                        Partition.RemoveVersion(WriterVersion);
                    }
                };
                if (UpdateStores.ContainsKey(StorePath))
                {
                    var s = UpdateStores[StorePath];
                    s.Add(PartitionIndex, upi);
                }
                else
                {
                    var s = new SortedDictionary<int, UpdatePartitionInfo>();
                    s.Add(PartitionIndex, upi);
                    UpdateStores.Add(StorePath, s);
                }
                Updates.Add(Update);
            }
        }
    }
}
