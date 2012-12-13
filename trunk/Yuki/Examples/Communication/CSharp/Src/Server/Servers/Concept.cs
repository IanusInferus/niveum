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

    public class VirtualTransportHandleResultCommand
    {
        public String CommandName;
        public Func<Byte[]> ExecuteCommand;
        public Func<Byte[], Byte[]> PackageOutput;
    }

    public class VirtualTransportHandleResultBadCommand
    {
        public String CommandName;
    }

    public class VirtualTransportHandleResultBadCommandLine
    {
        public String CommandLine;
    }

    public enum VirtualTransportHandleResultTag
    {
        Continue = 0,
        Command = 1,
        BadCommand = 2,
        BadCommandLine = 3
    }
    [TaggedUnion]
    public class VirtualTransportHandleResult
    {
        public VirtualTransportHandleResultTag _Tag;
        public Unit Continue;
        public VirtualTransportHandleResultCommand Command;
        public VirtualTransportHandleResultBadCommand BadCommand;
        public VirtualTransportHandleResultBadCommandLine BadCommandLine;

        public static VirtualTransportHandleResult CreateContinue() { return new VirtualTransportHandleResult { _Tag = VirtualTransportHandleResultTag.Continue, Continue = new Unit() }; }
        public static VirtualTransportHandleResult CreateCommand(VirtualTransportHandleResultCommand Value) { return new VirtualTransportHandleResult { _Tag = VirtualTransportHandleResultTag.Command, Command = Value }; }
        public static VirtualTransportHandleResult CreateBadCommand(VirtualTransportHandleResultBadCommand Value) { return new VirtualTransportHandleResult { _Tag = VirtualTransportHandleResultTag.BadCommand, BadCommand = Value }; }
        public static VirtualTransportHandleResult CreateBadCommandLine(VirtualTransportHandleResultBadCommandLine Value) { return new VirtualTransportHandleResult { _Tag = VirtualTransportHandleResultTag.BadCommandLine, BadCommandLine = Value }; }

        public Boolean OnRead { get { return _Tag == VirtualTransportHandleResultTag.Continue; } }
        public Boolean OnCommand { get { return _Tag == VirtualTransportHandleResultTag.Command; } }
        public Boolean OnBadCommand { get { return _Tag == VirtualTransportHandleResultTag.BadCommand; } }
        public Boolean OnBadCommandLine { get { return _Tag == VirtualTransportHandleResultTag.BadCommandLine; } }
    }

    public interface IVirtualTransportServer<TContext>
    {
        ArraySegment<Byte> GetReadBuffer(TContext c);
        VirtualTransportHandleResult Handle(TContext c, int Count);
        UInt64 Hash { get; }
        event Action<TContext, Byte[]> ServerEvent;
    }
}
