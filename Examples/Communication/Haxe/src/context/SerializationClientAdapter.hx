package context;

import haxe.Json;
import jQuery.JQuery;

import Common;
import communication.Communication;
import communication.CommunicationJson;

import clients.ISerializationClient;

class JsonSerializationClientAdapter implements IJsonSerializationClientAdapter implements IJsonSender
{
    private var jc : JsonSerializationClient;

    public function new()
    {
        this.jc = new JsonSerializationClient(this);
        var ac = jc.getApplicationClient();
        ac.errorCommand = function(e)
        {
            ac.dequeueCallback(e.commandName);
        };
    }

    public function getApplicationClient() : IApplicationClient
    {
        return jc.getApplicationClient();
    }

    public var hash(get, null) : String;
    public function get_hash() : String
    {
        return jc.getApplicationClient().hash;
    }

    public function dequeueCallback(commandName : String) : Void
    {
        jc.getApplicationClient().dequeueCallback(commandName);
    }

    public function handleResult(commandName : String, commandHash : String, parameters : String) : Void
    {
        jc.handleResult(commandName, commandHash, parameters);
    }
    public var clientEvent : String -> String -> String -> Void;

    public function send(commandName : String, commandHash : String, parameters : String) : Void
    {
        if (clientEvent != null)
        {
            clientEvent(commandName, commandHash, parameters);
        }
    }
}
