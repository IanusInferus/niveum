using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal sealed class ByteArrayStream : IDisposable
    {
        private Byte[] Buffer;
        public int Position { get; set; }

        public ByteArrayStream(Byte[] BaseArray, int Position, int Length)
        {
            Buffer = BaseArray;
            this.Position = Position;
        }
        public void Dispose()
        {
        }

        public Byte ReadByte()
        {
            if (Position + 1 > Buffer.Length) { throw new IndexOutOfRangeException(); }
            var b = Buffer[Position];
            Position += 1;
            return b;
        }
        public Byte[] Read(int Size)
        {
            if (Position + Size > Buffer.Length) { throw new IndexOutOfRangeException(); }
            var l = new Byte[Size];
            if (Size == 0) { return l; }
            Array.Copy(Buffer, Position, l, 0, Size);
            Position += Size;
            return l;
        }

        public UInt32 ReadUInt32()
        {
            UInt32 o;
            o = (UInt32)(ReadByte()) & 0xFF;
            o = o | (((UInt32)(ReadByte()) & 0xFF) << 8);
            o = o | (((UInt32)(ReadByte()) & 0xFF) << 16);
            o = o | (((UInt32)(ReadByte()) & 0xFF) << 24);
            return o;
        }
        public Int32 ReadInt32()
        {
            Int32 o;
            o = (Int32)(ReadByte()) & 0xFF;
            o = o | (((Int32)(ReadByte()) & 0xFF) << 8);
            o = o | (((Int32)(ReadByte()) & 0xFF) << 16);
            o = o | (((Int32)(ReadByte()) & 0xFF) << 24);
            return o;
        }
    }

    internal sealed class MemoryStream : IDisposable
    {
        private System.IO.MemoryStream BaseStream;

        public MemoryStream()
        {
            BaseStream = new System.IO.MemoryStream();
        }
        public void Dispose()
        {
            BaseStream.Dispose();
        }

        public Byte ReadByte()
        {
            var b = BaseStream.ReadByte();
            if (b == -1) { throw new System.IO.EndOfStreamException(); }
            return (Byte)(b);
        }
        public Byte[] Read(int Count)
        {
            var Buffer = new Byte[Count];
            var c = 0;
            while (c < Count)
            {
                var k = BaseStream.Read(Buffer, c, Count - c);
                if (k < 0) { throw new System.IO.EndOfStreamException(); }
                if (k == 0) { break; }
                c += k;
            }
            if (c != Count) { throw new System.IO.EndOfStreamException(); }
            return Buffer;
        }

        public void WriteByte(Byte b)
        {
            BaseStream.WriteByte(b);
        }
        public void Write(Byte[] l)
        {
            BaseStream.Write(l, 0, l.Length);
        }

        public Int64 Position
        {
            get
            {
                return BaseStream.Position;
            }
            set
            {
                BaseStream.Position = value;
            }
        }

        public Int64 Length
        {
            get
            {
                return BaseStream.Length;
            }
        }

        public void SetLength(Int64 Length)
        {
            BaseStream.SetLength(Length);
        }

        public UInt32 ReadUInt32()
        {
            UInt32 o;
            o = (UInt32)(BaseStream.ReadByte());
            o = o | (((UInt32)(BaseStream.ReadByte())) << 8);
            o = o | (((UInt32)(BaseStream.ReadByte())) << 16);
            o = o | (((UInt32)(BaseStream.ReadByte())) << 24);
            return o;
        }
        public Int32 ReadInt32()
        {
            Int32 o;
            o = BaseStream.ReadByte();
            o = o | (((Int32)(BaseStream.ReadByte())) << 8);
            o = o | (((Int32)(BaseStream.ReadByte())) << 16);
            o = o | (((Int32)(BaseStream.ReadByte())) << 24);
            return o;
        }
        public void WriteUInt32(UInt32 v)
        {
            BaseStream.WriteByte((Byte)(v & 0xFF));
            BaseStream.WriteByte((Byte)((v >> 8) & 0xFF));
            BaseStream.WriteByte((Byte)((v >> 16) & 0xFF));
            BaseStream.WriteByte((Byte)((v >> 24) & 0xFF));
        }
        public void WriteInt32(Int32 v)
        {
            BaseStream.WriteByte((Byte)(v & 0xFF));
            BaseStream.WriteByte((Byte)((v >> 8) & 0xFF));
            BaseStream.WriteByte((Byte)((v >> 16) & 0xFF));
            BaseStream.WriteByte((Byte)((v >> 24) & 0xFF));
        }
    }
}
