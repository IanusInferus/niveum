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
        private SortedDictionary<String[], UpdateStoreInfo> UpdateStores;
        private List<Func<Version, Boolean>> Updates;

        private class UpdateStoreInfo
        {
            public IVersionedStore Store;
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
                    var r = x[k].CompareTo(y[k]);
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
            if (IsolationLevel != System.Data.IsolationLevel.ReadCommitted) { throw new NotSupportedException(); }
            UpdateStores = new SortedDictionary<String[], UpdateStoreInfo>(new StringArrayComparer());
            Updates = new List<Func<Version, Boolean>>();
        }

        public void Revert()
        {
            if (WriterVersion.OnHasValue)
            {
                Instance.RevertWriterVersion(WriterVersion.Value);
                WriterVersion = Optional<Version>.Empty;
            }
            if (ReaderVersion.OnHasValue)
            {
                Instance.ReturnReaderVersion(ReaderVersion.Value);
                ReaderVersion = Optional<Version>.Empty;
            }
        }

        public void Commit()
        {
            try
            {
                if (Updates.Count > 0)
                {
                    while (true)
                    {
                        var Success = false;
                        var Locked = new List<Object>();
                        try
                        {
                            foreach (var p in UpdateStores)
                            {
                                var o = p.Value.Store;
                                Monitor.Enter(o);
                                Locked.Add(o);
                            }
                            var WriterVersion = GetWriterVersion();
                            var UpdateSuccess = true;
                            foreach (var u in Updates)
                            {
                                if (!u(WriterVersion))
                                {
                                    UpdateSuccess = false;
                                    break;
                                }
                            }
                            if (!UpdateSuccess) { continue; }
                            Success = true;
                        }
                        finally
                        {
                            if (Success)
                            {
                                if (WriterVersion.OnHasValue)
                                {
                                    Instance.CommitWriterVersion(WriterVersion.Value, new HashSet<IVersionedStore>(UpdateStores.Select(p => p.Value.Store)));
                                    WriterVersion = Optional<Version>.Empty;
                                }
                            }
                            else
                            {
                                if (WriterVersion.OnHasValue)
                                {
                                    var WriterVersionValue = WriterVersion.Value;
                                    foreach (var p in UpdateStores)
                                    {
                                        var Revert = p.Value.Revert;
                                        Revert(WriterVersionValue);
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
                        if (Success) { break; }
                    }
                }
            }
            finally
            {
                if (ReaderVersion.OnHasValue)
                {
                    Instance.ReturnReaderVersion(ReaderVersion.Value);
                    ReaderVersion = Optional<Version>.Empty;
                }
            }
        }

        public void Dispose()
        {
            Revert();
        }

        public Version GetReaderVersion()
        {
            if (ReaderVersion.OnNotHasValue)
            {
                ReaderVersion = Instance.TakeReaderVersion();
            }
            return ReaderVersion.Value;
        }
        public Version GetWriterVersion()
        {
            if (WriterVersion.OnNotHasValue)
            {
                WriterVersion = Instance.CreateWriterVersion();
            }
            return WriterVersion.Value;
        }
        public ImmutableSortedDictionary<Version, Unit> GetPendingWriterVersions()
        {
            if (PendingWriterVesions.OnNotHasValue)
            {
                PendingWriterVesions = Instance.GetPendingWriterVesions();
            }
            return PendingWriterVesions.Value;
        }

        public TRet CheckReaderVersioned<T, TRet>(VersionedStore<T> Store, Func<T, TRet> Selector)
        {
            var StorePath = Store.Path;
            if (!UpdateStores.ContainsKey(StorePath))
            {
                var ReaderVersion = GetReaderVersion();
                var Content = Store.GetVersionContent(ReaderVersion, GetPendingWriterVersions());
                return Selector(Content);
            }
            else
            {
                var usi = UpdateStores[StorePath];
                return Selector((T)(usi.CurrentStateFromReaderVersion));
            }
        }
        public void UpdateVersioned<T>(VersionedStore<T> Store, Func<T, T> Transformer)
        {
            var StorePath = Store.Path;
            if (!UpdateStores.ContainsKey(StorePath))
            {
                var ReaderVersion = GetReaderVersion();
                var ReaderVersionContent = Store.GetVersionContent(ReaderVersion);
                var CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                UpdateStores.Add(StorePath, new UpdateStoreInfo
                {
                    Store = Store,
                    CurrentStateFromReaderVersion = CurrentStateFromReaderVersion,
                    Revert = WriterVersion =>
                    {
                        Store.RemoveVersion(WriterVersion);
                    }
                });
            }
            else
            {
                var usi = UpdateStores[StorePath];
                var ReaderVersionContent = (T)(usi.CurrentStateFromReaderVersion);
                var CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                usi.CurrentStateFromReaderVersion = CurrentStateFromReaderVersion;
            }
            Updates.Add(WriterVersion =>
            {
                var Content = Store.GetLastVersionContent();
                return Store.TryPutLastVersion(WriterVersion, Transformer(Content));
            });
        }
    }
}
