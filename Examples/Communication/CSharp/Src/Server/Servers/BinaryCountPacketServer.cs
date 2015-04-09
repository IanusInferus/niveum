using System;
using System.Collections.Generic;
using System.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;

namespace Server
{
    public partial class Streamed<TServerContext>
    {
        public class BinaryCountPacketServer : IStreamedVirtualTransportServer
        {
            private class Context
            {
                public ArraySegment<Byte> ReadBuffer;
                public Object WriteBufferLockee = new Object();
                public List<Byte[]> WriteBuffer = new List<Byte[]>();

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
                public Int32 InputCommandByteLength = 0;

                public Context(int ReadBufferSize)
                {
                    ReadBuffer = new ArraySegment<Byte>(new Byte[ReadBufferSize], 0, 0);
                }
            }

            public delegate Boolean CheckCommandAllowedDelegate(String CommandName);

            private IBinarySerializationServerAdapter ss;
            private Context c;
            private CheckCommandAllowedDelegate CheckCommandAllowed;
            private IBinaryTransformer Transformer;
            public BinaryCountPacketServer(IBinarySerializationServerAdapter SerializationServerAdapter, CheckCommandAllowedDelegate CheckCommandAllowed, IBinaryTransformer Transformer = null, int ReadBufferSize = 8 * 1024)
            {
                this.ss = SerializationServerAdapter;
                this.c = new Context(ReadBufferSize);
                this.CheckCommandAllowed = CheckCommandAllowed;
                this.Transformer = Transformer;
                this.ss.ServerEvent += (CommandName, CommandHash, Parameters) =>
                {
                    var CommandNameBytes = TextEncoding.UTF16.GetBytes(CommandName);
                    Byte[] Bytes;
                    using (var ms = Streams.CreateMemoryStream())
                    {
                        ms.WriteInt32(CommandNameBytes.Length);
                        ms.Write(CommandNameBytes);
                        ms.WriteUInt32(CommandHash);
                        ms.WriteInt32(Parameters.Length);
                        ms.Write(Parameters);
                        ms.Position = 0;
                        Bytes = ms.Read((int)(ms.Length));
                    }
                    var BytesLength = Bytes.Length;
                    lock (c.WriteBufferLockee)
                    {
                        if (Transformer != null)
                        {
                            Transformer.Transform(Bytes, 0, BytesLength);
                        }
                        c.WriteBuffer.Add(Bytes);
                    }
                    if (OutputByteLengthReport != null)
                    {
                        OutputByteLengthReport(CommandName, BytesLength);
                    }
                    if (this.ServerEvent != null)
                    {
                        this.ServerEvent();
                    }
                };
            }

            public ArraySegment<Byte> GetReadBuffer()
            {
                return c.ReadBuffer;
            }

            public Byte[][] TakeWriteBuffer()
            {
                lock (c.WriteBufferLockee)
                {
                    var WriteBuffer = c.WriteBuffer.ToArray();
                    c.WriteBuffer = new List<Byte[]>();
                    return WriteBuffer;
                }
            }

            public StreamedVirtualTransportServerHandleResult Handle(int Count)
            {
                var ret = StreamedVirtualTransportServerHandleResult.CreateContinue();

                var Buffer = c.ReadBuffer.Array;
                var FirstPosition = c.ReadBuffer.Offset;
                var BufferLength = c.ReadBuffer.Offset + c.ReadBuffer.Count;
                if (Transformer != null)
                {
                    Transformer.Inverse(Buffer, BufferLength, Count);
                }
                BufferLength += Count;

                while (true)
                {
                    var r = TryShift(c, Buffer, FirstPosition, BufferLength - FirstPosition);
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
                        if (InputByteLengthReport != null)
                        {
                            InputByteLengthReport(CommandName, r.Command.ByteLength);
                        }
                        if (ss.HasCommand(CommandName, CommandHash) && (CheckCommandAllowed != null ? CheckCommandAllowed(CommandName) : true))
                        {
                            ret = StreamedVirtualTransportServerHandleResult.CreateCommand(new StreamedVirtualTransportServerHandleResultCommand
                            {
                                CommandName = CommandName,
                                ExecuteCommand = (OnSuccess, OnFailure) =>
                                {
                                    Action<Byte[]> OnSuccessInner = OutputParameters =>
                                    {
                                        var CommandNameBytes = TextEncoding.UTF16.GetBytes(CommandName);
                                        Byte[] Bytes;
                                        using (var ms = Streams.CreateMemoryStream())
                                        {
                                            ms.WriteInt32(CommandNameBytes.Length);
                                            ms.Write(CommandNameBytes);
                                            ms.WriteUInt32(CommandHash);
                                            ms.WriteInt32(OutputParameters.Length);
                                            ms.Write(OutputParameters);
                                            ms.Position = 0;
                                            Bytes = ms.Read((int)(ms.Length));
                                        }
                                        var BytesLength = Bytes.Length;
                                        lock (c.WriteBufferLockee)
                                        {
                                            if (Transformer != null)
                                            {
                                                Transformer.Transform(Bytes, 0, BytesLength);
                                            }
                                            c.WriteBuffer.Add(Bytes);
                                        }
                                        if (OutputByteLengthReport != null)
                                        {
                                            OutputByteLengthReport(CommandName, BytesLength);
                                        }
                                        OnSuccess();
                                    };
                                    ss.ExecuteCommand(CommandName, CommandHash, Parameters, OnSuccessInner, OnFailure);
                                }
                            });
                        }
                        else
                        {
                            ret = StreamedVirtualTransportServerHandleResult.CreateBadCommand(new StreamedVirtualTransportServerHandleResultBadCommand { CommandName = CommandName });
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
                    c.ReadBuffer = new ArraySegment<Byte>(Buffer, 0, 0);
                }
                else
                {
                    c.ReadBuffer = new ArraySegment<Byte>(Buffer, FirstPosition, BufferLength - FirstPosition);
                }

                return ret;
            }

            public UInt64 Hash { get { return ss.Hash; } }
            public event Action ServerEvent;
            public event Action<String, int> InputByteLengthReport;
            public event Action<String, int> OutputByteLengthReport;

            private class Command
            {
                public String CommandName;
                public UInt32 CommandHash;
                public Byte[] Parameters;
                public Int32 ByteLength;
            }
            private class TryShiftResult
            {
                public Command Command;
                public int Position;
            }

            private static TryShiftResult TryShift(Context bc, Byte[] Buffer, int Position, int Length)
            {
                if (bc.State == 0)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            bc.CommandNameLength = s.ReadInt32();
                        }
                        if (bc.CommandNameLength < 0 || bc.CommandNameLength > 128) { throw new InvalidOperationException("CommandNameLengthOutOfBound"); }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        bc.InputCommandByteLength += 4;
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
                        bc.InputCommandByteLength += bc.CommandNameLength;
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
                        bc.InputCommandByteLength += 4;
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
                        if (bc.ParametersLength < 0 || bc.ParametersLength > Buffer.Length) { throw new InvalidOperationException("ParametersLengthOutOfBound"); }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        bc.InputCommandByteLength += 4;
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
                        bc.InputCommandByteLength += bc.ParametersLength;
                        var cmd = new Command { CommandName = bc.CommandName, CommandHash = bc.CommandHash, Parameters = Parameters, ByteLength = bc.InputCommandByteLength };
                        var r = new TryShiftResult { Command = cmd, Position = Position + bc.ParametersLength };
                        bc.CommandNameLength = 0;
                        bc.CommandName = null;
                        bc.CommandHash = 0;
                        bc.ParametersLength = 0;
                        bc.InputCommandByteLength = 0;
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
}
