using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.Net;
using Communication.Binary;

namespace Client
{
    public class BinaryCountPacketClientContext
    {
        public ArraySegment<Byte> ReadBuffer = new ArraySegment<Byte>(new Byte[8 * 1024], 0, 0);

        public int State = 0;
        // 0 初始状态
        // 1 已读取NameLength
        // 2 已读取CommandHash
        // 3 已读取Name
        // 4 已读取ParametersLength

        public Int32 CommandNameLength = 0;
        public String CommandName = "";
        public UInt32 CommandHash = 0;
        public Int32 ParametersLength = 0;
    }

    public class BinaryCountPacketClient : ITcpVirtualTransportClient
    {
        private class BinaySender : IBinarySender
        {
            public Action<Byte[]> SendBytes;

            public void Send(String CommandName, UInt32 CommandHash, Byte[] Parameters)
            {
                var CommandNameBytes = TextEncoding.UTF16.GetBytes(CommandName);
                Byte[] Bytes;
                using (var s = Streams.CreateMemoryStream())
                {
                    s.WriteInt32(CommandNameBytes.Length);
                    s.Write(CommandNameBytes);
                    s.WriteUInt32(CommandHash);
                    s.WriteInt32(Parameters.Length);
                    s.Write(Parameters);
                    s.Position = 0;
                    Bytes = s.Read((int)(s.Length));
                }
                SendBytes(Bytes);
            }
        }

        private BinarySerializationClient bc;
        private BinaryCountPacketClientContext cc;
        public BinaryCountPacketClient()
        {
            this.bc = new BinarySerializationClient(new BinaySender { SendBytes = Bytes => { if (this.ClientMethod != null) { ClientMethod(Bytes); } } });
            this.cc = new BinaryCountPacketClientContext();
        }

        public IApplicationClient ApplicationClient
        {
            get
            {
                return bc.GetApplicationClient();
            }
        }

        public ArraySegment<Byte> GetReadBuffer()
        {
            return cc.ReadBuffer;
        }

        public TcpVirtualTransportClientHandleResult Handle(int Count)
        {
            var ret = TcpVirtualTransportClientHandleResult.CreateContinue();

            var Buffer = cc.ReadBuffer.Array;
            var FirstPosition = cc.ReadBuffer.Offset;
            var BufferLength = cc.ReadBuffer.Offset + cc.ReadBuffer.Count;
            BufferLength += Count;

            while (true)
            {
                var r = TryShift(cc, Buffer, FirstPosition, BufferLength - FirstPosition);
                if (r == null)
                {
                    break;
                }
                FirstPosition = r.Position;

                if (r.Command != null)
                {
                    var CommandName = r.Command.CommandName;
                    var CommandHash = r.Command.CommandHash;
                    var Parameters = r.Command.Parameters;
                    ret = TcpVirtualTransportClientHandleResult.CreateCommand(new TcpVirtualTransportClientHandleResultCommand
                    {
                        CommandName = CommandName,
                        HandleResult = () => bc.HandleResult(CommandName, CommandHash, Parameters)
                    });
                    break;
                }
            }

            if (BufferLength >= Buffer.Length && FirstPosition > 0)
            {
                var CopyLength = BufferLength - FirstPosition;
                for (int i = 0; i < CopyLength; i += 1)
                {
                    Buffer[i] = Buffer[FirstPosition + i];
                }
                BufferLength = CopyLength;
                FirstPosition = 0;
            }
            if (FirstPosition >= BufferLength)
            {
                cc.ReadBuffer = new ArraySegment<Byte>(Buffer, 0, 0);
            }
            else
            {
                cc.ReadBuffer = new ArraySegment<Byte>(Buffer, FirstPosition, BufferLength - FirstPosition);
            }

            return ret;
        }

        public UInt64 Hash { get { return ApplicationClient.Hash; } }
        public event Action<Byte[]> ClientMethod;

        private class Command
        {
            public String CommandName;
            public UInt32 CommandHash;
            public Byte[] Parameters;
        }
        private class TryShiftResult
        {
            public Command Command;
            public int Position;
        }

        private static TryShiftResult TryShift(BinaryCountPacketClientContext bc, Byte[] Buffer, int Position, int Length)
        {
            if (bc.State == 0)
            {
                if (Length >= 4)
                {
                    using (var s = new ByteArrayStream(Buffer, Position, Length))
                    {
                        bc.CommandNameLength = s.ReadInt32();
                    }
                    if (bc.CommandNameLength < 0 || bc.CommandNameLength > 128) { throw new InvalidOperationException(); }
                    var r = new TryShiftResult { Command = null, Position = Position + 4 };
                    bc.State = 1;
                    return r;
                }
                return null;
            }
            else if (bc.State == 1)
            {
                if (Length >= bc.CommandNameLength)
                {
                    using (var s = new ByteArrayStream(Buffer, Position, Length))
                    {
                        bc.CommandName = TextEncoding.UTF16.GetString(s.Read(bc.CommandNameLength));
                    }
                    var r = new TryShiftResult { Command = null, Position = Position + bc.CommandNameLength };
                    bc.State = 2;
                    return r;
                }
                return null;
            }
            else if (bc.State == 2)
            {
                if (Length >= 4)
                {
                    using (var s = new ByteArrayStream(Buffer, Position, Length))
                    {
                        bc.CommandHash = s.ReadUInt32();
                    }
                    var r = new TryShiftResult { Command = null, Position = Position + 4 };
                    bc.State = 3;
                    return r;
                }
                return null;
            }
            if (bc.State == 3)
            {
                if (Length >= 4)
                {
                    using (var s = new ByteArrayStream(Buffer, Position, Length))
                    {
                        bc.ParametersLength = s.ReadInt32();
                    }
                    if (bc.ParametersLength < 0 || bc.ParametersLength > Buffer.Length) { throw new InvalidOperationException(); }
                    var r = new TryShiftResult { Command = null, Position = Position + 4 };
                    bc.State = 4;
                    return r;
                }
                return null;
            }
            else if (bc.State == 4)
            {
                if (Length >= bc.ParametersLength)
                {
                    Byte[] Parameters;
                    using (var s = new ByteArrayStream(Buffer, Position, Length))
                    {
                        Parameters = s.Read(bc.ParametersLength);
                    }
                    var cmd = new Command { CommandName = bc.CommandName, CommandHash = bc.CommandHash, Parameters = Parameters };
                    var r = new TryShiftResult { Command = cmd, Position = Position + bc.ParametersLength };
                    bc.CommandNameLength = 0;
                    bc.CommandName = null;
                    bc.CommandHash = 0;
                    bc.ParametersLength = 0;
                    bc.State = 0;
                    return r;
                }
                return null;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
