﻿using System;
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
        Boolean EnableLogTransport { get; }
        Boolean ServerDebug { get; }
        Boolean ClientDebug { get; }

        void RaiseSessionLog(SessionLogEntry Entry);

        void RegisterSession(ISessionContext SessionContext);
        Boolean TryUnregisterSession(ISessionContext SessionContext);
        
        ISessionContext CreateSessionContext();
    }

    public class SecureContext
    {
        public Byte[] ServerToken; //服务器到客户端数据的Token
        public Byte[] ClientToken; //客户端到服务器数据的Token
    }

    public interface IBinaryTransformer
    {
        void Transform(Byte[] Buffer, int Start, int Count);
        void Inverse(Byte[] Buffer, int Start, int Count);
    }

    public interface ISessionContext : IDisposable
    {
        //跨线程共享只读访问

        event Action Quit; //跨线程事件(订阅者需要保证线程安全)
        event Action Authenticated; //跨线程事件(订阅者需要保证线程安全)
        event Action<SecureContext> SecureConnectionRequired; //跨线程事件(订阅者需要保证线程安全)

        IPEndPoint RemoteEndPoint { get; set; }
        /// <summary>长度为4</summary>
        Byte[] SessionToken { get; }
        String SessionTokenString { get; }
        DateTime RequestTime { get; set; }
    }
}
