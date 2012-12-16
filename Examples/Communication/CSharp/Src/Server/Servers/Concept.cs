using System;
using System.Collections.Generic;
using System.Linq;

namespace Server
{
    // Application Layer
    // Serialization Layer: Binary, JSON
    // Virtual Transport Layer: Binary Count Packet, JSON Line Packet, JSON HTTP Packet
    // Physical Transport Layer: TCP
    // ...

    public interface IServer : IDisposable
    {
        void Start();
        void Stop();

        event Action<SessionLogEntry> SessionLog;
    }
}
