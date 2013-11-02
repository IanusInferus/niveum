using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
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
        public void Dispose()
        {
        }

        //跨线程共享只读访问

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

        public ICollection<SessionContext> Sessions { get { return SessionSet.Check(ss => ss.ToArray()); } }
        private LockedVariable<HashSet<SessionContext>> SessionSet = new LockedVariable<HashSet<SessionContext>>(new HashSet<SessionContext>());

        public event Action<SessionLogEntry> SessionLog;
        public void RaiseSessionLog(SessionLogEntry Entry)
        {
            if (SessionLog != null)
            {
                SessionLog(Entry);
            }
        }

        public void RegisterSession(ISessionContext SessionContext)
        {
            var sc = (SessionContext)(SessionContext);
            SessionSet.DoAction(ss => ss.Add(sc));
        }

        public bool TryUnregisterSession(ISessionContext SessionContext)
        {
            var sc = (SessionContext)(SessionContext);
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
            var s = new SessionContext();
            s.SessionToken = Cryptography.CreateRandom(4);
            return s;
        }

        private ServerImplementation CreateServerImplementation(SessionContext Context)
        {
            var si = new ServerImplementation(this, Context);
            return si;
        }
        private void HookLog(SessionContext Context, JsonLogAspectWrapper law)
        {
            law.ClientCommandIn += (CommandName, Parameters) =>
            {
                if (EnableLogNormalIn)
                {
                    var CommandLine = String.Format(@"{0} {1}", CommandName, Parameters);
                    RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = Context.RemoteEndPoint, Time = DateTime.UtcNow, Type = "In", Message = CommandLine });
                }
            };
            law.ClientCommandOut += (CommandName, Parameters) =>
            {
                if (EnableLogNormalOut)
                {
                    var CommandLine = String.Format(@"svr {0} {1}", CommandName, Parameters);
                    RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = Context.RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
                }
            };
            law.ServerCommand += (CommandName, Parameters) =>
            {
                if (EnableLogNormalOut)
                {
                    var CommandLine = String.Format(@"svr {0} {1}", CommandName, Parameters);
                    RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = Context.RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
                }
            };
        }

        public KeyValuePair<IServerImplementation, IBinarySerializationServerAdapter> CreateServerImplementationWithBinaryAdapter(ISessionContext SessionContext)
        {
            var Context = (SessionContext)(SessionContext);
            var si = CreateServerImplementation(Context);
            var law = new JsonLogAspectWrapper(si);
            HookLog(Context, law);
            var a = new BinarySerializationServerAdapter(law);
            return new KeyValuePair<IServerImplementation, IBinarySerializationServerAdapter>(si, a);
        }
        public KeyValuePair<IServerImplementation, IJsonSerializationServerAdapter> CreateServerImplementationWithJsonAdapter(ISessionContext SessionContext)
        {
            var Context = (SessionContext)(SessionContext);
            var si = CreateServerImplementation(Context);
            var law = new JsonLogAspectWrapper(si);
            HookLog(Context, law);
            var a = new JsonSerializationServerAdapter(law);
            return new KeyValuePair<IServerImplementation, IJsonSerializationServerAdapter>(si, a);
        }
    }
}
