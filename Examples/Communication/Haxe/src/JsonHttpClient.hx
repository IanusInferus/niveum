import haxe.Json;
import jQuery.JQuery;

import Common;
import Communication;
import CommunicationJson;

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

    private var jc:JsonSerializationClient;
    private var sessionId:String;

    public function new(prefix : String, serviceVirtualPath : String, useJsonp : Bool)
    {
        if (!StringTools.endsWith(prefix, "/")) { throw "InvalidOperationException: PrefixNotEndWithSlash: '" + prefix + "'"; }
        var js = new JsonSender();
        jc = new JsonSerializationClient(js);
        sessionId = null;
        js.sendObject = function(jo : Dynamic)
        {
            if (useJsonp)
            {
                var url;
                if (sessionId == null)
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
                        jc.handleResult(c.commandName, c.commandHash, c.parameters);
                    }
                });
            }
            else
            {
                var url;
                if (sessionId == null)
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
                        jc.handleResult(c.commandName, c.commandHash, c.parameters);
                    }
                });
            }
        };
    }
}
