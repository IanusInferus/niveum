using System;
using System.IO;

namespace Firefly.Streaming
{
    public partial class Streams
    {
        private Streams() { }

        public static IReadableSeekableStream OpenReadable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.Open, FileAccess.Read, Share), fs => new IReadableSeekableStreamAdapter(fs));
        }
        public static IWritableSeekableStream CreateWritable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.Create, FileAccess.Write, Share), fs => new IWritableSeekableStreamAdapter(fs));
        }
        public static IWritableSeekableStream CreateNewWritable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.CreateNew, FileAccess.Write, Share), fs => new IWritableSeekableStreamAdapter(fs));
        }
        public static IReadableWritableSeekableStream CreateReadableWritable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.Create, FileAccess.ReadWrite, Share), fs => new IReadableWritableSeekableStreamAdapter(fs));
        }
        public static IReadableWritableSeekableStream OpenReadableWritable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, Share), fs => new IReadableWritableSeekableStreamAdapter(fs));
        }
        public static IReadableWritableSeekableStream OpenOrCreateReadableWritable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, Share), fs => new IReadableWritableSeekableStreamAdapter(fs));
        }
        public static IStream CreateMemoryStream()
        {
            return SafeWrap(new MemoryStream(), fs => new IStreamAdapter(fs));
        }
        public static IStream CreateResizable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.Create, FileAccess.ReadWrite, Share), fs => new IStreamAdapter(fs));
        }
        public static IStream OpenResizable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, Share), fs => new IStreamAdapter(fs));
        }
        public static IStream OpenOrCreateResizable(string Path, FileShare Share = FileShare.Read)
        {
            return SafeWrap(new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, Share), fs => new IStreamAdapter(fs));
        }

        private static T SafeWrap<T>(Stream Stream, Func<Stream, T> Factory)
        {
            var Success = false;
            try
            {
                var a = Factory(Stream);
                Success = true;
                return a;
            }
            finally
            {
                if (!Success)
                {
                    Stream.Dispose();
                }
            }
        }
    }
}
