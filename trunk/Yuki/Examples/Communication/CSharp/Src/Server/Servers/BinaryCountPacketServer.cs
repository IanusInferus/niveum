using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.Binary;

namespace Server
{
    public class BinaryCountPacketContext
    {
        public ArraySegment<Byte> Buffer = new ArraySegment<Byte>(new Byte[8 * 1024], 0, 0);

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

    public class BinaryCountPacketServer<TContext> : IVirtualTransportServer<TContext>
    {
        public delegate Boolean CheckCommandAllowedDelegate(TContext c, String CommandName);

        private BinaryServer<TContext> sv;
        private Func<TContext, BinaryCountPacketContext> Acquire;
        private CheckCommandAllowedDelegate CheckCommandAllowed;
        public BinaryCountPacketServer(IServerImplementation<TContext> ApplicationServer, Func<TContext, BinaryCountPacketContext> Acquire, CheckCommandAllowedDelegate CheckCommandAllowed)
        {
            this.sv = new BinaryServer<TContext>(ApplicationServer);
            this.Acquire = Acquire;
            this.CheckCommandAllowed = CheckCommandAllowed;
            this.sv.ServerEvent += (c, CommandName, CommandHash, Parameters) =>
            {
                if (this.ServerEvent != null)
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
                    this.ServerEvent(c, Bytes);
                }
            };
        }

        public ArraySegment<Byte> GetReadBuffer(TContext c)
        {
            var bc = Acquire(c);
            return bc.Buffer;
        }

        public VirtualTransportHandleResult Handle(TContext c, int Count)
        {
            var bc = Acquire(c);
            if (Count <= 0)
            {
                return VirtualTransportHandleResult.CreateContinue();
            }

            var ret = VirtualTransportHandleResult.CreateContinue();

            var Buffer = bc.Buffer.Array;
            var FirstPosition = bc.Buffer.Offset;
            var BufferLength = bc.Buffer.Offset + bc.Buffer.Count;
            BufferLength += Count;

            while (true)
            {
                var r = TryShift(bc, Buffer, FirstPosition, BufferLength - FirstPosition);
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
                    if (sv.HasCommand(CommandName, CommandHash) && (CheckCommandAllowed != null ? CheckCommandAllowed(c, CommandName) : true))
                    {
                        ret = VirtualTransportHandleResult.CreateCommand(new VirtualTransportHandleResultCommand
                        {
                            CommandName = CommandName,
                            ExecuteCommand = () => sv.ExecuteCommand(c, CommandName, CommandHash, Parameters),
                            PackageOutput = OutputParameters =>
                            {
                                var CommandNameBytes = TextEncoding.UTF16.GetBytes(CommandName);
                                Byte[] Bytes;
                                using (var s = Streams.CreateMemoryStream())
                                {
                                    s.WriteInt32(CommandNameBytes.Length);
                                    s.Write(CommandNameBytes);
                                    s.WriteUInt32(CommandHash);
                                    s.WriteInt32(OutputParameters.Length);
                                    s.Write(OutputParameters);
                                    s.Position = 0;
                                    Bytes = s.Read((int)(s.Length));
                                }
                                return Bytes;
                            }
                        });
                    }
                    else
                    {
                        ret = VirtualTransportHandleResult.CreateBadCommand(new VirtualTransportHandleResultBadCommand { CommandName = CommandName });
                    }
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
                bc.Buffer = new ArraySegment<Byte>(Buffer, 0, 0);
            }
            else
            {
                bc.Buffer = new ArraySegment<Byte>(Buffer, FirstPosition, BufferLength - FirstPosition);
            }

            return ret;
        }

        public UInt64 Hash { get { return sv.Hash; } }
        public event Action<TContext, Byte[]> ServerEvent;

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

        private static TryShiftResult TryShift(BinaryCountPacketContext bc, Byte[] Buffer, int Position, int Length)
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
                    if (bc.ParametersLength < 0 || bc.ParametersLength > 8 * 1024) { throw new InvalidOperationException(); }
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
