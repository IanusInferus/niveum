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

    public class VirtualTransportServerHandleResultCommand
    {
        public String CommandName;
        public Func<Byte[]> ExecuteCommand;
        public Func<Byte[], Byte[]> PackageOutput;
    }

    public class VirtualTransportServerHandleResultBadCommand
    {
        public String CommandName;
    }

    public class VirtualTransportServerHandleResultBadCommandLine
    {
        public String CommandLine;
    }

    public enum VirtualTransportServerHandleResultTag
    {
        Continue = 0,
        Command = 1,
        BadCommand = 2,
        BadCommandLine = 3
    }
    [TaggedUnion]
    public class VirtualTransportServerHandleResult
    {
        public VirtualTransportServerHandleResultTag _Tag;
        public Unit Continue;
        public VirtualTransportServerHandleResultCommand Command;
        public VirtualTransportServerHandleResultBadCommand BadCommand;
        public VirtualTransportServerHandleResultBadCommandLine BadCommandLine;

        public static VirtualTransportServerHandleResult CreateContinue() { return new VirtualTransportServerHandleResult { _Tag = VirtualTransportServerHandleResultTag.Continue, Continue = new Unit() }; }
        public static VirtualTransportServerHandleResult CreateCommand(VirtualTransportServerHandleResultCommand Value) { return new VirtualTransportServerHandleResult { _Tag = VirtualTransportServerHandleResultTag.Command, Command = Value }; }
        public static VirtualTransportServerHandleResult CreateBadCommand(VirtualTransportServerHandleResultBadCommand Value) { return new VirtualTransportServerHandleResult { _Tag = VirtualTransportServerHandleResultTag.BadCommand, BadCommand = Value }; }
        public static VirtualTransportServerHandleResult CreateBadCommandLine(VirtualTransportServerHandleResultBadCommandLine Value) { return new VirtualTransportServerHandleResult { _Tag = VirtualTransportServerHandleResultTag.BadCommandLine, BadCommandLine = Value }; }

        public Boolean OnRead { get { return _Tag == VirtualTransportServerHandleResultTag.Continue; } }
        public Boolean OnCommand { get { return _Tag == VirtualTransportServerHandleResultTag.Command; } }
        public Boolean OnBadCommand { get { return _Tag == VirtualTransportServerHandleResultTag.BadCommand; } }
        public Boolean OnBadCommandLine { get { return _Tag == VirtualTransportServerHandleResultTag.BadCommandLine; } }
    }

    public interface IVirtualTransportServer<TContext>
    {
        ArraySegment<Byte> GetReadBuffer(TContext c);
        VirtualTransportServerHandleResult Handle(TContext c, int Count);
        UInt64 Hash { get; }
        event Action<TContext, Byte[]> ServerEvent;
    }
}
