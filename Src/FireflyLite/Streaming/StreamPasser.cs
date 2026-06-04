using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Firefly.Streaming
{
    public static class StreamPasser
    {
        public static NewReadingStreamPasser AsNewReading(this IReadableSeekableStream This)
        {
            return new NewReadingStreamPasser(This);
        }
        public static NewWritingStreamPasser AsNewWriting(this IStream This)
        {
            return new NewWritingStreamPasser(This);
        }
        public static NewReadingWritingStreamPasser AsNewReadingWriting(this IStream This)
        {
            return new NewReadingWritingStreamPasser(This);
        }

        public static IReadableStream AsReadable(this Stream This) { return new IReadableStreamAdapter(This); }
        public static IWritableStream AsWritable(this Stream This) { return new IWritableStreamAdapter(This); }
        public static IReadableSeekableStream AsReadableSeekable(this Stream This) { return new IReadableSeekableStreamAdapter(This); }
        public static IWritableSeekableStream AsWritableSeekable(this Stream This) { return new IWritableSeekableStreamAdapter(This); }
        public static IReadableWritableSeekableStream AsReadableWritableSeekable(this Stream This) { return new IReadableWritableSeekableStreamAdapter(This); }
        public static IStream AsIStream(this Stream This) { return new IStreamAdapter(This); }

        public static Stream ToStream(this IBasicStream This) { return new StreamAdapter(This); }
        public static Stream ToUnsafeStream(this IBasicStream This) { return new UnsafeStreamAdapter(This); }
    }

    public class NewReadingStreamPasser
    {
        private IReadableSeekableStream BaseStream;

        public NewReadingStreamPasser(IReadableSeekableStream s)
        {
            if (s.Position != 0) throw new ArgumentException("PositionNotZero");
            BaseStream = s;
        }

        public IReadableSeekableStream GetStream()
        {
            if (BaseStream.Position != 0) throw new ArgumentException("PositionNotZero");
            return BaseStream;
        }
    }

    public class NewWritingStreamPasser
    {
        private IStream BaseStream;

        public NewWritingStreamPasser(IStream s)
        {
            if (s.Length != 0) throw new ArgumentException("LengthNotZero");
            if (s.Position != 0) throw new ArgumentException("PositionNotZero");
            BaseStream = s;
        }

        public IStream GetStream()
        {
            if (BaseStream.Length != 0) throw new ArgumentException("LengthNotZero");
            if (BaseStream.Position != 0) throw new ArgumentException("PositionNotZero");
            return BaseStream;
        }
    }

    public class NewReadingWritingStreamPasser
    {
        private IStream BaseStream;

        public NewReadingWritingStreamPasser(IStream s)
        {
            if (s.Position != 0) throw new ArgumentException("PositionNotZero");
            BaseStream = s;
        }

        public IStream GetStream()
        {
            if (BaseStream.Position != 0) throw new ArgumentException("PositionNotZero");
            return BaseStream;
        }
    }
}
