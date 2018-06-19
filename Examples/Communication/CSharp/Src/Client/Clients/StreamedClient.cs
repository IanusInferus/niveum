using System;

namespace Client
{
    public class StreamedVirtualTransportClientHandleResultCommand
    {
        public String CommandName;
        public Action HandleResult;
    }

    public enum StreamedVirtualTransportClientHandleResultTag
    {
        Continue = 0,
        Command = 1
    }
    [TaggedUnion]
    public class StreamedVirtualTransportClientHandleResult
    {
        [Tag]
        public StreamedVirtualTransportClientHandleResultTag _Tag;
        public Unit Continue;
        public StreamedVirtualTransportClientHandleResultCommand Command;

        public static StreamedVirtualTransportClientHandleResult CreateContinue() { return new StreamedVirtualTransportClientHandleResult { _Tag = StreamedVirtualTransportClientHandleResultTag.Continue, Continue = new Unit() }; }
        public static StreamedVirtualTransportClientHandleResult CreateCommand(StreamedVirtualTransportClientHandleResultCommand Value) { return new StreamedVirtualTransportClientHandleResult { _Tag = StreamedVirtualTransportClientHandleResultTag.Command, Command = Value }; }

        public Boolean OnContinue { get { return _Tag == StreamedVirtualTransportClientHandleResultTag.Continue; } }
        public Boolean OnCommand { get { return _Tag == StreamedVirtualTransportClientHandleResultTag.Command; } }
    }

    public interface IStreamedVirtualTransportClient
    {
        ArraySegment<Byte> GetReadBuffer();
        Byte[][] TakeWriteBuffer();
        StreamedVirtualTransportClientHandleResult Handle(int Count);
        UInt64 Hash { get; }
        event Action ClientMethod;
    }
}
