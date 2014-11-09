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
        private ImmutableSortedDictionary<Version, T> Versions = new ImmutableSortedDictionary<Version, T>();

        public VersionedStore(String[] Path, Func<T> Allocator)
        {
            this.Path = Path;
            this.Allocator = Allocator;
        }

        public T GetVersionContent(Version v)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, v))
            {
                return Pair.Value;
            }
            return Allocator();
        }
        public T GetVersionContent(Version v, ImmutableSortedDictionary<Version, Unit> Excepts)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, v))
            {
                if (Excepts.ContainsKey(Pair.Key)) { continue; }
                return Pair.Value;
            }
            return Allocator();
        }

        public Optional<KeyValuePair<Version, T>> TryGetLastVersion()
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            return Versions.TryGetMaxPair();
        }
        public Optional<KeyValuePair<Version, T>> TryGetLastVersion(ImmutableSortedDictionary<Version, Unit> Excepts)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, Optional<Version>.Empty))
            {
                if (Excepts.ContainsKey(Pair.Key)) { continue; }
                return Pair;
            }
            return Optional<KeyValuePair<Version, T>>.Empty;
        }
        public T GetLastVersionContent()
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            var oPair = Versions.TryGetMaxPair();
            if (oPair.OnHasValue)
            {
                return oPair.Value.Value;
            }
            else
            {
                return Allocator();
            }
        }
        public T GetLastVersionContent(ImmutableSortedDictionary<Version, Unit> Excepts)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, Optional<Version>.Empty))
            {
                if (Excepts.ContainsKey(Pair.Key)) { continue; }
                return Pair.Value;
            }
            return Allocator();
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
        public Boolean TryPutLastVersion(Version v, T Content)
        {
            var Versions = Interlocked.CompareExchange(ref this.Versions, null, null);
            var oMaxPair = Versions.TryGetMaxPair();
            if (oMaxPair.OnHasValue && oMaxPair.Value.Key > v) { return false; }
            var NewVersion = (Versions.ContainsKey(v) ? Versions.Remove(v) : Versions).Add(v, Content);
            if (Interlocked.CompareExchange(ref this.Versions, NewVersion, Versions) == Versions)
            {
                return true;
            }
            return false;
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
                foreach (var p in Versions.Range(Optional<Version>.Empty, v))
                {
                    if (p.Key == v) { continue; }
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
