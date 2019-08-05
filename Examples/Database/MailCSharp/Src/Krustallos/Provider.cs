using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;
using Firefly;
using Firefly.Streaming;
using Krustallos;
using Database.Database;
using Server;
using Version = Krustallos.Version;

namespace Database.Krustallos
{
    public class Provider : IDataAccessProvider
    {
        private KrustallosFileDataAccessPool Inner = new KrustallosFileDataAccessPool();

        public Func<String, ITransactionLock, IDataAccess> GetConnectionFactory()
        {
            return (ConnectionString, TransactionLock) => Inner.Create(ConnectionString, System.Data.IsolationLevel.Snapshot, t => TransactionLock, 16);
        }

        public Func<Exception, Boolean> GetIsRetryable()
        {
            return ex => false;
        }

        public void Dispose()
        {
            if (Inner != null)
            {
                Inner.SaveAll();
                Inner = null;
            }
        }
    }

    public class KrustallosFileDataAccessPool
    {
        private static UInt64 HashValue;
        static KrustallosFileDataAccessPool()
        {
            HashValue = (new KrustallosDataAccessPool()).Hash;
        }

        public UInt64 Hash
        {
            get
            {
                return HashValue;
            }
        }

        public int GetBackupInterval(String ConnectionString)
        {
            var Fragments = ConnectionString.Split(';');
            foreach (var Fragment in Fragments)
            {
                var Pairs = Fragment.Split('=');
                if (Pairs.Length >= 2)
                {
                    if (Pairs[0].Equals("BackupInterval", StringComparison.OrdinalIgnoreCase))
                    {
                        return int.Parse(Pairs[1]);
                    }
                }
            }
            return 300;
        }

        private class InstanceInfo
        {
            public Instance Instance = new Instance();
            public String FilePath = "";
            public String BackupDir = "";
            public Optional<int> MaxBackupCount = Optional<int>.Empty;
            public int BackupInterval = 300;
            public KrustallosData Data;
            public Yuki.RelationSchema.Schema Schema;
            public Firefly.Mapping.Binary.BinarySerializer sbs;
            public Firefly.Mapping.Binary.BinarySerializer bs;

            public InstanceInfo(String ConnectionString, IsolationLevel IsolationLevel, int NumPartition)
            {
                var Fragments = ConnectionString.Split(';');
                foreach (var Fragment in Fragments)
                {
                    var Pairs = Fragment.Split('=');
                    if (Pairs.Length >= 2)
                    {
                        if (Pairs[0].Equals("File", StringComparison.OrdinalIgnoreCase))
                        {
                            this.FilePath = FileNameHandling.GetAbsolutePath(Pairs[1], System.Environment.CurrentDirectory);
                        }
                        else if (Pairs[0].Equals("BackupDir", StringComparison.OrdinalIgnoreCase))
                        {
                            this.BackupDir = FileNameHandling.GetAbsolutePath(Pairs[1], System.Environment.CurrentDirectory);
                        }
                        else if (Pairs[0].Equals("MaxBackupCount", StringComparison.OrdinalIgnoreCase))
                        {
                            this.MaxBackupCount = int.Parse(Pairs[1]);
                        }
                        else if (Pairs[0].Equals("BackupInterval", StringComparison.OrdinalIgnoreCase))
                        {
                            this.BackupInterval = int.Parse(Pairs[1]);
                        }
                    }
                }
                if (this.FilePath == "") { throw new InvalidOperationException("InvalidConnectionString: {0}".Formats(ConnectionString)); }

                Data = new KrustallosData(NumPartition);
                sbs = Niveum.ObjectSchema.BinarySerializerWithString.Create();
                bs = KrustallosSerializer.Create();
                using (var da = new KrustallosDataAccess(Instance, Data, IsolationLevel, (ITransactionLock)(null)))
                {
                    Schema = da.Load(sbs, bs, FilePath);
                }
            }
        }
        private Dictionary<String, Lazy<InstanceInfo>> Instances = new Dictionary<String, Lazy<InstanceInfo>>();

        public IDataAccess Create(String ConnectionString, IsolationLevel IsolationLevel, Func<Transaction, ITransactionLock> TransactionLockFactory, int NumPartition)
        {
            Lazy<InstanceInfo> LazyInstance;
            lock (Instances)
            {
                if (Instances.ContainsKey(ConnectionString))
                {
                    LazyInstance = Instances[ConnectionString];
                }
                else
                {
                    LazyInstance = new Lazy<InstanceInfo>(() => new InstanceInfo(ConnectionString, IsolationLevel, NumPartition));
                    Instances.Add(ConnectionString, LazyInstance);
                }
            }
            var ii = LazyInstance.Value;
            return new KrustallosDataAccess(ii.Instance, ii.Data, IsolationLevel, TransactionLockFactory);
        }

        private Object SaveLockee = new Object();
        public void SaveAll()
        {
            lock (SaveLockee)
            {
                var InstancesSnapshot = new List<Lazy<InstanceInfo>>();
                lock (Instances)
                {
                    InstancesSnapshot = Instances.Values.ToList();
                }
                foreach (var p in InstancesSnapshot)
                {
                    var i = p.Value;
                    DateTime Time;
                    Version Version;
                    using (var da = new KrustallosDataAccess(i.Instance, i.Data, IsolationLevel.Snapshot, (ITransactionLock)(null)))
                    {
                        Version = da.GetReaderVersion();
                        Time = DateTime.UtcNow;
                        var Dir = FileNameHandling.GetFileDirectory(i.FilePath);
                        if (!Directory.Exists(Dir))
                        {
                            Directory.CreateDirectory(Dir);
                        }
                        using (var s = new ReliableFileWriteStream(i.FilePath))
                        {
                            da.Save(i.sbs, i.bs, s, i.Schema);
                        }
                    }
                    if (i.BackupDir != "")
                    {
                        if (!Directory.Exists(i.BackupDir))
                        {
                            Directory.CreateDirectory(i.BackupDir);
                        }
                        var FileName = FileNameHandling.GetFileName(i.FilePath);
                        var FilePath = FileNameHandling.GetPath(i.BackupDir, FileNameHandling.GetMainFileName(FileName) + Time.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Version.ToString() + "." + FileNameHandling.GetExtendedFileName(FileName));
                        if (!File.Exists(FilePath))
                        {
                            File.Copy(i.FilePath, FilePath + ".new", true);
                            File.Move(FilePath + ".new", FilePath);
                        }
                        if (i.MaxBackupCount.OnSome)
                        {
                            var MaxBackupCount = i.MaxBackupCount.Value;
                            var FilesToDelete = new List<String>();
                            var FilePaths = new SortedSet<String>(StringComparer.OrdinalIgnoreCase);
                            foreach (var f in Directory.EnumerateFiles(i.BackupDir, "*", SearchOption.TopDirectoryOnly))
                            {
                                FilePaths.Add(f);
                                while (FilePaths.Count > MaxBackupCount)
                                {
                                    var First = FilePaths.First();
                                    FilePaths.Remove(First);
                                    FilesToDelete.Add(First);
                                }
                            }
                            foreach (var f in FilesToDelete)
                            {
                                if (File.Exists(f))
                                {
                                    try
                                    {
                                        File.Delete(f);
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
