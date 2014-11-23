using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Firefly;
using Algorithms;
using BaseSystem;

namespace Server
{
    public partial class Streamed<TServerContext>
    {
        /// <summary>
        /// 本类的所有公共成员均是线程安全的。
        /// </summary>
        public class UdpSession
        {
            public UdpServer Server { get; private set; }
            private Socket ServerSocket;
            private LockedVariable<IPEndPoint> RemoteEndPointValue = new LockedVariable<IPEndPoint>(default(IPEndPoint));
            public IPEndPoint RemoteEndPoint
            {
                get
                {
                    return RemoteEndPointValue.Check(v => v);
                }
                set
                {
                    RemoteEndPointValue.Update(v => value);
                }
            }
            private LockedVariable<DateTime> LastActiveTimeValue;
            public DateTime LastActiveTime { get { return LastActiveTimeValue.Check(v => v); } }
            private LockedVariable<SecureContext> NextSecureContextValue = new LockedVariable<SecureContext>(null);
            public SecureContext NextSecureContext
            {
                get
                {
                    return NextSecureContextValue.Check(v => v);
                }
                set
                {
                    NextSecureContextValue.Update(v => value);
                }
            }
            private LockedVariable<SecureContext> SecureContextValue = new LockedVariable<SecureContext>(null);
            public SecureContext SecureContext
            {
                get
                {
                    return SecureContextValue.Check(v => v);
                }
                set
                {
                    SecureContextValue.Update(v => value);
                }
            }

            private ISessionContext Context;
            private IServerImplementation si;
            private IStreamedVirtualTransportServer vts;
            private int NumBadCommands = 0;
            private Boolean IsDisposed = false;

            public const int MaxPacketLength = 1400;
            public const int ReadingWindowSize = 32;
            public const int WritingWindowSize = 1024;
            public const int IndexSpace = 65536;
            public static readonly int[] TimeoutSequences = { 400, 800, 1600, 2000, 3000, 4000 };
            private static int GetTimeoutMilliseconds(int ResentCount)
            {
                if (ResentCount < TimeoutSequences.Length) { return TimeoutSequences[ResentCount]; }
                return TimeoutSequences[TimeoutSequences.Length - 1];
            }

            private class Part
            {
                public int Index;
                public Byte[] Data;
                public DateTime ResendTime;
                public int ResentCount;
            }
            private class PartContext
            {
                private int WindowSize;
                public PartContext(int WindowSize)
                {
                    this.WindowSize = WindowSize;
                }

                public int MaxHandled = IndexSpace - 1;
                public SortedDictionary<int, Part> Parts = new SortedDictionary<int, Part>();
                public Part TryTakeFirstPart()
                {
                    if (Parts.Count == 0) { return null; }
                    var First = Parts.First();
                    if (IsSuccessor(First.Key, MaxHandled))
                    {
                        Parts.Remove(First.Key);
                        MaxHandled = First.Key;
                        return First.Value;
                    }
                    return null;
                }
                public Boolean IsEqualOrAfter(int New, int Original)
                {
                    return ((New - Original + IndexSpace) % IndexSpace) < WindowSize;
                }
                public static Boolean IsSuccessor(int New, int Original)
                {
                    return ((New - Original + IndexSpace) % IndexSpace) == 1;
                }
                public static int GetSuccessor(int Original)
                {
                    return (Original + 1) % IndexSpace;
                }
                public Boolean HasPart(int Index)
                {
                    if (IsEqualOrAfter(MaxHandled, Index))
                    {
                        return true;
                    }
                    if (Parts.ContainsKey(Index))
                    {
                        return true;
                    }
                    return false;
                }
                public Boolean TryPushPart(int Index, Byte[] Data, int Offset, int Length)
                {
                    if (((Index - MaxHandled + IndexSpace) % IndexSpace) > WindowSize)
                    {
                        return false;
                    }
                    var b = new Byte[Length];
                    Array.Copy(Data, Offset, b, 0, Length);
                    Parts.Add(Index, new Part { Index = Index, Data = b, ResendTime = DateTime.UtcNow.AddIntMilliseconds(GetTimeoutMilliseconds(0)), ResentCount = 0 });
                    return true;
                }
                public Boolean TryPushPart(int Index, Byte[] Data)
                {
                    if (((Index - MaxHandled + IndexSpace) % IndexSpace) > WindowSize)
                    {
                        return false;
                    }
                    Parts.Add(Index, new Part { Index = Index, Data = Data, ResendTime = DateTime.UtcNow.AddIntMilliseconds(GetTimeoutMilliseconds(0)), ResentCount = 0 });
                    return true;
                }

                public void Acknowledge(int Index, IEnumerable<int> Indices, int WritenIndex)
                {
                    MaxHandled = Index;
                    while (true)
                    {
                        if (Parts.Count == 0) { return; }
                        var First = Parts.First();
                        if (First.Key <= Index)
                        {
                            Parts.Remove(First.Key);
                        }
                        if (First.Key >= Index)
                        {
                            break;
                        }
                    }
                    foreach (var i in Indices)
                    {
                        if (Parts.ContainsKey(i))
                        {
                            Parts.Remove(i);
                        }
                    }
                    while ((MaxHandled != WritenIndex) && IsEqualOrAfter(WritenIndex, MaxHandled))
                    {
                        var Next = GetSuccessor(MaxHandled);
                        if (!Parts.ContainsKey(Next))
                        {
                            MaxHandled = Next;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                public void ForEachTimedoutPacket(int SessionId, DateTime Time, Action<int, Byte[]> f)
                {
                    foreach (var p in Parts)
                    {
                        if (p.Value.ResendTime <= Time)
                        {
                            f(p.Key, p.Value.Data);
                            p.Value.ResendTime = Time.AddIntMilliseconds(GetTimeoutMilliseconds(p.Value.ResentCount));
                            p.Value.ResentCount += 1;
                            //Debug.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + " Resend SessionId: " + SessionId.ToString("X8") + " Index: " + p.Key.ToString() + " Count: " + p.Value.ResentCount.ToString());
                        }
                    }
                }
            }
            private class UdpReadContext
            {
                public PartContext Parts;
                public SortedSet<int> NotAcknowledgedIndices = new SortedSet<int>();
                public Action<StreamedVirtualTransportServerHandleResult[]> OnSuccess;
                public Action OnFailure;
            }
            private class UdpWriteContext
            {
                public PartContext Parts;
                public int WritenIndex;
            }
            private LockedVariable<UdpReadContext> RawReadingContext = new LockedVariable<UdpReadContext>(new UdpReadContext { Parts = new PartContext(ReadingWindowSize), OnSuccess = null, OnFailure = null });
            private LockedVariable<UdpWriteContext> CookedWritingContext = new LockedVariable<UdpWriteContext>(new UdpWriteContext { Parts = new PartContext(WritingWindowSize), WritenIndex = IndexSpace - 1 });

            private SessionStateMachine<StreamedVirtualTransportServerHandleResult, Unit> ssm;

            public UdpSession(UdpServer Server, Socket ServerSocket, IPEndPoint RemoteEndPoint, Func<ISessionContext, IBinaryTransformer, KeyValuePair<IServerImplementation, IStreamedVirtualTransportServer>> VirtualTransportServerFactory, Action<Action> QueueUserWorkItem)
            {
                this.Server = Server;
                this.ServerSocket = ServerSocket;
                this.RemoteEndPoint = RemoteEndPoint;
                this.LastActiveTimeValue = new LockedVariable<DateTime>(DateTime.UtcNow);
                ssm = new SessionStateMachine<StreamedVirtualTransportServerHandleResult, Unit>(ex => ex is SocketException, OnCriticalError, OnShutdownRead, OnShutdownWrite, OnWrite, OnExecute, OnStartRawRead, OnExit, QueueUserWorkItem);

                Context = Server.ServerContext.CreateSessionContext();
                Context.Quit += ssm.NotifyExit;
                Context.Authenticated += () => Server.NotifySessionAuthenticated(this);

                var rpst = new Rc4PacketServerTransformer();
                var Pair = VirtualTransportServerFactory(Context, rpst);
                si = Pair.Key;
                vts = Pair.Value;
                Context.SecureConnectionRequired += c =>
                {
                    rpst.SetSecureContext(c);
                    NextSecureContext = c;
                };
                vts.ServerEvent += () => ssm.NotifyWrite(new Unit());
                vts.InputByteLengthReport += (CommandName, ByteLength) => Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "InBytes", Name = CommandName, Message = ByteLength.ToInvariantString() });
                vts.OutputByteLengthReport += (CommandName, ByteLength) => Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "OutBytes", Name = CommandName, Message = ByteLength.ToInvariantString() });
            }

            public int SessionId
            {
                get
                {
                    var b = Context.SessionToken;
                    var v = b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24); ;
                    return v;
                }
            }

            private void OnShutdownRead()
            {
                Action OnFailure = null;
                RawReadingContext.DoAction(c =>
                {
                    if ((c.OnSuccess != null) && (c.OnFailure != null))
                    {
                        OnFailure = c.OnFailure;
                        c.OnSuccess = null;
                        c.OnFailure = null;
                    }
                });
                if (OnFailure != null)
                {
                    OnFailure();
                }
            }
            private void OnShutdownWrite()
            {
            }
            private void OnWrite(Unit w, Action OnSuccess, Action OnFailure)
            {
                var ByteArrays = vts.TakeWriteBuffer();
                var TotalLength = ByteArrays.Sum(b => b.Length);
                var WriteBuffer = new Byte[GetMinNotLessPowerOfTwo(TotalLength)];
                var Offset = 0;
                foreach (var b in ByteArrays)
                {
                    Array.Copy(b, 0, WriteBuffer, Offset, b.Length);
                    Offset += b.Length;
                }
                var RemoteEndPoint = this.RemoteEndPoint;
                var SessionId = this.SessionId;
                var SecureContext = this.SecureContext;
                var Indices = new List<int>();
                RawReadingContext.DoAction(c =>
                {
                    if (c.NotAcknowledgedIndices.Count == 0) { return; }
                    var MaxHandled = c.Parts.MaxHandled;
                    while (c.NotAcknowledgedIndices.Count > 0)
                    {
                        var First = c.NotAcknowledgedIndices.First();
                        if (c.Parts.IsEqualOrAfter(MaxHandled, First))
                        {
                            c.NotAcknowledgedIndices.Remove(First);
                        }
                        else if (PartContext.IsSuccessor(First, MaxHandled))
                        {
                            c.NotAcknowledgedIndices.Remove(First);
                            MaxHandled = First;
                        }
                        else
                        {
                            break;
                        }
                    }
                    Indices.Add(MaxHandled);
                    Indices.AddRange(c.NotAcknowledgedIndices);
                    c.NotAcknowledgedIndices.Clear();
                });
                if ((ByteArrays.Length == 0) && (Indices.Count == 0))
                {
                    OnSuccess();
                    return;
                }
                var Success = true;
                var Parts = new List<Byte[]>();
                CookedWritingContext.DoAction(c =>
                {
                    var Time = DateTime.UtcNow;
                    var WritingOffset = 0;
                    while ((Indices.Count > 0) || (WritingOffset < TotalLength))
                    {
                        var Index = PartContext.GetSuccessor(c.WritenIndex);

                        var NumIndex = Indices.Count;
                        if (NumIndex > 0xFFFF)
                        {
                            Success = false;
                            return;
                        }

                        var IsACK = NumIndex > 0;

                        var Length = Math.Min(12 + (IsACK ? 2 + NumIndex * 2 : 0) + TotalLength - WritingOffset, MaxPacketLength);
                        var DataLength = Length - (12 + (IsACK ? 2 + NumIndex * 2 : 0));
                        var Buffer = new Byte[Length];
                        Buffer[0] = (Byte)(SessionId & 0xFF);
                        Buffer[1] = (Byte)((SessionId >> 8) & 0xFF);
                        Buffer[2] = (Byte)((SessionId >> 16) & 0xFF);
                        Buffer[3] = (Byte)((SessionId >> 24) & 0xFF);

                        var Flag = 0;
                        if (IsACK)
                        {
                            Flag |= 1; //ACK
                            Buffer[12] = (Byte)(NumIndex & 0xFF);
                            Buffer[13] = (Byte)((NumIndex >> 8) & 0xFF);
                            var j = 0;
                            foreach (var i in Indices)
                            {
                                Buffer[14 + j * 2] = (Byte)(i & 0xFF);
                                Buffer[14 + j * 2 + 1] = (Byte)((i >> 8) & 0xFF);
                                j += 1;
                            }
                            Indices.Clear();
                        }

                        Array.Copy(WriteBuffer, WritingOffset, Buffer, 12 + (IsACK ? 2 + NumIndex * 2 : 0), DataLength);
                        WritingOffset += DataLength;

                        var IsEncrypted = (SecureContext != null);
                        if (IsEncrypted)
                        {
                            Flag |= 2; //ENC
                        }
                        Buffer[4] = (Byte)(Flag & 0xFF);
                        Buffer[5] = (Byte)((Flag >> 8) & 0xFF);
                        Buffer[6] = (Byte)(Index & 0xFF);
                        Buffer[7] = (Byte)((Index >> 8) & 0xFF);

                        var Verification = 0;
                        if (SecureContext != null)
                        {
                            var Key = SecureContext.ClientToken.Concat(Cryptography.SHA1(Buffer.Skip(4).Take(4)));
                            var HMACBytes = Cryptography.HMACSHA1Simple(Key, Buffer).Take(4).ToArray();
                            Verification = HMACBytes[0] | ((Int32)(HMACBytes[1]) << 8) | ((Int32)(HMACBytes[2]) << 16) | ((Int32)(HMACBytes[3]) << 24);
                        }
                        else
                        {
                            Verification = Cryptography.CRC32(Buffer);
                        }

                        Buffer[8] = (Byte)(Verification & 0xFF);
                        Buffer[9] = (Byte)((Verification >> 8) & 0xFF);
                        Buffer[10] = (Byte)((Verification >> 16) & 0xFF);
                        Buffer[11] = (Byte)((Verification >> 24) & 0xFF);

                        var Part = new Part { Index = Index, ResendTime = Time.AddIntMilliseconds(GetTimeoutMilliseconds(0)), Data = Buffer, ResentCount = 0 };
                        if (!c.Parts.TryPushPart(Index, Buffer))
                        {
                            Success = false;
                            return;
                        }
                        Parts.Add(Part.Data);
                        //Debug.WriteLine(Times.DateTimeUtcWithMillisecondsToString(DateTime.UtcNow) + " Send SessionId: " + SessionId.ToString("X8") + " Index: " + Index.ToString());

                        c.WritenIndex = Index;
                    }
                });
                foreach (var p in Parts)
                {
                    try
                    {
                        SendPacket(RemoteEndPoint, p);
                    }
                    catch
                    {
                        Success = false;
                        break;
                    }
                }
                if (!Success)
                {
                    OnFailure();
                }
                else
                {
                    OnSuccess();
                }
            }
            private void OnExecute(StreamedVirtualTransportServerHandleResult r, Action OnSuccess, Action OnFailure)
            {
                if (r.OnCommand)
                {
                    var CommandName = r.Command.CommandName;

                    Action a = () =>
                    {
                        var CurrentTime = DateTime.UtcNow;
                        Context.RequestTime = CurrentTime;
                        if (Server.ServerContext.EnableLogPerformance)
                        {
                            var sw = new Stopwatch();
                            sw.Start();
                            Action OnSuccessInner = () =>
                            {
                                sw.Stop();
                                Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Time", Name = CommandName, Message = String.Format("{0}ms", sw.ElapsedMilliseconds) });
                                ssm.NotifyWrite(new Unit());
                                OnSuccess();
                            };
                            Action<Exception> OnFailureInner = ex =>
                            {
                                RaiseUnknownError(CommandName, ex, new StackTrace(true));
                                OnSuccess();
                            };
                            r.Command.ExecuteCommand(OnSuccessInner, OnFailureInner);
                        }
                        else
                        {
                            Action OnSuccessInner = () =>
                            {
                                ssm.NotifyWrite(new Unit());
                                OnSuccess();
                            };
                            Action<Exception> OnFailureInner = ex =>
                            {
                                RaiseUnknownError(CommandName, ex, new StackTrace(true));
                                OnSuccess();
                            };
                            r.Command.ExecuteCommand(OnSuccessInner, OnFailureInner);
                        }
                    };

                    ssm.AddToActionQueue(a);
                }
                else if (r.OnBadCommand)
                {
                    var CommandName = r.BadCommand.CommandName;

                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError(CommandName, "Too many bad commands, closing transmission channel.");
                        OnFailure();
                    }
                    else
                    {
                        RaiseError(CommandName, "Not recognized.");
                        OnSuccess();
                    }
                }
                else if (r.OnBadCommandLine)
                {
                    var CommandLine = r.BadCommandLine.CommandLine;

                    NumBadCommands += 1;

                    // Maximum allowed bad commands exceeded.
                    if (Server.MaxBadCommands != 0 && NumBadCommands > Server.MaxBadCommands)
                    {
                        RaiseError("", String.Format(@"""{0}"": Too many bad commands, closing transmission channel.", CommandLine));
                        OnFailure();
                    }
                    else
                    {
                        RaiseError("", String.Format(@"""{0}"":  recognized.", CommandLine));
                        OnSuccess();
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            private void OnStartRawRead(Action<StreamedVirtualTransportServerHandleResult[]> OnSuccess, Action OnFailure)
            {
                var Pushed = true;
                var Parts = new List<Part>();
                RawReadingContext.DoAction(c =>
                {
                    if ((c.OnSuccess == null) && (c.OnFailure == null))
                    {
                        while (true)
                        {
                            var p = c.Parts.TryTakeFirstPart();
                            if (p == null) { break; }
                            Parts.Add(p);
                        }
                        if (Parts.Count == 0)
                        {
                            c.OnSuccess = OnSuccess;
                            c.OnFailure = OnFailure;
                        }
                        Pushed = true;
                    }
                    else
                    {
                        Pushed = false;
                    }
                });

                if (Parts.Count > 0)
                {
                    HandleRawRead(Parts, OnSuccess, OnFailure);
                }
                if (!Pushed)
                {
                    OnFailure();
                }
            }

            private void HandleRawRead(IEnumerable<Part> Parts, Action<StreamedVirtualTransportServerHandleResult[]> OnSuccess, Action OnFailure)
            {
                if (ssm.IsExited()) { return; }
                var Results = new List<StreamedVirtualTransportServerHandleResult>();
                foreach (var p in Parts)
                {
                    var Buffer = vts.GetReadBuffer();
                    var BufferLength = Buffer.Offset + Buffer.Count;
                    if (p.Data.Length > Buffer.Array.Length - BufferLength)
                    {
                        OnFailure();
                        return;
                    }
                    Array.Copy(p.Data, 0, Buffer.Array, BufferLength, p.Data.Length);

                    var c = p.Data.Length;
                    while (true)
                    {
                        StreamedVirtualTransportServerHandleResult Result;
                        try
                        {
                            Result = vts.Handle(c);
                        }
                        catch (Exception ex)
                        {
                            if ((ex is InvalidOperationException) && (ex.Message != ""))
                            {
                                Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Known", Name = "Exception", Message = ex.Message });
                            }
                            else if (!IsSocketErrorKnown(ex))
                            {
                                OnCriticalError(ex, new StackTrace(true));
                            }
                            OnFailure();
                            return;
                        }
                        c = 0;
                        if (Result.OnContinue)
                        {
                            break;
                        }
                        Results.Add(Result);
                    }
                }
                if (Results.Count == 0)
                {
                    OnStartRawRead(OnSuccess, OnFailure);
                    return;
                }
                OnSuccess(Results.ToArray());
            }

            public void Dispose()
            {
                if (IsDisposed) { return; }
                IsDisposed = true;

                IsExitingValue.Update(b => true);
                ssm.NotifyExit();

                Server.SessionMappings.DoAction(Mappings =>
                {
                    if (Mappings.ContainsKey(Context))
                    {
                        Mappings.Remove(Context);
                    }
                });
                Server.ServerContext.TryUnregisterSession(Context);

                si.Dispose();

                IsRunningValue.Update(b => false);

                SpinWait.SpinUntil(() => ssm.IsExited());

                Context.Dispose();

                IsExitingValue.Update(b => false);

                if (Server.ServerContext.EnableLogSystem)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Name = "SessionExit", Message = "" });
                }
            }

            private void OnExit()
            {
                IsExitingValue.Update(b =>
                {
                    if (!IsRunningValue.Check(bb => bb)) { return b; }
                    if (!b)
                    {
                        Server.NotifySessionQuit(this);
                    }
                    return true;
                });
            }

            public void Start()
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

                    Server.ServerContext.RegisterSession(Context);
                    Server.SessionMappings.DoAction(Mappings => Mappings.Add(Context, this));

                    if (Server.ServerContext.EnableLogSystem)
                    {
                        Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Sys", Name = "SessionEnter", Message = "" });
                    }
                    ssm.Start();
                }
                catch (Exception ex)
                {
                    OnCriticalError(ex, new StackTrace(true));
                    ssm.NotifyFailure();
                }
            }

            private void SendPacket(IPEndPoint RemoteEndPoint, Byte[] Data)
            {
                ServerSocket.SendTo(Data, RemoteEndPoint);
            }

            public Boolean PushAux(IPEndPoint RemoteEndPoint, int[] Indices)
            {
                var SessionId = this.SessionId;
                var Time = DateTime.UtcNow;
                if ((Indices != null) && (Indices.Length > 0))
                {
                    var l = new List<Byte[]>();
                    CookedWritingContext.DoAction(c =>
                    {
                        c.Parts.Acknowledge(Indices.First(), Indices.Skip(1), c.WritenIndex);
                        c.Parts.ForEachTimedoutPacket(SessionId, Time, (i, d) => l.Add(d));
                    });
                    foreach (var p in l)
                    {
                        try
                        {
                            SendPacket(RemoteEndPoint, p);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            public bool IsPushed(int Index)
            {
                var Pushed = false;
                RawReadingContext.DoAction(c =>
                {
                    if (c.Parts.HasPart(Index))
                    {
                        Pushed = true;
                        return;
                    }
                });
                return Pushed;
            }
            public void PrePush(Action a)
            {
                ssm.AddToActionQueue(a);
            }
            public Boolean Push(IPEndPoint RemoteEndPoint, int Index, int[] Indices, Byte[] Buffer, int Offset, int Length)
            {
                var SessionId = this.SessionId;
                var Time = DateTime.UtcNow;
                if ((Indices != null) && (Indices.Length > 0))
                {
                    var l = new List<Byte[]>();
                    CookedWritingContext.DoAction(c =>
                    {
                        c.Parts.Acknowledge(Indices.First(), Indices.Skip(1), c.WritenIndex);
                        c.Parts.ForEachTimedoutPacket(SessionId, Time, (i, d) => l.Add(d));
                    });
                    foreach (var p in l)
                    {
                        try
                        {
                            SendPacket(RemoteEndPoint, p);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }

                var Pushed = false;
                var Parts = new List<Part>();
                Action<StreamedVirtualTransportServerHandleResult[]> OnSuccess = null;
                Action OnFailure = null;
                RawReadingContext.DoAction(c =>
                {
                    if (c.Parts.HasPart(Index))
                    {
                        Pushed = true;
                        return;
                    }
                    Pushed = c.Parts.TryPushPart(Index, Buffer, Offset, Length);
                    if (Pushed)
                    {
                        c.NotAcknowledgedIndices.Add(Index);
                        while (c.NotAcknowledgedIndices.Count > 0)
                        {
                            var First = c.NotAcknowledgedIndices.First();
                            if (c.Parts.IsEqualOrAfter(c.Parts.MaxHandled, First))
                            {
                                c.NotAcknowledgedIndices.Remove(First);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if ((c.OnSuccess != null) && (c.OnFailure != null))
                        {
                            while (true)
                            {
                                var p = c.Parts.TryTakeFirstPart();
                                if (p == null) { break; }
                                Parts.Add(p);
                            }

                            if (Parts.Count > 0)
                            {
                                OnSuccess = c.OnSuccess;
                                OnFailure = c.OnFailure;
                                c.OnSuccess = null;
                                c.OnFailure = null;
                            }
                        }
                    }
                });

                if (Pushed)
                {
                    LastActiveTimeValue.Update(v => DateTime.UtcNow);
                }
                if (Parts.Count > 0)
                {
                    HandleRawRead(Parts, OnSuccess, OnFailure);
                }
                return Pushed;
            }

            private LockedVariable<Boolean> IsRunningValue = new LockedVariable<Boolean>(false);
            private LockedVariable<Boolean> IsExitingValue = new LockedVariable<Boolean>(false);
            public Boolean IsRunning
            {
                get
                {
                    return IsRunningValue.Check(b => b);
                }
            }

            private static Boolean IsSocketErrorKnown(Exception ex)
            {
                var sex = ex as SocketException;
                if (sex == null) { return false; }
                var se = sex.SocketErrorCode;
                if (se == SocketError.ConnectionAborted) { return true; }
                if (se == SocketError.ConnectionReset) { return true; }
                if (se == SocketError.Shutdown) { return true; }
                if (se == SocketError.OperationAborted) { return true; }
                if (se == SocketError.Interrupted) { return true; }
                return false;
            }

            private static int GetMinNotLessPowerOfTwo(int v)
            {
                //计算不小于TotalLength的最小2的幂
                if (v < 1) { return 1; }
                var n = 0;
                var z = v - 1;
                while (z != 0)
                {
                    z >>= 1;
                    n += 1;
                }
                var Value = 1 << n;
                if (Value == 0) { throw new InvalidOperationException(); }
                return Value;
            }

            //线程安全
            public void RaiseError(String CommandName, String Message)
            {
                si.RaiseError(CommandName, Message);
            }
            //线程安全
            public void RaiseUnknownError(String CommandName, Exception ex, StackTrace s)
            {
                var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                if (Server.ServerContext.ClientDebug)
                {
                    si.RaiseError(CommandName, Info);
                }
                else
                {
                    si.RaiseError(CommandName, "Internal server error.");
                }
                if (Server.ServerContext.EnableLogUnknownError)
                {
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Unk", Name = "Exception", Message = Info });
                }
            }

            //线程安全
            private void OnCriticalError(Exception ex, StackTrace s)
            {
                if (Server.ServerContext.EnableLogCriticalError)
                {
                    var Info = ExceptionInfo.GetExceptionInfo(ex, s);
                    Server.ServerContext.RaiseSessionLog(new SessionLogEntry { Token = Context.SessionTokenString, RemoteEndPoint = RemoteEndPoint, Time = DateTime.UtcNow, Type = "Crtcl", Name = "Exception", Message = Info });
                }
            }
        }
    }
}
