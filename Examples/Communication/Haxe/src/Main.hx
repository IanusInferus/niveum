import Functional.*;

import haxe.Template;
import haxe.Json;

import js.Lib;
import js.Lib.alert;

import jQuery.JQuery;

import Common;
import communication.Communication;
import context.SerializationClientAdapter;
import clients.JsonHttpClient;

using Lambda;

class Main
{
    private static function Q(in1 : Dynamic, ?in2 : Dynamic) : JQuery
    {
        return new JQuery(in1, in2);
    }
    private static function T(selector : String) : Template
    {
        return new Template(Q(selector).html());
    }

    static function main()
    {
        var a = [1, 2, 3, 4];
        var f = fun(a, b, a * b);
        var h = f(2, 3);
        if (h != 6) { alert("Error."); };
        var b = a.map(fun(z, z * z)).array();

        var d = BuildDate.getBuildDate();
        trace("BuildDate: " + d.toString() + ", " + b.toString());

        Q(null).ready(function(e)
        {
            Q("#button_alert").click(function(e)
            {
                alert("Test Alert Message");
            });

            Q("#button_template").click(function(e)
            {
                var users = [{Id: "zhang3", Name: "ZHANG San"}, {Id: "li4", Name: "LI Si"}].list();
                var template = T("#tmpl_template");
                var text = template.execute({users : users});
                Q("#tbody_template").append(text);
            });

            Q("#button_ajax").click(function(e)
            {
                JQueryStatic.getJSON("users.json", function(r)
                {
                    var users = r;
                    var template = T("#tmpl_template");
                    var text = template.execute({users : users});
                    Q("#tbody_ajax").append(text);
                });
            });

            var jsca = new JsonSerializationClientAdapter();
            var jhc = new JsonHttpClient(jsca, "/api/", "q", true, false);
            var jc = jsca.getApplicationClient();
            Q("#button_servertime").click(function(e)
            {
                jc.serverTime({}, function(r)
                {
                    switch (r)
                    {
                        case success(time):
                            var template = T("#tmpl_paragraph");
                            Q("#div_servertime").append(template.execute({Content : time}));
                    }
                });
            });
        });
    }
}
