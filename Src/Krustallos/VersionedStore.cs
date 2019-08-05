using System;
using System.Collections.Generic;
using System.Linq;

namespace Krustallos
{
    public interface IVersionedPartition
    {
        void RemoveVersion(Version v);
        void RemovePreviousVersions(Version v);

    }
    public class Partition<T> : IVersionedPartition
    {
        public int PartitionIndex { get; private set; }
        private Func<T> Allocator;
        private ImmutableSortedDictionary<Version, T> Versions = new ImmutableSortedDictionary<Version, T>();
        private Object Lockee = new Object();

        public Partition(Func<T> Allocator, int PartitionIndex)
        {
            this.Allocator = Allocator;
            this.PartitionIndex = PartitionIndex;
        }

        public T GetVersionContent(Version v)
        {
            ImmutableSortedDictionary<Version, T> Versions;
            lock (Lockee)
            {
                Versions = this.Versions;
            }
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, v))
            {
                return Pair.Value;
            }
            return Allocator();
        }
        public T GetVersionContent(Version v, ImmutableSortedDictionary<Version, Unit> Excepts)
        {
            ImmutableSortedDictionary<Version, T> Versions;
            lock (Lockee)
            {
                Versions = this.Versions;
            }
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, v))
            {
                if (Excepts.ContainsKey(Pair.Key)) { continue; }
                return Pair.Value;
            }
            return Allocator();
        }

        public Optional<KeyValuePair<Version, T>> TryGetLastVersion()
        {
            ImmutableSortedDictionary<Version, T> Versions;
            lock (Lockee)
            {
                Versions = this.Versions;
            }
            return Versions.TryGetMaxPair();
        }
        public Optional<KeyValuePair<Version, T>> TryGetLastVersion(ImmutableSortedDictionary<Version, int> Excepts)
        {
            ImmutableSortedDictionary<Version, T> Versions;
            lock (Lockee)
            {
                Versions = this.Versions;
            }
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, Optional<Version>.Empty))
            {
                if (Excepts.ContainsKey(Pair.Key)) { continue; }
                return Pair;
            }
            return Optional<KeyValuePair<Version, T>>.Empty;
        }
        public T GetLastVersionContent()
        {
            ImmutableSortedDictionary<Version, T> Versions;
            lock (Lockee)
            {
                Versions = this.Versions;
            }
            var oPair = Versions.TryGetMaxPair();
            if (oPair.OnSome)
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
            ImmutableSortedDictionary<Version, T> Versions;
            lock (Lockee)
            {
                Versions = this.Versions;
            }
            foreach (var Pair in Versions.RangeReversed(Optional<Version>.Empty, Optional<Version>.Empty))
            {
                if (Excepts.ContainsKey(Pair.Key)) { continue; }
                return Pair.Value;
            }
            return Allocator();
        }

        public void PutVersion(Version v, T Content)
        {
            lock (Lockee)
            {
                var Versions = this.Versions;
                this.Versions = (Versions.ContainsKey(v) ? Versions.Remove(v) : Versions).Add(v, Content);
            }
        }
        public Boolean TryPutLastVersion(Version v, T Content)
        {
            lock (Lockee)
            {
                var Versions = this.Versions;
                var oMaxPair = Versions.TryGetMaxPair();
                if (oMaxPair.OnSome && oMaxPair.Value.Key > v) { return false; }
                this.Versions = (Versions.ContainsKey(v) ? Versions.Remove(v) : Versions).Add(v, Content);
            }
            return true;
        }

        public void RemoveVersion(Version v)
        {
            lock (Lockee)
            {
                var Versions = this.Versions;
                if (!Versions.ContainsKey(v))
                {
                    return;
                }
                this.Versions = Versions.Remove(v);
            }
        }
        public void RemovePreviousVersions(Version v)
        {
            lock (Lockee)
            {
                var Versions = this.Versions;
                var NewVersions = Versions;
                var Prev = Optional<Version>.Empty;
                foreach (var p in Versions.Range(Optional<Version>.Empty, v))
                {
                    if (Prev.OnSome)
                    {
                        NewVersions = NewVersions.Remove(Prev.Value);
                    }
                    Prev = p.Key;
                }
                this.Versions = NewVersions;
            }
        }
    }

    public class VersionedStore<T>
    {
        public String[] Path { get; private set; }
        public int NumPartition { get; private set; }
        private Partition<T>[] Partitions;

        public VersionedStore(String[] Path, Func<T> Allocator, int NumPartition = 1)
        {
            if (NumPartition < 1) { throw new ArgumentException(); }
            this.Path = Path;
            this.NumPartition = NumPartition;
            this.Partitions = Enumerable.Range(0, NumPartition).Select(Index => new Partition<T>(Allocator, Index)).ToArray();
        }

        public Partition<T> GetPartition(int Index)
        {
            return Partitions[Index];
        }
    }
}
