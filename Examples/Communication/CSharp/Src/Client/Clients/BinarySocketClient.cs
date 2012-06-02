using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Communication.Binary;

namespace Client
{
    public sealed class BinarySocketClient : IBinarySender, IDisposable
    {
        private IClientImplementation<ClientContext> ci;
        public BinaryClient<ClientContext> InnerClient { get; private set; }
        public ClientContext Context { get; private set; }

        private IPEndPoint RemoteEndPoint;
        private LockedVariable<StreamedAsyncSocket> Socket = new LockedVariable<StreamedAsyncSocket>(null);

        public BinarySocketClient(IPEndPoint RemoteEndPoint, IClientImplementation<ClientContext> ci)
        {
            this.RemoteEndPoint = RemoteEndPoint;
            Socket = new LockedVariable<StreamedAsyncSocket>(new StreamedAsyncSocket(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)));
            this.ci = ci;
            InnerClient = new BinaryClient<ClientContext>(this, ci);
            Context = new ClientContext();
            Context.DequeueCallback = InnerClient.DequeueCallback;
        }

        public void Connect()
        {
            Socket.DoAction(sock => sock.InnerSocket.Connect(RemoteEndPoint));
        }

        public Socket GetSocket()
        {
            return Socket.Check(ss => ss).Branch(ss => ss != null, ss => ss.InnerSocket, ss => null);
        }

        void IBinarySender.Send(String CommandName, UInt32 CommandHash, Byte[] Parameters)
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
            Socket.DoAction(sock => sock.InnerSocket.Send(Bytes));
        }

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

        private class BufferStateMachine
        {
            private int State;
            // 0 初始状态
            // 1 已读取NameLength
            // 2 已读取CommandHash
            // 3 已读取Name
            // 4 已读取ParametersLength

            private Int32 CommandNameLength;
            private String CommandName;
            private UInt32 CommandHash;
            private Int32 ParametersLength;

            public BufferStateMachine()
            {
                State = 0;
            }

            public TryShiftResult TryShift(Byte[] Buffer, int Position, int Length)
            {
                if (State == 0)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            CommandNameLength = s.ReadInt32();
                        }
                        if (CommandNameLength < 0 || CommandNameLength > 128) { throw new InvalidOperationException(); }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        State = 1;
                        return r;
                    }
                    return null;
                }
                else if (State == 1)
                {
                    if (Length >= CommandNameLength)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            CommandName = TextEncoding.UTF16.GetString(s.Read(CommandNameLength));
                        }
                        var r = new TryShiftResult { Command = null, Position = Position + CommandNameLength };
                        State = 2;
                        return r;
                    }
                    return null;
                }
                else if (State == 2)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            CommandHash = s.ReadUInt32();
                        }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        State = 3;
                        return r;
                    }
                    return null;
                }
                if (State == 3)
                {
                    if (Length >= 4)
                    {
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            ParametersLength = s.ReadInt32();
                        }
                        if (ParametersLength < 0 || ParametersLength > 8 * 1024) { throw new InvalidOperationException(); }
                        var r = new TryShiftResult { Command = null, Position = Position + 4 };
                        State = 4;
                        return r;
                    }
                    return null;
                }
                else if (State == 4)
                {
                    if (Length >= ParametersLength)
                    {
                        Byte[] Parameters;
                        using (var s = new ByteArrayStream(Buffer, Position, Length))
                        {
                            Parameters = s.Read(ParametersLength);
                        }
                        var cmd = new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters };
                        var r = new TryShiftResult { Command = cmd, Position = Position + ParametersLength };
                        CommandNameLength = 0;
                        CommandName = null;
                        CommandHash = 0;
                        ParametersLength = 0;
                        State = 0;
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

        private BufferStateMachine bsm = new BufferStateMachine();
        private Byte[] Buffer = new Byte[8 * 1024];
        private int BufferLength = 0;

        private Boolean IsSocketErrorKnown(SocketError se)
        {
            if (se == SocketError.ConnectionAborted) { return true; }
            if (se == SocketError.ConnectionReset) { return true; }
            if (se == SocketError.Shutdown) { return true; }
            if (se == SocketError.OperationAborted) { return true; }
            if (se == SocketError.Interrupted) { return true; }
            return false;
        }

        public void Receive(Action<SocketError> UnknownFaulted)
        {
            Action<SocketError> Faulted = se =>
            {
                if (!IsSocketErrorKnown(se))
                {
                    UnknownFaulted(se);
                }
            };

            Action<int> Completed = null;
            Completed = Count =>
            {
                if (Count == 0)
                {
                    return;
                }
                var FirstPosition = 0;
                BufferLength += Count;
                while (true)
                {
                    var r = bsm.TryShift(Buffer, FirstPosition, BufferLength - FirstPosition);
                    if (r == null)
                    {
                        break;
                    }
                    FirstPosition = r.Position;

                    if (r.Command != null)
                    {
                        var cmd = r.Command;
                        InnerClient.HandleResult(Context, cmd.CommandName, cmd.CommandHash, cmd.Parameters);
                    }
                }
                if (FirstPosition > 0)
                {
                    var CopyLength = BufferLength - FirstPosition;
                    for (int i = 0; i < CopyLength; i += 1)
                    {
                        Buffer[i] = Buffer[FirstPosition + i];
                    }
                    BufferLength = CopyLength;
                }
                Socket.DoAction
                (
                    sock =>
                    {
                        if (sock == null) { return; }
                        sock.ReceiveAsync(Buffer, BufferLength, Buffer.Length - BufferLength, Completed, Faulted);
                    }
                );
            };

            Socket.DoAction
            (
                sock =>
                {
                    if (sock == null) { return; }
                    sock.ReceiveAsync(Buffer, BufferLength, Buffer.Length - BufferLength, Completed, Faulted);
                }
            );
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            Socket.Update
            (
                s =>
                {
                    if (s != null)
                    {
                        try
                        {
                            s.Shutdown(SocketShutdown.Both);
                        }
                        catch
                        {
                        }
                        try
                        {
                            s.Close();
                        }
                        catch
                        {
                        }
                        try
                        {
                            s.Dispose();
                        }
                        catch
                        {
                        }
                    }
                    return null;
                }
            );
        }
    }
}
