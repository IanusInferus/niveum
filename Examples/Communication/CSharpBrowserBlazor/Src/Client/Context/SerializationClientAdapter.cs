#nullable disable

using System;
using Communication;
using Communication.Json;

using Client;

public class JsonSerializationClientAdapter : IJsonSerializationClientAdapter, IJsonSender
{
    private JsonSerializationClient jc;

    public JsonSerializationClientAdapter()
    {
        this.jc = new JsonSerializationClient(this);
        var ac = jc.GetApplicationClient();
        ac.ErrorCommand += e =>
        {
            ac.NotifyErrorCommand(e.CommandName, e.Message);
        };
    }

    public IApplicationClient GetApplicationClient()
    {
        return jc.GetApplicationClient();
    }

    public UInt64 Hash
    {
        get
        {
            return jc.GetApplicationClient().Hash;
        }
    }

    public void NotifyErrorCommand(String CommandName, String Message)
    {
        jc.GetApplicationClient().NotifyErrorCommand(CommandName, Message);
    }

    public void HandleResult(String CommandName, UInt32 CommandHash, String Parameters)
    {
        jc.HandleResult(CommandName, CommandHash, Parameters);
    }
    public event JsonClientEventDelegate ClientEvent;

    public void Send(String CommandName, UInt32 CommandHash, String Parameters, Action<Exception> OnError)
    {
        if (ClientEvent != null)
        {
            ClientEvent(CommandName, CommandHash, Parameters, OnError);
        }
    }
}
