using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Firefly.TextEncoding;

namespace Server
{
    public partial class Tcp<TServerContext>
    {
        public class JsonLinePacketServer : ITcpVirtualTransportServer
        {
            private class Context
            {
                public ArraySegment<Byte> ReadBuffer = new ArraySegment<Byte>(new Byte[8 * 1024], 0, 0);
                public Object WriteBufferLockee = new Object();
                public List<Byte[]> WriteBuffer = new List<Byte[]>();
            }

            public delegate Boolean CheckCommandAllowedDelegate(String CommandName);

            private IJsonSerializationServerAdapter ss;
            private Context c;
            private CheckCommandAllowedDelegate CheckCommandAllowed;
            public JsonLinePacketServer(IJsonSerializationServerAdapter SerializationServerAdapter, CheckCommandAllowedDelegate CheckCommandAllowed)
            {
                this.ss = SerializationServerAdapter;
                this.c = new Context();
                this.CheckCommandAllowed = CheckCommandAllowed;
                this.ss.ServerEvent += (CommandName, CommandHash, Parameters) =>
                {
                    var Bytes = TextEncoding.UTF8.GetBytes(String.Format(@"/svr {0} {1}" + "\r\n", CommandName + "@" + CommandHash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), Parameters));
                    lock (c.WriteBufferLockee)
                    {
                        c.WriteBuffer.Add(Bytes);
                    }
                    if (OutputByteLengthReport != null)
                    {
                        OutputByteLengthReport(CommandName, Bytes.Length);
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

            public TcpVirtualTransportServerHandleResult Handle(int Count)
            {
                var ret = TcpVirtualTransportServerHandleResult.CreateContinue();

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
                    var Line = TextEncoding.UTF8.GetString(LineBytes, 0, LineBytes.Length);
                    var cmd = ParseCommand(Line);
                    if (cmd.OnHasValue)
                    {
                        var CommandName = cmd.HasValue.CommandName;
                        var CommandHash = cmd.HasValue.CommandHash;
                        var Parameters = cmd.HasValue.Parameters;
                        if (InputByteLengthReport != null)
                        {
                            InputByteLengthReport(CommandName, LineBytes.Length);
                        }
                        if ((CommandHash.OnHasValue ? ss.HasCommand(CommandName, CommandHash.HasValue) : ss.HasCommand(CommandName)) && (CheckCommandAllowed != null ? CheckCommandAllowed(CommandName) : true))
                        {
                            if (CommandHash.OnHasValue)
                            {
                                ret = TcpVirtualTransportServerHandleResult.CreateCommand(new TcpVirtualTransportServerHandleResultCommand
                                {
                                    CommandName = CommandName,
                                    ExecuteCommand = (OnSuccess, OnFailure) =>
                                    {
                                        Action<String> OnSuccessInner = OutputParameters =>
                                        {
                                            var Bytes = TextEncoding.UTF8.GetBytes(String.Format(@"/svr {0} {1}" + "\r\n", CommandName + "@" + CommandHash.HasValue.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), OutputParameters));
                                            lock (c.WriteBufferLockee)
                                            {
                                                c.WriteBuffer.Add(Bytes);
                                            }
                                            if (OutputByteLengthReport != null)
                                            {
                                                OutputByteLengthReport(CommandName, Bytes.Length);
                                            }
                                            OnSuccess();
                                        };
                                        ss.ExecuteCommand(CommandName, CommandHash.HasValue, Parameters, OnSuccessInner, OnFailure);
                                    }
                                });
                            }
                            else
                            {
                                ret = TcpVirtualTransportServerHandleResult.CreateCommand(new TcpVirtualTransportServerHandleResultCommand
                                {
                                    CommandName = CommandName,
                                    ExecuteCommand = (OnSuccess, OnFailure) =>
                                    {
                                        Action<String> OnSuccessInner = OutputParameters =>
                                        {
                                            var Bytes = TextEncoding.UTF8.GetBytes(String.Format(@"/svr {0} {1}" + "\r\n", CommandName, OutputParameters));
                                            lock (c.WriteBufferLockee)
                                            {
                                                c.WriteBuffer.Add(Bytes);
                                            }
                                            if (OutputByteLengthReport != null)
                                            {
                                                OutputByteLengthReport(CommandName, Bytes.Length);
                                            }
                                            OnSuccess();
                                        };
                                        ss.ExecuteCommand(CommandName, Parameters, OnSuccessInner, OnFailure);
                                    }
                                });
                            }
                        }
                        else
                        {
                            ret = TcpVirtualTransportServerHandleResult.CreateBadCommand(new TcpVirtualTransportServerHandleResultBadCommand { CommandName = CommandName });
                        }
                    }
                    else if (cmd.OnNotHasValue)
                    {
                        ret = TcpVirtualTransportServerHandleResult.CreateBadCommandLine(new TcpVirtualTransportServerHandleResultBadCommandLine { CommandLine = Line });
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

            public UInt64 Hash { get { return ss.Hash; } }
            public event Action ServerEvent;
            public event Action<String, int> InputByteLengthReport;
            public event Action<String, int> OutputByteLengthReport;

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
                    UInt32 ch;
                    if (!UInt32.TryParse(mName.Result("${CommandHash}"), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ch))
                    {
                        return Optional<Command>.Empty;
                    }
                    CommandHash = ch;
                }
                var Parameters = m.Result("${Params}") ?? "";
                if (Parameters == "") { Parameters = "{}"; }

                return new Command { CommandName = CommandName, CommandHash = CommandHash, Parameters = Parameters };
            }
        }
    }
}
