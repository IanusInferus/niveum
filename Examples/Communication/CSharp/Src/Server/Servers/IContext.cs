using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Server
{
    public interface IServerImplementation : IDisposable
    {
        void RaiseError(String CommandName, String Message);
    }

    public interface IServerContext : IDisposable
    {
        //跨线程共享只读访问
        Boolean EnableLogNormalIn { get; }
        Boolean EnableLogNormalOut { get; }
        Boolean EnableLogUnknownError { get; }
        Boolean EnableLogCriticalError { get; }
        Boolean EnableLogPerformance { get; }
        Boolean EnableLogSystem { get; }
        Boolean ServerDebug { get; }
        Boolean ClientDebug { get; }

        void RaiseSessionLog(SessionLogEntry Entry);

        void RegisterSession(ISessionContext SessionContext);
        Boolean TryUnregisterSession(ISessionContext SessionContext);
        
        ISessionContext CreateSessionContext();
        KeyValuePair<IServerImplementation, IBinarySerializationServerAdapter> CreateServerImplementationWithBinaryAdapter(ISessionContext SessionContext);
        KeyValuePair<IServerImplementation, IJsonSerializationServerAdapter> CreateServerImplementationWithJsonAdapter(ISessionContext SessionContext);
    }

    public class SecureContext
    {
        public Byte[] ServerToken;
        public Byte[] ClientToken;
    }

    public interface ISessionContext : IDisposable
    {
        //跨线程共享只读访问

        event Action Quit; //跨线程事件(订阅者需要保证线程安全)
        event Action Authenticated; //跨线程事件(订阅者需要保证线程安全)

        IPEndPoint RemoteEndPoint { get; set; }
        String SessionTokenString { get; }
        DateTime RequestTime { get; set; }
    }
}
