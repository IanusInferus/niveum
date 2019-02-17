using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Communication.Binary;
using Communication.Json;

namespace Server
{
    public class BinarySerializationServerAdapter : IBinarySerializationServerAdapter
    {
        private TaskFactory Factory;
        private IApplicationServer s;
        private static ThreadLocal<BinarySerializationServer> sss = new ThreadLocal<BinarySerializationServer>(() => new BinarySerializationServer());
        private BinarySerializationServer ss = sss.Value;
        private BinarySerializationServerEventDispatcher ssed;

        public BinarySerializationServerAdapter(TaskFactory Factory, IApplicationServer ApplicationServer)
        {
            this.Factory = Factory;
            this.s = ApplicationServer;
            this.ssed = new BinarySerializationServerEventDispatcher(ApplicationServer);
            this.ssed.ServerEvent += (CommandName, CommandHash, Parameters) =>
            {
                if (ServerEvent != null)
                {
                    ServerEvent(CommandName, CommandHash, Parameters);
                }
            };
        }

        public UInt64 Hash { get { return ss.Hash; } }
        public Boolean HasCommand(String CommandName, UInt32 CommandHash) { return ss.HasCommand(CommandName, CommandHash) || ss.HasCommandAsync(CommandName, CommandHash); }
        public void ExecuteCommand(String CommandName, UInt32 CommandHash, Byte[] Parameters, Action<Byte[]> OnSuccess, Action<Exception> OnFailure)
        {
            if (ss.HasCommand(CommandName, CommandHash))
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var OutParameters = ss.ExecuteCommand(s, CommandName, CommandHash, Parameters);
                    OnSuccess(OutParameters);
                }
                else
                {
                    try
                    {
                        var OutParameters = ss.ExecuteCommand(s, CommandName, CommandHash, Parameters);
                        OnSuccess(OutParameters);
                    }
                    catch (Exception ex)
                    {
                        OnFailure(ex);
                    }
                }
            }
            else if (ss.HasCommandAsync(CommandName, CommandHash))
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var t = Factory.StartNew(() => ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters));
                    t.ContinueWith(tt => OnFailure(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                    t.ContinueWith((Task<Task<Byte[]>> tt) => {
                        tt.Result.ContinueWith(ttt => OnFailure(ttt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                        tt.Result.ContinueWith(ttt => OnSuccess(ttt.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
                else
                {
                    try
                    {
                        var t = Factory.StartNew(() => ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters));
                        t.ContinueWith(tt => OnFailure(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                        t.ContinueWith((Task<Task<Byte[]>> tt) => {
                            tt.Result.ContinueWith(ttt => OnFailure(ttt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                            tt.Result.ContinueWith(ttt => OnSuccess(ttt.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    catch (Exception ex)
                    {
                        OnFailure(ex);
                    }
                }
            }
            else
            {
                OnFailure(new InvalidOperationException());
            }
        }
        public event BinaryServerEventDelegate ServerEvent;
    }

    public class JsonSerializationServerAdapter : IJsonSerializationServerAdapter
    {
        private TaskFactory Factory;
        private IApplicationServer s;
        private static ThreadLocal<JsonSerializationServer> sss = new ThreadLocal<JsonSerializationServer>(() => new JsonSerializationServer());
        private JsonSerializationServer ss = sss.Value;
        private JsonSerializationServerEventDispatcher ssed;

        public JsonSerializationServerAdapter(TaskFactory Factory, IApplicationServer ApplicationServer)
        {
            this.Factory = Factory;
            this.s = ApplicationServer;
            this.ssed = new JsonSerializationServerEventDispatcher(ApplicationServer);
            this.ssed.ServerEvent += (CommandName, CommandHash, Parameters) =>
            {
                if (ServerEvent != null)
                {
                    ServerEvent(CommandName, CommandHash, Parameters);
                }
            };
        }

        public UInt64 Hash { get { return ss.Hash; } }
        public Boolean HasCommand(String CommandName) { return ss.HasCommand(CommandName) || ss.HasCommandAsync(CommandName); }
        public Boolean HasCommand(String CommandName, UInt32 CommandHash) { return ss.HasCommand(CommandName, CommandHash) || ss.HasCommandAsync(CommandName); }
        public void ExecuteCommand(String CommandName, String Parameters, Action<String> OnSuccess, Action<Exception> OnFailure)
        {
            if (ss.HasCommand(CommandName))
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var OutParameters = ss.ExecuteCommand(s, CommandName, Parameters);
                    OnSuccess(OutParameters);
                }
                else
                {
                    try
                    {
                        var OutParameters = ss.ExecuteCommand(s, CommandName, Parameters);
                        OnSuccess(OutParameters);
                    }
                    catch (Exception ex)
                    {
                        OnFailure(ex);
                    }
                }
            }
            else if (ss.HasCommandAsync(CommandName))
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var t = Factory.StartNew(() => ss.ExecuteCommandAsync(s, CommandName, Parameters));
                    t.ContinueWith(tt => OnFailure(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                    t.ContinueWith((Task<Task<String>> tt) => {
                        tt.Result.ContinueWith(ttt => OnFailure(ttt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                        tt.Result.ContinueWith(ttt => OnSuccess(ttt.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
                else
                {
                    try
                    {
                        var t = Factory.StartNew(() => ss.ExecuteCommandAsync(s, CommandName, Parameters));
                        t.ContinueWith(tt => OnFailure(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                        t.ContinueWith((Task<Task<String>> tt) => {
                            tt.Result.ContinueWith(ttt => OnFailure(ttt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                            tt.Result.ContinueWith(ttt => OnSuccess(ttt.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    catch (Exception ex)
                    {
                        OnFailure(ex);
                    }
                }
            }
            else
            {
                OnFailure(new InvalidOperationException());
            }
        }
        public void ExecuteCommand(String CommandName, UInt32 CommandHash, String Parameters, Action<String> OnSuccess, Action<Exception> OnFailure)
        {
            if (ss.HasCommand(CommandName, CommandHash))
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var OutParameters = ss.ExecuteCommand(s, CommandName, CommandHash, Parameters);
                    OnSuccess(OutParameters);
                }
                else
                {
                    try
                    {
                        var OutParameters = ss.ExecuteCommand(s, CommandName, CommandHash, Parameters);
                        OnSuccess(OutParameters);
                    }
                    catch (Exception ex)
                    {
                        OnFailure(ex);
                    }
                }
            }
            else if (ss.HasCommandAsync(CommandName, CommandHash))
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var t = Factory.StartNew(() => ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters));
                    t.ContinueWith(tt => OnFailure(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                    t.ContinueWith((Task<Task<String>> tt) => {
                        tt.Result.ContinueWith(ttt => OnFailure(ttt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                        tt.Result.ContinueWith(ttt => OnSuccess(ttt.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
                else
                {
                    try
                    {
                        var t = Factory.StartNew(() => ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters));
                        t.ContinueWith(tt => OnFailure(tt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                        t.ContinueWith((Task<Task<String>> tt) => {
                            tt.Result.ContinueWith(ttt => OnFailure(ttt.Exception), TaskContinuationOptions.OnlyOnFaulted);
                            tt.Result.ContinueWith(ttt => OnSuccess(ttt.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    catch (Exception ex)
                    {
                        OnFailure(ex);
                    }
                }
            }
            else
            {
                OnFailure(new InvalidOperationException());
            }
        }
        public event JsonServerEventDelegate ServerEvent;
    }
}
