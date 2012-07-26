using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.CodeDom.Compiler;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Firefly;
using Firefly.Streaming;
using Firefly.Mapping.XmlText;
using Firefly.TextEncoding;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
using Yuki.ObjectSchema;
using OS = Yuki.ObjectSchema;
using Yuki.ObjectSchema.CSharp;
using Yuki.ObjectSchema.CSharpBinary;
using Yuki.ObjectSchema.CSharpJson;
using Communication.Net;

namespace TcpSendReceive
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private StreamedAsyncSocket sock;
        private void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            var Success = false;

            try
            {
                if (sock != null) { throw new InvalidOperationException(); }

                Button_Connect.IsEnabled = false;
                Button_Listen.IsEnabled = false;
                Button_Disconnect.IsEnabled = true;
                TextBox_IP.IsEnabled = false;
                TextBox_Port.IsEnabled = false;

                EndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Parse(TextBox_IP.Text), int.Parse(TextBox_Port.Text));
                sock = new StreamedAsyncSocket(new Socket(RemoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp));
                Action Completed = () => this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    Button_Send.IsEnabled = true;
                    StartReceive();
                }));
                Action<SocketError> Faulted = se => this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, (new SocketException((int)se)).Message, "Error");
                    Button_Disconnect_Click(null, null);
                }));
                sock.ConnectAsync(RemoteEndPoint, Completed, Faulted);

                Success = true;
            }
            finally
            {
                if (!Success)
                {
                    Button_Disconnect_Click(null, null);
                }
            }
        }

        private void Button_Listen_Click(object sender, RoutedEventArgs e)
        {
            var Success = false;

            try
            {
                if (sock != null) { throw new InvalidOperationException(); }

                Button_Connect.IsEnabled = false;
                Button_Listen.IsEnabled = false;
                Button_Disconnect.IsEnabled = true;
                TextBox_IP.IsEnabled = false;
                TextBox_Port.IsEnabled = false;

                AddressFamily AddressFamily = AddressFamily.InterNetwork;
                try
                {
                    AddressFamily = IPAddress.Parse(TextBox_IP.Text).AddressFamily;
                }
                catch
                {
                }
                sock = new StreamedAsyncSocket(new Socket(AddressFamily, SocketType.Stream, ProtocolType.Tcp));
                if (AddressFamily == AddressFamily.InterNetwork)
                {
                    sock.Bind(new IPEndPoint(IPAddress.Any, int.Parse(TextBox_Port.Text)));
                }
                else if (AddressFamily == AddressFamily.InterNetworkV6)
                {
                    sock.Bind(new IPEndPoint(IPAddress.IPv6Any, int.Parse(TextBox_Port.Text)));
                }
                else
                {
                    throw new InvalidOperationException();
                }
                sock.Listen(1);
                Action<StreamedAsyncSocket> Completed = s => this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    CloseSocket();
                    sock = s;
                    Button_Send.IsEnabled = true;
                    StartReceive();
                }));
                Action<SocketError> Faulted = se => this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, (new SocketException((int)se)).Message, "Error");
                    Button_Disconnect_Click(null, null);
                }));
                sock.AcceptAsync(Completed, Faulted);

                Success = true;
            }
            finally
            {
                if (!Success)
                {
                    Button_Disconnect_Click(null, null);
                }
            }
        }

        private void CloseSocket()
        {
            if (sock != null)
            {
                try
                {
                    sock.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
                try
                {
                    sock.Close();
                }
                catch
                {
                }
                try
                {
                    sock.Dispose();
                }
                catch
                {
                }
                sock = null;
            }
        }
        private void Button_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (sock != null)
            {
                Action Completed = () => this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    CloseSocket();

                    TextBox_Port.IsEnabled = true;
                    TextBox_IP.IsEnabled = true;
                    Button_Send.IsEnabled = false;
                    Button_Disconnect.IsEnabled = false;
                    Button_Listen.IsEnabled = true;
                    Button_Connect.IsEnabled = true;
                }));
                Action<SocketError> Faulted = se => this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, (new SocketException((int)se)).Message, "Error");
                    Button_Disconnect_Click(null, null);
                }));
                sock.DisconnectAsync(Completed, Faulted);
            }

            CloseSocket();

            TextBox_Port.IsEnabled = true;
            TextBox_IP.IsEnabled = true;
            Button_Send.IsEnabled = false;
            Button_Disconnect.IsEnabled = false;
            Button_Listen.IsEnabled = true;
            Button_Connect.IsEnabled = true;
        }

        private static Regex r = new Regex(@"^/(?<CommandName>\S+)(\s+(?<Params>.*))?$", RegexOptions.ExplicitCapture); //Regex是线程安全的
        private void Button_Send_Click(object sender, RoutedEventArgs e)
        {
            if (sock == null) { throw new InvalidOperationException(); }

            var Str = TextBox_Send.Text;
            var m = GetMode();
            Byte[] SendBuffer;
            if (m == Mode.Escaped)
            {
                Str = Str.Descape();
                SendBuffer = Encoding.UTF8.GetBytes(Str);
            }
            else if (m == Mode.Binary)
            {
                var Sche = Schema();
                var a = SchemaAssembly();
                var Lines = Regex.Split(Str, @"\n|\r\n").Select(l => l.Trim(' ')).Where(l => l != "").ToArray();
                using (var s = Streams.CreateMemoryStream())
                {
                    foreach (var Line in Lines)
                    {
                        var ma = r.Match(Line);
                        var CommandName = ma.Result("${CommandName}");
                        var CommandDef = Sche.Types.Where(t => t.Name() == CommandName && t.Version() == "").Single();
                        var jParameters = JToken.Parse(ma.Result("${Params}"));
                        var tRequest = a.GetType(CommandName + "Request");
                        var oRequest = a.GetType("JsonTranslator").GetMethod(CommandName + "RequestFromJson").Invoke(null, new Object[] { jParameters });
                        var Parameters = (Byte[])a.GetType("BinaryTranslator").GetMethod("Serialize").MakeGenericMethod(tRequest).Invoke(null, new Object[] { oRequest });
                        var CommandHash = (UInt32)(Sche.GetSubSchema(new TypeDef[] { CommandDef }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));

                        var CommandNameBytes = TextEncoding.UTF16.GetBytes(CommandName);
                        s.WriteInt32(CommandNameBytes.Length);
                        s.Write(CommandNameBytes);
                        s.WriteUInt32(CommandHash);
                        s.WriteInt32(Parameters.Length);
                        s.Write(Parameters);
                    }
                    s.Position = 0;
                    SendBuffer = s.Read((int)(s.Length));
                }
            }
            else if (m == Mode.Line)
            {
                var Lines = Regex.Split(Str, @"\n|\r\n").Select(l => l.Trim(' ')).Where(l => l != "").ToArray();
                Str = String.Join("\r\n", Lines) + "\r\n";
                SendBuffer = Encoding.UTF8.GetBytes(Str);
            }
            else
            {
                throw new InvalidOperationException();
            }

            Action Completed = () => this.Dispatcher.BeginInvoke((Action)(() =>
            {
            }));
            Action<SocketError> Faulted = se => this.Dispatcher.BeginInvoke((Action)(() =>
            {
                MessageBox.Show(this, (new SocketException((int)se)).Message, "Error");
                Button_Disconnect_Click(null, null);
            }));
            sock.SendAsync(SendBuffer, 0, SendBuffer.Length, Completed, Faulted);
        }

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            TextBox_Receive.Text = "";
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

        private List<Byte> AcceptBuffer = new List<Byte>();
        private int NumRemain = 0;
        private int FirstPosition = 0;
        private void AcceptBytes(Byte[] l)
        {
            var m = GetMode();
            if (m == Mode.Escaped)
            {
                foreach (var b in l)
                {
                    if (NumRemain == 0)
                    {
                        if (b.Bits(7, 5) == 3 * 2)
                        {
                            NumRemain = 2;
                        }
                        else if (b.Bits(7, 4) == 7 * 2)
                        {
                            NumRemain = 3;
                        }
                        else if (b.Bits(7, 3) == 15 * 2)
                        {
                            NumRemain = 4;
                        }
                        else
                        {
                            NumRemain = 1;
                        }
                    }
                    AcceptBuffer.Add(b);
                    NumRemain -= 1;
                    if ((NumRemain == 0) && (AcceptBuffer.Count != 0))
                    {
                        var s = TextEncoding.UTF8.GetString(AcceptBuffer.ToArray());
                        TextBox_Receive.AppendText(s.Escape());
                        AcceptBuffer.Clear();
                    }
                }
            }
            else if (m == Mode.Line)
            {
                foreach (var b in l)
                {
                    if (NumRemain == 0)
                    {
                        if (b.Bits(7, 5) == 3 * 2)
                        {
                            NumRemain = 2;
                        }
                        else if (b.Bits(7, 4) == 7 * 2)
                        {
                            NumRemain = 3;
                        }
                        else if (b.Bits(7, 3) == 15 * 2)
                        {
                            NumRemain = 4;
                        }
                        else
                        {
                            NumRemain = 1;
                        }
                    }
                    AcceptBuffer.Add(b);
                    NumRemain -= 1;
                    if ((NumRemain == 0) && (b == 10 || b == 0) && (AcceptBuffer.Count != 0))
                    {
                        var s = TextEncoding.UTF8.GetString(AcceptBuffer.ToArray());
                        TextBox_Receive.AppendText(s);
                        AcceptBuffer.Clear();
                    }
                }
            }
            else if (m == Mode.Binary)
            {
                var Sche = Schema();
                var a = SchemaAssembly();

                AcceptBuffer.AddRange(l);
                while (true)
                {
                    var r = bsm.TryShift(AcceptBuffer.ToArray(), FirstPosition, AcceptBuffer.Count - FirstPosition);
                    if (r == null)
                    {
                        return;
                    }
                    FirstPosition = r.Position;
                    if (r.Command == null) { continue; }

                    var CommandName = r.Command.CommandName;
                    var CommandDef = Sche.Types.Where(t => t.Name() == CommandName && t.Version() == "").Single();
                    var CommandHash = (UInt32)(Sche.GetSubSchema(new TypeDef[] { CommandDef }, new TypeSpec[] { }).GetNonversioned().Hash().Bits(31, 0));
                    if (CommandHash != r.Command.CommandHash)
                    {
                        throw new InvalidOperationException(String.Format("Command '{0}' ReceivedHash '{1}' ExceptedHash '{2}'", r.Command.CommandName, r.Command.CommandHash, CommandHash));
                    }

                    if (CommandDef.OnClientCommand)
                    {
                        var tReply = a.GetType(CommandName + "Reply");
                        var oParameters = a.GetType("BinaryTranslator").GetMethod("Deserialize").MakeGenericMethod(tReply).Invoke(null, new Object[] { r.Command.Parameters });
                        var jParameters = (JToken)a.GetType("JsonTranslator").GetMethod(CommandName + "ReplyToJson").Invoke(null, new Object[] { oParameters });
                        var Line = String.Format("/svr {0} {1}\r\n", CommandName, jParameters.ToString(Formatting.None));
                        TextBox_Receive.AppendText(Line);
                    }
                    else if (CommandDef.OnServerCommand)
                    {
                        var tReply = a.GetType(CommandName + "Event");
                        var oParameters = a.GetType("BinaryTranslator").GetMethod("Deserialize").MakeGenericMethod(tReply).Invoke(null, new Object[] { r.Command.Parameters });
                        var jParameters = (JToken)a.GetType("JsonTranslator").GetMethod(CommandName + "EventToJson").Invoke(null, new Object[] { oParameters });
                        var Line = String.Format("/svr {0} {1}\r\n", CommandName, jParameters.ToString(Formatting.None));
                        TextBox_Receive.AppendText(Line);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private Byte[] ReceiveBuffer = new Byte[2048];
        private void StartReceive()
        {
            var IsConnected = !(sock.InnerSocket.Poll(50, SelectMode.SelectRead) && sock.InnerSocket.Available == 0);
            if (!IsConnected)
            {
                MessageBox.Show(this, (new SocketException((int)SocketError.ConnectionReset)).Message, "Error");
                Button_Disconnect_Click(null, null);
                return;
            }

            Action<int> Completed = c => this.Dispatcher.BeginInvoke((Action)(() =>
            {
                AcceptBytes(ReceiveBuffer.Take(c).ToArray());

                if (sock != null)
                {
                    StartReceive();
                }
            }));
            Action<SocketError> Faulted = se => this.Dispatcher.BeginInvoke((Action)(() =>
            {
                MessageBox.Show(this, (new SocketException((int)se)).Message, "Error");
                Button_Disconnect_Click(null, null);
            }));
            sock.ReceiveAsync(ReceiveBuffer, 0, ReceiveBuffer.Length, Completed, Faulted);
        }

        private static String ConfigurationFilePath = Assembly.GetEntryAssembly().Location + ".ini";
        private static Configuration c;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigurationFilePath))
            {
                var x = TreeFile.ReadFile(ConfigurationFilePath);
                c = (new XmlSerializer()).Read<Configuration>(x);
            }
            else
            {
                c = new Configuration();
                c.IP = "127.0.0.1";
                c.Port = 8001;
                c.Mode = Mode.Binary;
                c.SchemaPaths = new List<String>
                {
                    "../Examples/Communication/Schema"
                };
            }

            TextBox_IP.Text = c.IP;
            TextBox_Port.Text = c.Port.ToInvariantString();
            ComboBox_Mode.SelectedIndex = (int)c.Mode;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            c.IP = TextBox_IP.Text;
            c.Port = NumericStrings.InvariantParseInt32(TextBox_Port.Text);
            c.Mode = GetMode();

            var x = (new XmlSerializer()).Write(c);
            try
            {
                TreeFile.WriteFile(ConfigurationFilePath, x);
            }
            catch
            {
            }
        }
        private Mode GetMode()
        {
            return (Mode)(ComboBox_Mode.SelectedIndex);
        }

        private static OS.Schema os = null;
        private static Assembly osa = null;
        private static OS.Schema Schema()
        {
            if (os != null) { return os; }
            var osl = new ObjectSchemaLoader();
            foreach (var ObjectSchemaPath in c.SchemaPaths)
            {
                if (Directory.Exists(ObjectSchemaPath))
                {
                    foreach (var f in Directory.GetFiles(ObjectSchemaPath, "*.tree", SearchOption.AllDirectories).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                    {
                        osl.LoadType(f);
                    }
                }
                else
                {
                    osl.LoadType(ObjectSchemaPath);
                }
            }
            os = osl.GetResult();
            os.Verify();
            return os;
        }
        private static Assembly SchemaAssembly()
        {
            if (osa != null) { return osa; }
            var s = Schema();
            var Codes = new String[] { s.CompileToCSharp(), s.CompileToCSharpBinary(), s.CompileToCSharpJson() };

            var cp = new CompilerParameters();
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(System.CodeDom.Compiler.CodeCompiler)).Location); //System.dll
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(System.Linq.Enumerable)).Location); //System.Core.dll
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(Firefly.N32)).Location); //Firefly.Core.dll
            cp.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(Newtonsoft.Json.Linq.JObject)).Location); //Newtonsoft.Json.dll
            cp.GenerateExecutable = false;
            cp.GenerateInMemory = true;
            var cr = (new Microsoft.CSharp.CSharpCodeProvider()).CompileAssemblyFromSource(cp, Codes);
            if (cr.Errors.HasErrors)
            {
                var l = new List<String>();
                l.Add("CodeCompileFailed");
                foreach (var e in cr.Errors.Cast<CompilerError>())
                {
                    l.Add(e.ToString());
                }
                throw new InvalidOperationException(String.Join(Environment.NewLine, l.ToArray()));
            }
            osa = cr.CompiledAssembly;
            return osa;
        }
    }
}
