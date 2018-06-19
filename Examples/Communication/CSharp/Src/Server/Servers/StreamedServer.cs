using System;

namespace Server
{
    public class StreamedVirtualTransportServerHandleResultCommand
    {
        public String CommandName;
        public Action<Action, Action<Exception>> ExecuteCommand;
    }

    public class StreamedVirtualTransportServerHandleResultBadCommand
    {
        public String CommandName;
    }

    public class StreamedVirtualTransportServerHandleResultBadCommandLine
    {
        public String CommandLine;
    }

    public enum StreamedVirtualTransportServerHandleResultTag
    {
        Continue = 0,
        Command = 1,
        BadCommand = 2,
        BadCommandLine = 3
    }
    [TaggedUnion]
    public class StreamedVirtualTransportServerHandleResult
    {
        [Tag]
        public StreamedVirtualTransportServerHandleResultTag _Tag;
        public Unit Continue;
        public StreamedVirtualTransportServerHandleResultCommand Command;
        public StreamedVirtualTransportServerHandleResultBadCommand BadCommand;
        public StreamedVirtualTransportServerHandleResultBadCommandLine BadCommandLine;

        public static StreamedVirtualTransportServerHandleResult CreateContinue() { return new StreamedVirtualTransportServerHandleResult { _Tag = StreamedVirtualTransportServerHandleResultTag.Continue, Continue = new Unit() }; }
        public static StreamedVirtualTransportServerHandleResult CreateCommand(StreamedVirtualTransportServerHandleResultCommand Value) { return new StreamedVirtualTransportServerHandleResult { _Tag = StreamedVirtualTransportServerHandleResultTag.Command, Command = Value }; }
        public static StreamedVirtualTransportServerHandleResult CreateBadCommand(StreamedVirtualTransportServerHandleResultBadCommand Value) { return new StreamedVirtualTransportServerHandleResult { _Tag = StreamedVirtualTransportServerHandleResultTag.BadCommand, BadCommand = Value }; }
        public static StreamedVirtualTransportServerHandleResult CreateBadCommandLine(StreamedVirtualTransportServerHandleResultBadCommandLine Value) { return new StreamedVirtualTransportServerHandleResult { _Tag = StreamedVirtualTransportServerHandleResultTag.BadCommandLine, BadCommandLine = Value }; }

        public Boolean OnContinue { get { return _Tag == StreamedVirtualTransportServerHandleResultTag.Continue; } }
        public Boolean OnCommand { get { return _Tag == StreamedVirtualTransportServerHandleResultTag.Command; } }
        public Boolean OnBadCommand { get { return _Tag == StreamedVirtualTransportServerHandleResultTag.BadCommand; } }
        public Boolean OnBadCommandLine { get { return _Tag == StreamedVirtualTransportServerHandleResultTag.BadCommandLine; } }
    }

    public interface IStreamedVirtualTransportServer
    {
        ArraySegment<Byte> GetReadBuffer();
        Byte[][] TakeWriteBuffer();
        StreamedVirtualTransportServerHandleResult Handle(int Count);
        UInt64 Hash { get; }
        event Action ServerEvent;
        event Action<String, int> InputByteLengthReport;
        event Action<String, int> OutputByteLengthReport;
    }
}
