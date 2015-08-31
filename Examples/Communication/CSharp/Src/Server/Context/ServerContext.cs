using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Communication;
using BaseSystem;
using Algorithms;
using Server.Services;
using Communication.Json;

namespace Server
{
    public class ServerContext : IServerContext
    {
        //单线程访问
        public ServerContext()
        {
            HeadCommunicationSchemaHash = (new Communication.Binary.BinarySerializationServer()).Hash.ToString("X16");
            CommunicationSchemaHashToVersion = new Dictionary<String, String>()
            {
                {HeadCommunicationSchemaHash, ""},
                {"E0FF01B4A754245C", "1"}
            };
        }
        public void Dispose()
        {
        }

        //跨线程共享只读访问
        public String HeadCommunicationSchemaHash;
        public Dictionary<String, String> CommunicationSchemaHashToVersion; //只读的Dictionary是线程安全的

        public Boolean EnableLogNormalIn { get; set; }
        public Boolean EnableLogNormalOut { get; set; }
        public Boolean EnableLogUnknownError { get; set; }
        public Boolean EnableLogCriticalError { get; set; }
        public Boolean EnableLogPerformance { get; set; }
        public Boolean EnableLogSystem { get; set; }
        public Boolean ServerDebug { get; set; }
        public Boolean ClientDebug { get; set; }

        public event Action Shutdown; //跨线程事件(订阅者需要保证线程安全)
        public void RaiseShutdown()
        {
            if (Shutdown != null) { Shutdown(); }
        }

        public event Action<SessionLogEntry> SessionLog;
        public void RaiseSessionLog(SessionLogEntry Entry)
        {
            if (SessionLog != null)
            {
                SessionLog(Entry);
            }
        }

        private LockedVariable<HashSet<SessionContext>> SessionSet = new LockedVariable<HashSet<SessionContext>>(new HashSet<SessionContext>());
        public ICollection<SessionContext> Sessions { get { return SessionSet.Check(ss => ss.ToArray()); } }
        public void RegisterSession(ISessionContext SessionContext)
        {
            var sc = (SessionContext)(SessionContext);
            if (sc == null) { throw new InvalidOperationException(); }
            SessionSet.DoAction(ss => ss.Add(sc));
        }

        public bool TryUnregisterSession(ISessionContext SessionContext)
        {
            var sc = (SessionContext)(SessionContext);
            if (sc == null) { throw new InvalidOperationException(); }
            var Success = false;
            SessionSet.DoAction(ss =>
            {
                if (ss.Contains(sc))
                {
                    ss.Remove(sc);
                    Success = true;
                }
            });
            return Success;
        }

        public ISessionContext CreateSessionContext()
        {
            return new SessionContext(Cryptography.CreateRandom(4));
        }

        private ServerImplementation CreateServerImplementation(SessionContext Context)
        {
            var si = new ServerImplementation(this, Context);
            return si;
        }

        public KeyValuePair<IServerImplementation, IBinarySerializationServerAdapter> CreateServerImplementationWithBinaryAdapter(ISessionContext SessionContext)
        {
            var sc = (SessionContext)(SessionContext);
            if (sc == null) { throw new InvalidOperationException(); }
            var si = CreateServerImplementation(sc);
            var law = new JsonLogAspectWrapper(si);
            HookLog(sc, law);
            var a = new BinarySerializationServerAdapter(law);
            return new KeyValuePair<IServerImplementation, IBinarySerializationServerAdapter>(si, a);
        }
        public KeyValuePair<IServerImplementation, IJsonSerializationServerAdapter> CreateServerImplementationWithJsonAdapter(ISessionContext SessionContext)
        {
            var sc = (SessionContext)(SessionContext);
            if (sc == null) { throw new InvalidOperationException(); }
            var si = CreateServerImplementation(sc);
            var law = new JsonLogAspectWrapper(si);
            HookLog(sc, law);
            var a = new JsonSerializationServerAdapter(law);
            return new KeyValuePair<IServerImplementation, IJsonSerializationServerAdapter>(si, a);
        }

        private Int64 RequestCountValue = 0;
        private Int64 ReplyCountValue = 0;
        private Int64 EventCountValue = 0;
        public Int64 RequestCount
        {
            get
            {
                return Interlocked.Read(ref RequestCountValue);
            }
        }
        public Int64 ReplyCount
        {
            get
            {
                return Interlocked.Read(ref ReplyCountValue);
            }
        }
        public Int64 EventCount
        {
            get
            {
                return Interlocked.Read(ref EventCountValue);
            }
        }

        private void HookLog(SessionContext Context, JsonLogAspectWrapper law)
        {
            law.ClientCommandIn += (CommandName, Parameters) =>
            {
                Interlocked.Add(ref RequestCountValue, 1);
                if (EnableLogNormalIn)
                {
                    RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = Context.RemoteEndPoint, Time = DateTime.UtcNow, Type = "In", Name = CommandName, Message = Parameters });
                }
            };
            law.ClientCommandOut += (CommandName, Parameters) =>
            {
                Interlocked.Add(ref ReplyCountValue, 1);
                if (EnableLogNormalOut)
                {
                    RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = Context.RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Name = CommandName, Message = Parameters });
                }
            };
            law.ServerCommand += (CommandName, Parameters) =>
            {
                Interlocked.Add(ref EventCountValue, 1);
                if (EnableLogNormalOut)
                {
                    RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = Context.RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Name = CommandName, Message = Parameters });
                }
            };
        }
    }
}
