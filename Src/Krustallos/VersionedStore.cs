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
    public class VersionedStore<T> : IVersionedStore
    {
        public String[] Path { get; private set; }
        private Func<T> Allocator;
        private ImmutableSortedDictionary<Version, T> Versions = new ImmutableSortedDictionary<Version, T>((Left, Right) => -Left.CompareTo(Right));

        public VersionedStore(String[] Path, Func<T> Allocator)
        {
            this.Path = Path;
            this.Allocator = Allocator;
        }

        public T GetVersionContent(Version v)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            foreach (var Pair in Versions.Range(v, Optional<Version>.Empty))
            {
                return Pair.Value;
            }
            return Allocator();
        }

        public Optional<KeyValuePair<Version, T>> TryGetLastVersion()
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            return Versions.TryGetPairByIndex(0);
        }
        public T GetLastVersionContent()
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            var oPair = Versions.TryGetPairByIndex(0);
            if (oPair.OnHasValue)
            {
                return oPair.Value.Value;
            }
            else
            {
                return Allocator();
            }
        }

        public void PutVersion(Version v, T Content)
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
