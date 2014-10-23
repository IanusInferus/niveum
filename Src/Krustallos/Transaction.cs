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
        private SortedDictionary<String[], UpdateStoreInfo> UpdateStores;
        private List<Action> Updates;

        private class UpdateStoreInfo
        {
            public IVersionedStore Store;
            public Action Revert;
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
            Updates = new List<Action>();
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
                foreach (var u in Updates)
                {
                    u();
                }
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
                    foreach (var p in UpdateStores)
                    {
                        var Revert = p.Value.Revert;
                        Revert();
                    }
                    if (WriterVersion.OnHasValue)
                    {
                        Instance.RevertWriterVersion(WriterVersion.Value);
                        WriterVersion = Optional<Version>.Empty;
                    }
                }
                foreach (var o in Locked)
                {
                    Monitor.Exit(o);
                }
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

        public TRet CheckReaderVersioned<T, TRet>(VersionedStore<T> Store, Func<T, TRet> Selector)
        {
            var ReaderVersion = GetReaderVersion();
            var Content = Store.GetVersionContent(ReaderVersion);
            return Selector(Content);
        }
        public TRet CheckCurrentVersioned<T, TRet>(VersionedStore<T> Store, Func<T, TRet> Selector)
        {
            var Content = Store.GetLastVersionContent();
            return Selector(Content);
        }
        public void UpdateVersioned<T>(String[] StorePath, VersionedStore<T> Store, Func<T, T> Transformer)
        {
            if (!UpdateStores.ContainsKey(StorePath))
            {
                UpdateStores.Add(StorePath, new UpdateStoreInfo
                {
                    Store = Store,
                    Revert = () =>
                    {
                        var WriterVersion = GetWriterVersion();
                        Store.RemoveVersion(WriterVersion);
                    }
                });
            }
            Updates.Add(() =>
            {
                var WriterVersion = GetWriterVersion();
                var Content = Store.GetLastVersionContent();
                Store.PutVersion(WriterVersion, Transformer(Content));
            });
        }
    }
}
