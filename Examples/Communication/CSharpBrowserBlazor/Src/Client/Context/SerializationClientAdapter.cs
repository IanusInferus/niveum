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
            ac.DequeueCallback(e.CommandName);
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

    public void DequeueCallback(String CommandName)
    {
        jc.GetApplicationClient().DequeueCallback(CommandName);
    }

    public void HandleResult(String CommandName, UInt32 CommandHash, String Parameters)
    {
        jc.HandleResult(CommandName, CommandHash, Parameters);
    }
    public event JsonClientEventDelegate ClientEvent;

    public void Send(String CommandName, UInt32 CommandHash, String Parameters)
    {
        if (ClientEvent != null)
        {
            ClientEvent(CommandName, CommandHash, Parameters);
        }
    }
}
