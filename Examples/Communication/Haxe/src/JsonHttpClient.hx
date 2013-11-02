import haxe.Json;
import jQuery.JQuery;

import Common;
import communication.Communication;
import communication.CommunicationJson;

private class JsonSender implements IJsonSender
{
    public var sendObject : Dynamic -> Void;

    public function new()
    {
    }

    public function send(commandName : String, commandHash : String, parameters : String) : Void
    {
        var jo = {commandName : commandName, commandHash : commandHash, parameters : parameters};
        sendObject(jo);
    }
}

class JsonHttpClient
{
    public var InnerClient(get, null) : IApplicationClient;
    public function get_InnerClient() : IApplicationClient
    {
        return jc.GetApplicationClient();
    }

    private var prefix : String;
    private var serviceVirtualPath : String;
    private var useJsonp : Boolean;
    private var useShortConnection : Boolean;
    private var jc : JsonSerializationClient;
    private var sessionId : String;

    public function new(prefix : String, serviceVirtualPath : String, useJsonp : Boolean, useShortConnection : Boolean)
    {
        if (!StringTools.endsWith(prefix, "/")) { throw "InvalidOperationException: PrefixNotEndWithSlash: '" + prefix + "'"; }
        this.prefix = prefix;
        this.serviceVirtualPath = serviceVirtualPath;
        this.useJsonp = useJsonp;
        this.useShortConnection = useShortConnection;
        var js = new JsonSender();
        jc = new JsonSerializationClient(js);
        var ac = jc.GetApplicationClient();
        ac.errorCommand = function(e)
        {
            try
            {
                ac.dequeueCallback(e.commandName);
            }
            catch (unknown : Dynamic)
            {
            }
        };
        sessionId = null;
        js.sendObject = function(jo : Dynamic)
        {
            sendRaw(jo, jc.handleResult);
        };
    }
    
    private function sendRaw(jo : Dynamic, _callback : String -> String -> String -> Void)
    {
        if (useJsonp)
        {
            var url;
            if ((sessionId == null) || useShortConnection)
            {
                url = prefix + serviceVirtualPath + "?callback=?";
            }
            else
            {
                url = prefix + serviceVirtualPath + "?sessionid=" + StringTools.urlEncode(sessionId) + "&callback=?";
            }
            JQueryStatic.getJSON(url, {data : Json.stringify([jo])}, function(r)
            {
                var commands : Array<Dynamic> = r.commands;
                sessionId = r.sessionid;
                for (c in commands)
                {
                    _callback(c.commandName, c.commandHash, c.parameters);
                }
            });
        }
        else
        {
            var url;
            if ((sessionId == null) || useShortConnection)
            {
                url = prefix + serviceVirtualPath;
            }
            else
            {
                url = prefix + serviceVirtualPath + "?sessionid=" + StringTools.urlEncode(sessionId);
            }
            JQueryStatic.post(url, Json.stringify([jo]), function(r)
            {
                var commands : Array<Dynamic> = r.commands;
                sessionId = r.sessionid;
                for (c in commands)
                {
                    _callback(c.commandName, c.commandHash, c.parameters);
                }
            });
        }
    }

    public function send(commandName : String, parameters : String, _callback : String -> String -> Void) : Void
    {
        var c = function(commandName : String, commandHash : String, parameters : String)
        {
            _callback(commandName, parameters);
        };
        sendRaw({commandName : commandName, parameters : parameters}, c);
    }
}
