using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.Json;

namespace Client
{
    public class JsonLinePacketClientContext
    {
        public ArraySegment<Byte> Buffer = new ArraySegment<Byte>(new Byte[8 * 1024], 0, 0);
    }

    public class JsonLinePacketClient<TContext> : ITcpVirtualTransportClient
    {
        private class JsonSender : IJsonSender
        {
            public Action<Byte[]> SendBytes;

            public void Send(String CommandName, UInt32 CommandHash, String Parameters)
            {
                var Message = "/" + CommandName + "@" + CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + " " + Parameters + "\r\n";
                var Bytes = TextEncoding.UTF8.GetBytes(Message);
                SendBytes(Bytes);
            }
        }

        private TContext c;
        private JsonClient<TContext> jc;
        private JsonLinePacketClientContext cc;
        public JsonLinePacketClient(TContext c, IClientImplementation<TContext> ApplicationClientImplementation, JsonLinePacketClientContext cc)
        {
            this.c = c;
            this.jc = new JsonClient<TContext>(new JsonSender { SendBytes = Bytes => { if (this.ClientMethod != null) { ClientMethod(Bytes); } } }, ApplicationClientImplementation);
            this.cc = cc;
        }

        public IClient GetApplicationClient
        {
            get
            {
                return jc;
            }
        }

        public ArraySegment<Byte> GetReadBuffer()
        {
            return cc.Buffer;
        }

        public TcpVirtualTransportClientHandleResult Handle(int Count)
        {
            var ret = TcpVirtualTransportClientHandleResult.CreateContinue();

            var Buffer = cc.Buffer.Array;
            var FirstPosition = cc.Buffer.Offset;
            var BufferLength = cc.Buffer.Offset + cc.Buffer.Count;
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
                var Line = TextEncoding.UTF8.GetString(LineBytes, 0, LineBytes.Length);
                var cmd = ParseCommand(Line);
                if (cmd.OnHasValue)
                {
                    var CommandName = cmd.HasValue.CommandName;
                    var CommandHash = cmd.HasValue.CommandHash;
                    var Parameters = cmd.HasValue.Parameters;
                    ret = TcpVirtualTransportClientHandleResult.CreateCommand(new TcpVirtualTransportClientHandleResultCommand
                    {
                        CommandName = CommandName,
                        HandleResult = () => jc.HandleResult(c, CommandName, CommandHash, Parameters)
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
                cc.Buffer = new ArraySegment<Byte>(Buffer, 0, 0);
            }
            else
            {
                cc.Buffer = new ArraySegment<Byte>(Buffer, FirstPosition, BufferLength - FirstPosition);
            }

            return ret;
        }

        public UInt64 Hash { get { return jc.Hash; } }
        public event Action<Byte[]> ClientMethod;

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
            var CommandHash = UInt32.Parse(mName.Result("${CommandHash}"), System.Globalization.NumberStyles.HexNumber);
            var Parameters = m.Result("${Params}") ?? "";
            if (Parameters == "") { Parameters = "{}"; }

            return new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters };
        }
    }
}
