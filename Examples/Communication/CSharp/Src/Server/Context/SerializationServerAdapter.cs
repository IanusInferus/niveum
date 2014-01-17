using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Communication;
using Communication.Binary;
using Communication.Json;

namespace Server
{
    public class BinarySerializationServerAdapter : IBinarySerializationServerAdapter
    {
        private IApplicationServer s;
        private static ThreadLocal<BinarySerializationServer> sss = new ThreadLocal<BinarySerializationServer>(() => new BinarySerializationServer());
        private BinarySerializationServer ss = sss.Value;
        private BinarySerializationServerEventDispatcher ssed;

        public BinarySerializationServerAdapter(IApplicationServer ApplicationServer)
        {
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
                    ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters, OutParameters => OnSuccess(OutParameters), OnFailure);
                }
                else
                {
                    try
                    {
                        ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters, OutParameters => OnSuccess(OutParameters), OnFailure);
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
        private IApplicationServer s;
        private static ThreadLocal<JsonSerializationServer> sss = new ThreadLocal<JsonSerializationServer>(() => new JsonSerializationServer());
        private JsonSerializationServer ss = sss.Value;
        private JsonSerializationServerEventDispatcher ssed;

        public JsonSerializationServerAdapter(IApplicationServer ApplicationServer)
        {
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
        public Boolean HasCommand(String CommandName) { return ss.HasCommand(CommandName); }
        public Boolean HasCommand(String CommandName, UInt32 CommandHash) { return ss.HasCommand(CommandName, CommandHash); }
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
                    ss.ExecuteCommandAsync(s, CommandName, Parameters, OutParameters => OnSuccess(OutParameters), OnFailure);
                }
                else
                {
                    try
                    {
                        ss.ExecuteCommandAsync(s, CommandName, Parameters, OutParameters => OnSuccess(OutParameters), OnFailure);
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
                    ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters, OutParameters => OnSuccess(OutParameters), OnFailure);
                }
                else
                {
                    try
                    {
                        ss.ExecuteCommandAsync(s, CommandName, CommandHash, Parameters, OutParameters => OnSuccess(OutParameters), OnFailure);
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
