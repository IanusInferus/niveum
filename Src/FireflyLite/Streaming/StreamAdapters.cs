using System;
using System.IO;

namespace Firefly.Streaming
{
    internal sealed class IReadableStreamAdapter : IReadableStream
    {
        private Stream BaseStream;

        public IReadableStreamAdapter(Stream BaseStream)
        {
            if (!BaseStream.CanRead) throw new ArgumentException();
            this.BaseStream = BaseStream;
        }

        public void Flush() { BaseStream.Flush(); }

        public byte ReadByte()
        {
            int b = BaseStream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return (byte)b;
        }
        public void Read(byte[] Buffer, int Offset, int Count)
        {
            int c = 0;
            while (c < Count)
            {
                var k = BaseStream.Read(Buffer, Offset + c, Count - c);
                if (k < 0) throw new EndOfStreamException();
                if (k == 0) break;
                c += k;
            }
            if (c != Count) throw new EndOfStreamException();
        }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
        }
    }

    internal sealed class IWritableStreamAdapter : IWritableStream
    {
        private Stream BaseStream;

        public IWritableStreamAdapter(Stream BaseStream)
        {
            if (!BaseStream.CanWrite) throw new ArgumentException();
            this.BaseStream = BaseStream;
        }

        public void Flush() { BaseStream.Flush(); }

        public void WriteByte(byte b) { BaseStream.WriteByte(b); }
        public void Write(byte[] Buffer, int Offset, int Count) { BaseStream.Write(Buffer, Offset, Count); }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
        }
    }

    internal sealed class IReadableSeekableStreamAdapter : IReadableSeekableStream
    {
        private Stream BaseStream;

        public IReadableSeekableStreamAdapter(Stream BaseStream)
        {
            if (!(BaseStream.CanRead && BaseStream.CanSeek)) throw new ArgumentException();
            this.BaseStream = BaseStream;
        }

        public void Flush() { BaseStream.Flush(); }

        public long Length { get { return BaseStream.Length; } }
        public long Position
        {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value; }
        }

        public byte ReadByte()
        {
            int b = BaseStream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return (byte)b;
        }
        public void Read(byte[] Buffer, int Offset, int Count)
        {
            int c = 0;
            while (c < Count)
            {
                var k = BaseStream.Read(Buffer, Offset + c, Count - c);
                if (k < 0) throw new EndOfStreamException();
                if (k == 0) break;
                c += k;
            }
            if (c != Count) throw new EndOfStreamException();
        }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
        }
    }

    internal sealed class IWritableSeekableStreamAdapter : IWritableSeekableStream
    {
        private Stream BaseStream;

        public IWritableSeekableStreamAdapter(Stream BaseStream)
        {
            if (!(BaseStream.CanWrite && BaseStream.CanSeek)) throw new ArgumentException();
            this.BaseStream = BaseStream;
        }

        public void Flush() { BaseStream.Flush(); }

        public long Length { get { return BaseStream.Length; } }
        public long Position
        {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value; }
        }

        public void WriteByte(byte b) { BaseStream.WriteByte(b); }
        public void Write(byte[] Buffer, int Offset, int Count) { BaseStream.Write(Buffer, Offset, Count); }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
        }
    }

    internal sealed class IReadableWritableSeekableStreamAdapter : IReadableWritableSeekableStream
    {
        private Stream BaseStream;

        public IReadableWritableSeekableStreamAdapter(Stream BaseStream)
        {
            if (!(BaseStream.CanRead && BaseStream.CanWrite && BaseStream.CanSeek)) throw new ArgumentException();
            this.BaseStream = BaseStream;
        }

        public void Flush() { BaseStream.Flush(); }

        public long Length { get { return BaseStream.Length; } }
        public long Position
        {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value; }
        }

        public byte ReadByte()
        {
            int b = BaseStream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return (byte)b;
        }
        public void Read(byte[] Buffer, int Offset, int Count)
        {
            int c = 0;
            while (c < Count)
            {
                var k = BaseStream.Read(Buffer, Offset + c, Count - c);
                if (k < 0) throw new EndOfStreamException();
                if (k == 0) break;
                c += k;
            }
            if (c != Count) throw new EndOfStreamException();
        }

        public void WriteByte(byte b) { BaseStream.WriteByte(b); }
        public void Write(byte[] Buffer, int Offset, int Count) { BaseStream.Write(Buffer, Offset, Count); }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
        }
    }

    internal sealed class IStreamAdapter : IStream
    {
        private Stream BaseStream;

        public IStreamAdapter(Stream BaseStream)
        {
            if (!(BaseStream.CanRead && BaseStream.CanWrite && BaseStream.CanSeek)) throw new ArgumentException();
            this.BaseStream = BaseStream;
        }

        public void Flush() { BaseStream.Flush(); }

        public long Length { get { return BaseStream.Length; } }
        public long Position
        {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value; }
        }
        public void SetLength(long Value) { BaseStream.SetLength(Value); }

        public byte ReadByte()
        {
            int b = BaseStream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return (byte)b;
        }
        public void Read(byte[] Buffer, int Offset, int Count)
        {
            int c = 0;
            while (c < Count)
            {
                var k = BaseStream.Read(Buffer, Offset + c, Count - c);
                if (k < 0) throw new EndOfStreamException();
                if (k == 0) break;
                c += k;
            }
            if (c != Count) throw new EndOfStreamException();
        }

        public void WriteByte(byte b) { BaseStream.WriteByte(b); }
        public void Write(byte[] Buffer, int Offset, int Count) { BaseStream.Write(Buffer, Offset, Count); }

        public void Dispose()
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
        }
    }

    internal sealed class StreamAdapter : Stream
    {
        private IBasicStream BaseStream;

        private IReadableStream Readable;
        private IWritableStream Writable;
        private ISeekableStream Seekable;
        private IResizableStream Resizable;

        public StreamAdapter(IBasicStream s)
        {
            BaseStream = s;
            Readable = s as IReadableStream;
            Writable = s as IWritableStream;
            Seekable = s as ISeekableStream;
            Resizable = s as IResizableStream;
        }
        public override bool CanRead { get { return Readable != null; } }
        public override bool CanSeek { get { return Seekable != null; } }
        public override bool CanWrite { get { return Writable != null; } }
        public override void Flush() { BaseStream.Flush(); }
        public override long Length { get { return Seekable.Length; } }
        public override long Position
        {
            get { return Seekable.Position; }
            set { Seekable.Position = value; }
        }
        public override long Seek(long Offset, SeekOrigin Origin)
        {
            switch (Origin)
            {
                case SeekOrigin.Begin:
                    Position = Offset;
                    break;
                case SeekOrigin.Current:
                    Position += Offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - Offset;
                    break;
            }
            return Position;
        }
        public override void SetLength(long Value) { Resizable.SetLength(Value); }
        public override int ReadByte() { return Readable.ReadByte(); }
        public override void WriteByte(byte Value) { Writable.WriteByte(Value); }
        public override int Read(byte[] Buffer, int Offset, int Count)
        {
            Readable.Read(Buffer, Offset, Count);
            return Count;
        }
        public override void Write(byte[] Buffer, int Offset, int Count)
        {
            Writable.Write(Buffer, Offset, Count);
        }
        protected override void Dispose(bool disposing)
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class UnsafeStreamAdapter : Stream
    {
        private IBasicStream BaseStream;

        private IReadableStream Readable;
        private IWritableStream Writable;
        private ISeekableStream Seekable;
        private IResizableStream Resizable;

        public UnsafeStreamAdapter(IBasicStream s)
        {
            BaseStream = s;
            Readable = s as IReadableStream;
            Writable = s as IWritableStream;
            Seekable = s as ISeekableStream;
            Resizable = s as IResizableStream;
        }
        public override bool CanRead { get { return Readable != null; } }
        public override bool CanSeek { get { return Seekable != null; } }
        public override bool CanWrite { get { return Writable != null; } }
        public override void Flush() { BaseStream.Flush(); }
        public override long Length { get { return Seekable.Length; } }
        public override long Position
        {
            get { return Seekable.Position; }
            set { Seekable.Position = value; }
        }
        public override long Seek(long Offset, SeekOrigin Origin)
        {
            switch (Origin)
            {
                case SeekOrigin.Begin:
                    Position = Offset;
                    break;
                case SeekOrigin.Current:
                    Position += Offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - Offset;
                    break;
            }
            return Position;
        }
        public override void SetLength(long Value) { Resizable.SetLength(Value); }
        public override int ReadByte()
        {
            try
            {
                return Readable.ReadByte();
            }
            catch (EndOfStreamException)
            {
                return -1;
            }
        }
        public override void WriteByte(byte Value) { Writable.WriteByte(Value); }
        public override int Read(byte[] Buffer, int Offset, int Count)
        {
            if (Seekable.Position >= Seekable.Length)
            {
                return 0;
            }
            else if (Seekable.Position + Count > Seekable.Length)
            {
                int NewCount = (int)(Seekable.Length - Seekable.Position);
                Readable.Read(Buffer, Offset, NewCount);
                return NewCount;
            }
            else
            {
                Readable.Read(Buffer, Offset, Count);
                return Count;
            }
        }
        public override void Write(byte[] Buffer, int Offset, int Count)
        {
            Writable.Write(Buffer, Offset, Count);
        }
        protected override void Dispose(bool disposing)
        {
            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }
            base.Dispose(disposing);
        }
    }
}
