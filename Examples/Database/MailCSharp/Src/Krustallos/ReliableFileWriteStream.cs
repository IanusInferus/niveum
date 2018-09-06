using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Firefly;
using Firefly.Streaming;

namespace Server
{
    public class ReliableFileWriteStream : IStream
    {
        private String AbsoluteFilePath;
        private IStream FileStream;

        public ReliableFileWriteStream(String FilePath)
        {
            AbsoluteFilePath = FileNameHandling.GetAbsolutePath(FilePath, System.Environment.CurrentDirectory);
            if (File.Exists(AbsoluteFilePath + ".new"))
            {
                throw new InvalidOperationException("FileExists: {0}".Formats(AbsoluteFilePath + ".new"));
            }
            if (File.Exists(AbsoluteFilePath))
            {
                if (File.Exists(AbsoluteFilePath + ".old"))
                {
                    throw new InvalidOperationException("FileExists: {0}".Formats(AbsoluteFilePath + ".old"));
                }
            }
            FileStream = Streams.CreateResizable(AbsoluteFilePath + ".new");
        }

        public void Dispose()
        {
            if (FileStream != null)
            {
                FileStream.Dispose();
                FileStream = null;
                if (File.Exists(AbsoluteFilePath))
                {
                    if (File.Exists(AbsoluteFilePath + ".old"))
                    {
                        throw new InvalidOperationException("FileExists: {0}".Formats(AbsoluteFilePath + ".old"));
                    }
                    File.Move(AbsoluteFilePath, AbsoluteFilePath + ".old");
                    File.Move(AbsoluteFilePath + ".new", AbsoluteFilePath);
                    File.Delete(AbsoluteFilePath + ".old");
                }
                else
                {
                    File.Move(AbsoluteFilePath + ".new", AbsoluteFilePath);
                }
            }
        }


        public void Read(Byte[] Buffer, int Offset, int Count)
        {
            FileStream.Read(Buffer, Offset, Count);
        }

        public Byte ReadByte()
        {
            return FileStream.ReadByte();
        }

        public void Write(Byte[] Buffer, int Offset, int Count)
        {
            FileStream.Write(Buffer, Offset, Count);
        }

        public void WriteByte(Byte b)
        {
            FileStream.WriteByte(b);
        }

        public void Flush()
        {
            FileStream.Flush();
        }

        public Int64 Position
        {
            get
            {
                return FileStream.Position;
            }
            set
            {
                FileStream.Position = value;
            }
        }

        public Int64 Length
        {
            get { return FileStream.Length; }
        }

        public void SetLength(Int64 Value)
        {
            FileStream.SetLength(Value);
        }
    }
}
