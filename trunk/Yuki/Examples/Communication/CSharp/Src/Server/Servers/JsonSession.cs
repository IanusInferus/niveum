using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Communication;
using Communication.BaseSystem;
using Communication.Net;
using Server.Algorithms;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class JsonSession : TcpSession<JsonServer, JsonSession>
    {
        private SessionContext Context;
        private int NumBadCommands = 0;

        public JsonSession()
        {
            Context = new SessionContext();
            Context.SessionToken = Cryptography.CreateRandom4();
            Context.Quit += StopAsync;
        }

        protected override void StartInner()
        {
            IsRunningValue.Update
            (
                b =>
                {
                    if (b) { throw new InvalidOperationException(); }
                    return true;
                }
            );

            try
            {
                Context.RemoteEndPoint = RemoteEndPoint;

                if (!Server.SessionMappings.TryAdd(Context, this))
                {
                    throw new InvalidOperationException();
                }

                SessionTask.DoAction(t => t.Start());

                if (Server.EnableLogSystem)
                {
                    Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionEnter" });
                }
                //WriteLine(@"<cross-domain-policy><allow-access-from domain=""*"" to-ports=""*""/></cross-domain-policy>" + "\0"); //发送安全策略
                PushCommand(SessionCommand.CreateReadRaw());
            }
            catch (Exception ex)
            {
                OnCriticalError(ex, new StackTrace(true));
                StopAsync();
            }
        }

        private enum SessionCommandTag
        {
            Read = 0,
            Write = 1,
            ReadRaw = 2,
            Quit = 3
        }
        private class SessionCommand
        {
            public SessionCommandTag _Tag;
            public String Read;
            public String Write;
            public Unit ReadRaw;
            public Unit Quit;

            public static SessionCommand CreateRead(String Value) { return new SessionCommand { _Tag = SessionCommandTag.Read, Read = Value }; }
            public static SessionCommand CreateWrite(String Value) { return new SessionCommand { _Tag = SessionCommandTag.Write, Write = Value }; }
            public static SessionCommand CreateReadRaw() { return new SessionCommand { _Tag = SessionCommandTag.ReadRaw, ReadRaw = new Unit() }; }
            public static SessionCommand CreateQuit() { return new SessionCommand { _Tag = SessionCommandTag.Quit, Quit = new Unit() }; }

            public Boolean OnRead { get { return _Tag == SessionCommandTag.Read; } }
            public Boolean OnWrite { get { return _Tag == SessionCommandTag.Write; } }
            public Boolean OnReadRaw { get { return _Tag == SessionCommandTag.ReadRaw; } }
            public Boolean OnQuit { get { return _Tag == SessionCommandTag.Quit; } }
        }

        private AutoResetEvent NumSessionCommandUpdated = new AutoResetEvent(false);
        private LockedVariable<int> NumSessionCommand = new LockedVariable<int>(0);
        private LockedVariable<Task> SessionTask = new LockedVariable<Task>(new Task(() => { }));
        private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
        public Boolean IsRunning
        {
            get
            {
                return IsRunningValue.Check(b => b);
            }
        }

        private void PushCommand(SessionCommand sc, TaskContinuationOptions? ContinuationOptions = null)
        {
            if (!IsRunning) { return; }

            NumSessionCommand.Update(n => n + 1);
            NumSessionCommandUpdated.Set();

            Action a;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                a = () =>
                {
                    MessageLoop(sc);
                };
            }
            else
            {
                a = () =>
                {
                    try
                    {
                        MessageLoop(sc);
                    }
                    catch (Exception ex)
                    {
                        OnCriticalError(ex, new StackTrace(true));
                        StopAsync();
                    }
                };
            }

            SessionTask.Update
            (
                t =>
                {
                    Task nt;
                    if (ContinuationOptions != null)
                    {
                        nt = t.ContinueWith(tt => a(), ContinuationOptions.Value);
                    }
                    else
                    {
                        nt = t.ContinueWith(tt => a());
                    }
                    nt = nt.ContinueWith
                    (
                        tt =>
                        {
                            NumSessionCommand.Update(n => n - 1);
                            NumSessionCommandUpdated.Set();
                        }
                    );
                    return nt;
                }
            );
        }
        private void QueueCommand(SessionCommand sc)
        {
            PushCommand(sc, TaskContinuationOptions.PreferFairness);
        }
        private Boolean IsSocketErrorKnown(SocketError se)
        {
            if (se == SocketError.ConnectionAborted) { return true; }
            if (se == SocketError.ConnectionReset) { return true; }
            if (se == SocketError.Shutdown) { return true; }
            if (se == SocketError.OperationAborted) { return true; }
            return false;
        }

        private Byte[] Buffer = new Byte[8 * 1024];
        private int BufferLength = 0;
        private Boolean MessageLoop(SessionCommand sc)
        {
            if (!IsRunningValue.Check(b => b))
            {
                if (!sc.OnWrite && !sc.OnQuit)
                {
                    return true;
                }
            }

            if (sc.OnRead)
            {
                ReadLine(sc.Read);
            }
            else if (sc.OnWrite)
            {
                var Line = sc.Write;
                var Bytes = Encoding.UTF8.GetBytes(Line + "\r\n");
                SendAsync(Bytes, 0, Bytes.Length, () => { }, se =>
                {
                    if (!IsSocketErrorKnown(se))
                    {
                        OnCriticalError((new SocketException((int)se)), new StackTrace(true));
                    }
                    StopAsync();
                });
            }
            else if (sc.OnReadRaw)
            {
                Action<int> CompletedInner = Count =>
                {
                    if (Count == 0)
                    {
                        StopAsync();
                        return;
                    }
                    var FirstPosition = 0;
                    var CheckPosition = BufferLength;
                    BufferLength += Count;
                    while (true)
                    {
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
                        if (LineFeedPosition >= 0)
                        {
                            var LineBytes = Buffer.Skip(FirstPosition).Take(LineFeedPosition - FirstPosition).Where(b => b != '\r').ToArray();
                            var Line = Encoding.UTF8.GetString(LineBytes, 0, LineBytes.Length);
                            PushCommand(SessionCommand.CreateRead(Line));
                            FirstPosition = LineFeedPosition + 1;
                            CheckPosition = FirstPosition;
                        }
                        else
                        {
                            break;
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
                    QueueCommand(SessionCommand.CreateReadRaw());
                };
                Action<int> Completed;
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Completed = CompletedInner;
                }
                else
                {
                    Completed = Count =>
                    {
                        try
                        {
                            CompletedInner(Count);
                        }
                        catch (Exception ex)
                        {
                            OnCriticalError(ex, new StackTrace(true));
                            StopAsync();
                        }
                    };
                }
                Action<SocketError> Faulted = se =>
                {
                    if (!IsSocketErrorKnown(se))
                    {
                        OnCriticalError((new SocketException((int)se)), new StackTrace(true));
                    }
                    StopAsync();
                };

                ReceiveAsync(Buffer, BufferLength, Buffer.Length - BufferLength, Completed, Faulted);
            }
            else if (sc.OnQuit)
            {
                return false;
            }
            else
            {
                throw new InvalidOperationException();
            }
            return true;
        }

        private static Regex r = new Regex(@"^/(?<CommandName>\S+)(\s+(?<Params>.*))?$", RegexOptions.ExplicitCapture); //Regex是线程安全的
        private void ReadLine(String CommandLine)
        {
            if (Server.EnableLogNormalIn)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "In", Message = CommandLine });
            }
            if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
            {
                return;
            }

            var m = r.Match(CommandLine);
            if (m.Success)
            {
                var CommandName = m.Result("${CommandName}");
                var Parameters = m.Result("${Params}") ?? "";
                if (Parameters == "") { Parameters = "{}"; }

                var sv = Server.InnerServer;
                if (sv.HasCommand(CommandName))
                {
                    Action a = () =>
                    {
                        if (Server.EnableLogPerformance)
                        {
                            var sw = new Stopwatch();
                            sw.Start();
                            var s = Server.InnerServer.ExecuteCommand(Context, CommandName, Parameters);
                            sw.Stop();
                            Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Message = String.Format("Time {0}ms", sw.ElapsedMilliseconds) });
                            WriteLine(CommandName, s);
                        }
                        else
                        {
                            var s = Server.InnerServer.ExecuteCommand(Context, CommandName, Parameters);
                            WriteLine(CommandName, s);
                        }
                    };

                    if (Debugger.IsAttached)
                    {
                        a();
                    }
                    else
                    {
                        try
                        {
                            a();
                        }
                        catch (Exception ex)
                        {
                            RaiseUnknownError(CommandName, ex, new StackTrace(true));
                        }
                    }
                }
                else
                {
                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError(CommandName, "Too many bad commands, closing transmission channel.");
                        StopAsync();
                    }
                    else
                    {
                        RaiseError(CommandName, "Not recognized.");
                    }
                }
            }
            else
            {
                NumBadCommands += 1;

                // Maximum allowed bad commands exceeded.
                if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                {
                    RaiseError(String.Format(@"""{0}""", CommandLine), "Too many bad commands, closing transmission channel.");
                    StopAsync();
                }

                RaiseError(String.Format(@"""{0}""", CommandLine), "Not recognized.");
            }
        }

        //线程安全
        public void WriteLine(String CommandName, String Parameters)
        {
            WriteLine(String.Format(@"/svr {0} {1}", CommandName, Parameters));
        }
        //线程安全
        private void WriteLine(String CommandLine)
        {
            if (Server.EnableLogNormalOut)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Out", Message = CommandLine });
            }
            PushCommand(SessionCommand.CreateWrite(CommandLine));
        }
        //线程安全
        public void RaiseError(String CommandName, String Message)
        {
            Server.RaiseError(Context, CommandName, Message);
        }
        //线程安全
        public void RaiseUnknownError(String CommandName, Exception ex, StackTrace s)
        {
            var Info = ExceptionInfo.GetExceptionInfo(ex, s);
            if (Server.ClientDebug)
            {
                Server.RaiseError(Context, CommandName, Info);
                if (Server.EnableLogUnknownError)
                {
                    Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Unk", Message = Info });
                }
            }
            else
            {
                Server.RaiseError(Context, CommandName, "Internal server error.");
            }
        }

        //线程安全
        private void OnCriticalError(Exception ex, StackTrace s)
        {
            if (Server.EnableLogCriticalError)
            {
                var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Crtcl", Message = Info });
            }
        }

        private void Logon()
        {
        }

        //线程安全
        private void StopAsync()
        {
            var ss = GetSocket();
            if (ss != null)
            {
                ss.Shutdown(SocketShutdown.Receive);
            }
            if (Server.EnableLogSystem)
            {
                Server.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Message = "SessionExit" });
            }
            IsRunningValue.Update
            (
                b =>
                {
                    if (b)
                    {
                        PushCommand(SessionCommand.CreateQuit());
                    }
                    return false;
                }
            );
            Server.NotifySessionQuit(this);
        }
        protected override void StopInner()
        {
            if (Server != null)
            {
                JsonSession v = null;
                Server.SessionMappings.TryRemove(Context, out v);
            }

            IsRunningValue.Update
            (
                b =>
                {
                    if (b)
                    {
                        PushCommand(SessionCommand.CreateQuit());
                    }
                    return false;
                }
            );

            while (NumSessionCommand.Check(n => n != 0))
            {
                NumSessionCommandUpdated.WaitOne();
            }
            SessionTask.Update
            (
                t =>
                {
                    if (t != null)
                    {
                        t.Wait();
                        t.Dispose();
                        Buffer = null;
                    }
                    return null;
                }
            );
        }
    }
}
