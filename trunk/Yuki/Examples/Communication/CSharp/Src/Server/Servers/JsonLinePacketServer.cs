using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly;
using Firefly.Streaming;
using Firefly.TextEncoding;
using Communication;
using Communication.Json;

namespace Server
{
    public class JsonLinePacketServerContext
    {
        public ArraySegment<Byte> Buffer = new ArraySegment<Byte>(new Byte[8 * 1024], 0, 0);
    }

    public class JsonLinePacketServer<TContext> : IVirtualTransportServer<TContext>
    {
        public delegate Boolean CheckCommandAllowedDelegate(TContext c, String CommandName);

        private JsonServer<TContext> sv;
        private Func<TContext, JsonLinePacketServerContext> Acquire;
        private CheckCommandAllowedDelegate CheckCommandAllowed;
        public JsonLinePacketServer(IServerImplementation<TContext> ApplicationServer, Func<TContext, JsonLinePacketServerContext> Acquire, CheckCommandAllowedDelegate CheckCommandAllowed)
        {
            this.sv = new JsonServer<TContext>(ApplicationServer);
            this.Acquire = Acquire;
            this.CheckCommandAllowed = CheckCommandAllowed;
            this.sv.ServerEvent += (c, CommandName, CommandHash, Parameters) =>
            {
                if (this.ServerEvent != null)
                {
                    var Bytes = TextEncoding.UTF8.GetBytes(String.Format(@"/svr {0} {1}" + "\r\n", CommandName + "@" + CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), Parameters));
                    this.ServerEvent(c, Bytes);
                }
            };
        }

        public ArraySegment<Byte> GetReadBuffer(TContext c)
        {
            var bc = Acquire(c);
            return bc.Buffer;
        }

        public VirtualTransportServerHandleResult Handle(TContext c, int Count)
        {
            var bc = Acquire(c);

            var ret = VirtualTransportServerHandleResult.CreateContinue();

            var Buffer = bc.Buffer.Array;
            var FirstPosition = bc.Buffer.Offset;
            var BufferLength = bc.Buffer.Offset + bc.Buffer.Count;
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
                    if ((CommandHash.OnHasValue ? sv.HasCommand(CommandName, CommandHash.HasValue) : sv.HasCommand(CommandName)) && (CheckCommandAllowed != null ? CheckCommandAllowed(c, CommandName) : true))
                    {
                        if (CommandHash.OnHasValue)
                        {
                            ret = VirtualTransportServerHandleResult.CreateCommand(new VirtualTransportServerHandleResultCommand
                            {
                                CommandName = CommandName,
                                ExecuteCommand = () => TextEncoding.UTF8.GetBytes(sv.ExecuteCommand(c, CommandName, CommandHash.HasValue, Parameters)),
                                PackageOutput = OutputParameters =>
                                {
                                    var Bytes = TextEncoding.UTF8.GetBytes(String.Format(@"/svr {0} ", CommandName + "@" + CommandHash.HasValue.ToString("X8", System.Globalization.CultureInfo.InvariantCulture))).Concat(OutputParameters).Concat(TextEncoding.UTF8.GetBytes("\r\n")).ToArray();
                                    return Bytes;
                                }
                            });
                        }
                        else
                        {
                            ret = VirtualTransportServerHandleResult.CreateCommand(new VirtualTransportServerHandleResultCommand
                            {
                                CommandName = CommandName,
                                ExecuteCommand = () => TextEncoding.UTF8.GetBytes(sv.ExecuteCommand(c, CommandName, Parameters)),
                                PackageOutput = OutputParameters =>
                                {
                                    var Bytes = TextEncoding.UTF8.GetBytes(String.Format(@"/svr {0} ", CommandName)).Concat(OutputParameters).Concat(TextEncoding.UTF8.GetBytes("\r\n")).ToArray();
                                    return Bytes;
                                }
                            });
                        }
                    }
                    else
                    {
                        ret = VirtualTransportServerHandleResult.CreateBadCommand(new VirtualTransportServerHandleResultBadCommand { CommandName = CommandName });
                    }
                }
                else if (cmd.OnNotHasValue)
                {
                    ret = VirtualTransportServerHandleResult.CreateBadCommandLine(new VirtualTransportServerHandleResultBadCommandLine { CommandLine = Line });
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

        private static Regex r = new Regex(@"^/(?<Name>\S+)(\s+(?<Params>.*))?$", RegexOptions.ExplicitCapture); //Regex是线程安全的
        private static Regex rName = new Regex(@"^(?<CommandName>.*?)@(?<CommandHash>.*)$", RegexOptions.ExplicitCapture); //Regex是线程安全的

        private class Command
        {
            public String CommandName;
            public Optional<UInt32> CommandHash;
            public String Parameters;
        }
        private Optional<Command> ParseCommand(String CommandLine)
        {
            var m = r.Match(CommandLine);
            if (!m.Success) { return Optional<Command>.Empty; }

            var Name = m.Result("${Name}");
            var mName = rName.Match(Name);
            String CommandName = Name;
            var CommandHash = Optional<UInt32>.Empty;
            if (mName.Success)
            {
                CommandName = mName.Result("${CommandName}");
                CommandHash = UInt32.Parse(mName.Result("${CommandHash}"), System.Globalization.NumberStyles.HexNumber);
            }
            var Parameters = m.Result("${Params}") ?? "";
            if (Parameters == "") { Parameters = "{}"; }

            return new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters };
        }
    }
}
