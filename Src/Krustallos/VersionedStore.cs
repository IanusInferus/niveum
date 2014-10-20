using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Krustallos
{
    public interface IVersionedStore
    {
        void RemoveVersion(Version v);
        void RemovePreviousVersions(Version v);

    }
    public class VersionedStore<TKey, TValue> : IVersionedStore
    {
        private ImmutableSortedDictionary<Version, ImmutableSortedDictionary<TKey, TValue>> Versions = new ImmutableSortedDictionary<Version, ImmutableSortedDictionary<TKey, TValue>>((Left, Right) => -Left.CompareTo(Right));
        private Func<TKey, TKey, int> Compare;

        public VersionedStore()
        {
            this.Compare = ConcurrentComparer.CreateDefault<TKey>();
        }
        public VersionedStore(bool IsReversed)
        {
            this.Compare = ConcurrentComparer.CreateDefault<TKey>(IsReversed);
        }
        public VersionedStore(Func<TKey, TKey, int> ConcurrentCompare)
        {
            this.Compare = ConcurrentCompare;
        }

        public ImmutableSortedDictionary<TKey, TValue> GetVersionContent(Version v)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            foreach (var Pair in Versions.Range(v, Optional<Version>.Empty))
            {
                return Pair.Value;
            }
            return new ImmutableSortedDictionary<TKey, TValue>(Compare);
        }

        public Optional<KeyValuePair<Version, ImmutableSortedDictionary<TKey, TValue>>> TryGetLastVersion()
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            return Versions.TryGetPairByIndex(0);
        }
        public ImmutableSortedDictionary<TKey, TValue> GetLastVersionContent()
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            var oPair = Versions.TryGetPairByIndex(0);
            if (oPair.OnHasValue)
            {
                return oPair.Value.Value;
            }
            else
            {
                return new ImmutableSortedDictionary<TKey, TValue>(Compare);
            }
        }

        public void PutVersion(Version v, ImmutableSortedDictionary<TKey, TValue> Content)
        {
            while (true)
            {
                var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
                var NewVersion = (Versions.ContainsKey(v) ? Versions.Remove(v) : Versions).Add(v, Content);
                if (Interlocked.CompareExchange(ref this.Versions, NewVersion, Versions) == Versions)
                {
                    break;
                }
            }
        }

        public void RemoveVersion(Version v)
        {
            while (true)
            {
                var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
                if (!Versions.ContainsKey(v))
                {
                    break;
                }
                if (Interlocked.CompareExchange(ref this.Versions, Versions.Remove(v), Versions) == Versions)
                {
                    break;
                }
            }
        }
        public void RemovePreviousVersions(Version v)
        {
            while (true)
            {
                var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
                var NewVersions = Versions;
                foreach (var p in Versions.Range(v, Optional<Version>.Empty).Skip(1))
                {
                    if (NewVersions.Count > 1)
                    {
                        NewVersions = NewVersions.Remove(p.Key);
                        break;
                    }
                }
                if (Interlocked.CompareExchange(ref this.Versions, NewVersions, Versions) == Versions)
                {
                    break;
                }
            }
        }
    }
}
