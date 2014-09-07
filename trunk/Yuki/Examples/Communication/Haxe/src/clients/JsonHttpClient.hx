package clients;

import haxe.Json;
import jQuery.JQuery;

import Common;

import clients.ISerializationClient;

class JsonHttpClient
{
    private var jc : IJsonSerializationClientAdapter;

    private var prefix : String;
    private var serviceVirtualPath : String;
    private var useJsonp : Boolean;
    private var useShortConnection : Boolean;
    private var sessionId : String;

    public function new(jc : IJsonSerializationClientAdapter, prefix : String, serviceVirtualPath : String, useJsonp : Boolean, useShortConnection : Boolean)
    {
        if (!StringTools.endsWith(prefix, "/")) { throw "InvalidOperationException: PrefixNotEndWithSlash: '" + prefix + "'"; }
        this.jc = jc;
        this.prefix = prefix;
        this.serviceVirtualPath = serviceVirtualPath;
        this.useJsonp = useJsonp;
        this.useShortConnection = useShortConnection;
        sessionId = null;
        jc.clientEvent = function(commandName : String, commandHash : String, parameters : String) : Void
        {
            var jo = {commandName : commandName, commandHash : commandHash, parameters : parameters};
            sendRaw(jo, jc.handleResult);
        };
    }

    private function getTimeAndRandom() : String
    {
        var Time = DateTools.format(Date.now(), "%Y%m%d%H%M%S");
        var Random = StringTools.lpad(Std.string(Std.random(100)), "0", 2);
        return Time + Random;
    }
    private function sendRaw(jo : Dynamic, _callback : String -> String -> String -> Void)
    {
        if (useJsonp)
        {
            var url;
            if ((sessionId == null) || useShortConnection)
            {
                url = prefix + serviceVirtualPath + "?avoidCache=" + getTimeAndRandom() + "&callback=?";
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
                url = prefix + serviceVirtualPath + "?avoidCache=" + getTimeAndRandom();
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
