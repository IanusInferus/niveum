﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Communication;
using Communication.Json;

namespace Client
{
    public class JsonLinePacketClient : ITcpVirtualTransportClient
    {
        private class Context
        {
            public ArraySegment<Byte> ReadBuffer = new ArraySegment<Byte>(new Byte[8 * 1024], 0, 0);
            public List<Byte> WriteBuffer = new List<Byte>();
        }

        private class JsonSender : IJsonSender
        {
            public Action<Byte[]> SendBytes;

            public void Send(String CommandName, UInt32 CommandHash, String Parameters)
            {
                var Message = "/" + CommandName + "@" + CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + " " + Parameters + "\r\n";
                var Bytes = System.Text.Encoding.UTF8.GetBytes(Message);
                SendBytes(Bytes);
            }
        }

        private Context c;
        private JsonSerializationClient jc;
        public JsonLinePacketClient()
        {
            this.c = new Context();
            Action<Byte[]> SendBytes = Bytes =>
            {
                c.WriteBuffer.AddRange(Bytes);
                if (this.ClientMethod != null) { ClientMethod(); }
            };
            this.jc = new JsonSerializationClient(new JsonSender { SendBytes = SendBytes });
        }

        public IApplicationClient ApplicationClient
        {
            get
            {
                return jc.GetApplicationClient();
            }
        }

        public ArraySegment<Byte> GetReadBuffer()
        {
            return c.ReadBuffer;
        }

        public Byte[] TakeWriteBuffer()
        {
            var r = c.WriteBuffer.ToArray();
            c.WriteBuffer = new List<Byte>();
            return r;
        }

        public TcpVirtualTransportClientHandleResult Handle(int Count)
        {
            var ret = TcpVirtualTransportClientHandleResult.CreateContinue();

            var Buffer = c.ReadBuffer.Array;
            var FirstPosition = c.ReadBuffer.Offset;
            var BufferLength = c.ReadBuffer.Offset + c.ReadBuffer.Count;
            var CheckPosition = FirstPosition;
            BufferLength += Count;

            var LineFeedPosition = -1;
            for (int i = CheckPosition; i < BufferLength; i += 1)
            {
                Byte b = Buffer[i];
                if (b == '\n')
                {
                    LineFeedPosition = i;
                    break;
                }
            }
            if (LineFeedPosition >= FirstPosition)
            {
                var LineBytes = Buffer.Skip(FirstPosition).Take(LineFeedPosition - FirstPosition).Where(b => b != '\r').ToArray();
                var Line = System.Text.Encoding.UTF8.GetString(LineBytes, 0, LineBytes.Length);
                var cmd = ParseCommand(Line);
                if (cmd.OnHasValue)
                {
                    var CommandName = cmd.HasValue.CommandName;
                    var CommandHash = cmd.HasValue.CommandHash;
                    var Parameters = cmd.HasValue.Parameters;
                    ret = TcpVirtualTransportClientHandleResult.CreateCommand(new TcpVirtualTransportClientHandleResultCommand
                    {
                        CommandName = CommandName,
                        HandleResult = () => jc.HandleResult(CommandName, CommandHash, Parameters)
                    });
                }
                else
                {
                    throw new InvalidOperationException();
                }
                FirstPosition = LineFeedPosition + 1;
                CheckPosition = FirstPosition;
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

        public UInt64 Hash { get { return ApplicationClient.Hash; } }
        public event Action ClientMethod;

        private static Regex r = new Regex(@"^/svr\s+(?<Name>\S+)(\s+(?<Params>.*))?$", RegexOptions.ExplicitCapture); //Regex是线程安全的
        private static Regex rName = new Regex(@"^(?<CommandName>.*?)@(?<CommandHash>.*)$", RegexOptions.ExplicitCapture); //Regex是线程安全的

        private class Command
        {
            public String CommandName;
            public UInt32 CommandHash;
            public String Parameters;
        }
        private Optional<Command> ParseCommand(String CommandLine)
        {
            var m = r.Match(CommandLine);
            if (!m.Success) { return Optional<Command>.Empty; }

            var Name = m.Result("${Name}");
            var mName = rName.Match(Name);
            if (!mName.Success) { return Optional<Command>.Empty; }
            var CommandName = mName.Result("${CommandName}");
            var CommandHash = UInt32.Parse(mName.Result("${CommandHash}"), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            var Parameters = m.Result("${Params}") ?? "";
            if (Parameters == "") { Parameters = "{}"; }

            return new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters };
        }
    }
}