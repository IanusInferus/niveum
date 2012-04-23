//==========================================================================
//
//  File:        MainWindow.xaml.cs
//  Location:    TcpSendReceive <Visual C#>
//  Description: TCP发送接收器
//  Version:     2012.04.19.
//  Copyright(C) 上海幻达网络科技有限公司 2011
//
//==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using Firefly;
using Firefly.Mapping.XmlText;
using Firefly.Texting;
using Firefly.Texting.TreeFormat;
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

        private void Button_Send_Click(object sender, RoutedEventArgs e)
        {
            if (sock == null) { throw new InvalidOperationException(); }

            var Str = TextBox_Send.Text;
            if (CheckBox_AsLine.IsChecked == true)
            {
                Str = Str.Trim('\r', '\n') + "\r\n";
            }
            else
            {
                Str = Str.Descape();
            }
            Byte[] SendBuffer = Encoding.UTF8.GetBytes(Str);

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

        private Boolean AsLine
        {
            get
            {
                if (!CheckBox_AsLine.IsChecked.HasValue)
                {
                    return false;
                }
                return CheckBox_AsLine.IsChecked.Value;
            }
        }
        private List<String> Strings = new List<String>();
        private void AcceptString(String s)
        {
            if (AsLine)
            {
                Strings.Add(s);
                if (s.Contains("\n") || s.Contains("\0"))
                {
                    TextBox_Receive.AppendText(String.Join("", Strings.ToArray()));
                    Strings.Clear();
                }
            }
            else
            {
                TextBox_Receive.AppendText(s.Escape());
            }
        }

        private List<Byte> Bytes = new List<Byte>();
        private int NumRemain = 0;

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
                Action<Byte[]> AcceptBytes = l => AcceptString(Encoding.UTF8.GetString(l, 0, l.Length));

                foreach (var b in ReceiveBuffer.Take(c))
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
                    Bytes.Add(b);
                    NumRemain -= 1;
                    if ((NumRemain == 0) && (Bytes.Count != 0))
                    {
                        AcceptBytes(Bytes.ToArray());
                        Bytes.Clear();
                    }
                }

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
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigurationFilePath))
            {
                var x = TreeFile.ReadFile(ConfigurationFilePath);
                var c = (new XmlSerializer()).Read<Configuration>(x);
                TextBox_IP.Text = c.IP;
                TextBox_Port.Text = c.Port;
                CheckBox_AsLine.IsChecked = c.AsLine;
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var c = new Configuration();
            c.IP = TextBox_IP.Text;
            c.Port = TextBox_Port.Text;
            c.AsLine = CheckBox_AsLine.IsChecked == true;

            var x = (new XmlSerializer()).Write(c);
            try
            {
                TreeFile.WriteFile(ConfigurationFilePath, x);
            }
            catch
            {
            }
        }
    }
}
