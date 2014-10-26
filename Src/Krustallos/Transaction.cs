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
            public Object CurrentStateFromReaderVersion;
            public Object CurrentStateFromCurrentVersion;
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
            Monitor.Enter(Instance);
            try
            {
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
                Monitor.Exit(Instance);
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
            var StorePath = Store.Path;
            if (!UpdateStores.ContainsKey(StorePath))
            {
                var ReaderVersion = GetReaderVersion();
                var Content = Store.GetVersionContent(ReaderVersion);
                return Selector(Content);
            }
            else
            {
                var usi = UpdateStores[StorePath];
                return Selector((T)(usi.CurrentStateFromReaderVersion));
            }
        }
        public TRet CheckCurrentVersioned<T, TRet>(VersionedStore<T> Store, Func<T, TRet> Selector)
        {
            var StorePath = Store.Path;
            if (!UpdateStores.ContainsKey(StorePath))
            {
                var Content = Store.GetLastVersionContent();
                return Selector(Content);
            }
            else
            {
                var usi = UpdateStores[StorePath];
                return Selector((T)(usi.CurrentStateFromCurrentVersion));
            }
        }
        public void UpdateVersioned<T>(VersionedStore<T> Store, Func<T, T> Transformer)
        {
            var StorePath = Store.Path;
            if (!UpdateStores.ContainsKey(StorePath))
            {
                var ReaderVersion = GetReaderVersion();
                var ReaderVersionContent = Store.GetVersionContent(ReaderVersion);
                var CurrentVersionContent = Store.GetLastVersionContent();
                Object CurrentStateFromReaderVersion;
                Object CurrentStateFromCurrentVersion;
                if ((Object)(ReaderVersionContent) == (Object)(CurrentVersionContent))
                {
                    CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                    CurrentStateFromCurrentVersion = CurrentStateFromReaderVersion;
                }
                else
                {
                    CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                    CurrentStateFromCurrentVersion = Transformer(CurrentVersionContent);
                }
                UpdateStores.Add(StorePath, new UpdateStoreInfo
                {
                    Store = Store,
                    CurrentStateFromReaderVersion = CurrentStateFromReaderVersion,
                    CurrentStateFromCurrentVersion = CurrentStateFromCurrentVersion,
                    Revert = () =>
                    {
                        var WriterVersion = GetWriterVersion();
                        Store.RemoveVersion(WriterVersion);
                    }
                });
            }
            else
            {
                var usi = UpdateStores[StorePath];
                var ReaderVersionContent = (T)(usi.CurrentStateFromReaderVersion);
                var CurrentVersionContent = (T)(usi.CurrentStateFromCurrentVersion);
                Object CurrentStateFromReaderVersion;
                Object CurrentStateFromCurrentVersion;
                if ((Object)(ReaderVersionContent) == (Object)(CurrentVersionContent))
                {
                    CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                    CurrentStateFromCurrentVersion = CurrentStateFromReaderVersion;
                }
                else
                {
                    CurrentStateFromReaderVersion = Transformer(ReaderVersionContent);
                    CurrentStateFromCurrentVersion = Transformer(CurrentVersionContent);
                }
                usi.CurrentStateFromReaderVersion = CurrentStateFromReaderVersion;
                usi.CurrentStateFromCurrentVersion = CurrentStateFromCurrentVersion;
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
