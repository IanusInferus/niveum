using System;

namespace Server
{
    // Application Layer
    // Serialization Layer: Binary, JSON
    // Virtual Transport Layer: Binary Count Packet, JSON Line Packet, JSON HTTP Packet
    // Physical Transport Layer: TCP
    // ...

    public interface IServer : IDisposable
    {
        Boolean IsRunning { get; }
        void Start();
        void Stop();
    }
}
