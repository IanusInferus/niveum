using System;
using System.Collections.Generic;
using System.Linq;
using Communication;

namespace Client
{
    // Application Layer
    // Serialization Layer: Binary, JSON
    // Virtual Transport Layer: Binary Count Packet, JSON Line Packet, JSON HTTP Packet
    // Physical Transport Layer: TCP
    // ...

    public class VirtualTransportClientHandleResultCommand
    {
        public String CommandName;
        public Action HandleResult;
    }

    public enum VirtualTransportClientHandleResultTag
    {
        Continue = 0,
        Command = 1
    }
    [TaggedUnion]
    public class VirtualTransportClientHandleResult
    {
        public VirtualTransportClientHandleResultTag _Tag;
        public Unit Continue;
        public VirtualTransportClientHandleResultCommand Command;

        public static VirtualTransportClientHandleResult CreateContinue() { return new VirtualTransportClientHandleResult { _Tag = VirtualTransportClientHandleResultTag.Continue, Continue = new Unit() }; }
        public static VirtualTransportClientHandleResult CreateCommand(VirtualTransportClientHandleResultCommand Value) { return new VirtualTransportClientHandleResult { _Tag = VirtualTransportClientHandleResultTag.Command, Command = Value }; }

        public Boolean OnRead { get { return _Tag == VirtualTransportClientHandleResultTag.Continue; } }
        public Boolean OnCommand { get { return _Tag == VirtualTransportClientHandleResultTag.Command; } }
    }

    public interface IVirtualTransportClient
    {
        IClient GetApplicationClient { get; }

        ArraySegment<Byte> GetReadBuffer();
        VirtualTransportClientHandleResult Handle(int Count);
        UInt64 Hash { get; }
        event Action<Byte[]> ClientMethod;
    }
}
