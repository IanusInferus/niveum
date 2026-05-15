using System;

namespace Firefly.Streaming
{
    public interface IFlushable
    {
        void Flush();
    }

    public interface IBasicStream : IFlushable, IDisposable
    {
    }

    public interface IReadableStream : IBasicStream
    {
        byte ReadByte();

        void Read(byte[] Buffer, int Offset, int Count);
    }

    public interface IWritableStream : IBasicStream
    {
        void WriteByte(byte b);

        void Write(byte[] Buffer, int Offset, int Count);
    }

    public interface ISeekableStream : IBasicStream
    {
        long Position { get; set; }

        long Length { get; }
    }

    public interface IResizableStream : IBasicStream
    {
        void SetLength(long Value);
    }

    public interface IReadableSeekableStream : IReadableStream, ISeekableStream
    {
    }

    public interface IWritableSeekableStream : IWritableStream, ISeekableStream
    {
    }

    public interface IReadableWritableSeekableStream : IReadableSeekableStream, IWritableSeekableStream
    {
    }

    public interface IStream : IReadableWritableSeekableStream, IResizableStream
    {
    }
}
