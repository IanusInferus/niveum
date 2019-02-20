using System;
using System.Collections.Generic;
using System.Linq;
using Bridge;
using Bridge.Html5;
using Bridge.jQuery2;
using Client;
using Communication;

public class App
{
    [External]
    [Template("$({0})")]
    public static jQuery Q(String Selector) { throw new InvalidOperationException(); }
    [External]
    [Template("$({0})")]
    public static jQuery Q(Object obj) { throw new InvalidOperationException(); }
    [External]
    [Template("$({0}, {1})")]
    public static jQuery Q(Object obj, Object context) { throw new InvalidOperationException(); }
    [External]
    public interface ITemplate
    {
        String render(dynamic Data);
    }
    [External]
    [Template("$.templates({0})")]
    public static ITemplate T(String Selector) { throw new InvalidOperationException(); }

    [Ready]
    public static void Main()
    {
        var a = new[] { 1, 2, 3, 4 };
        Func<int, int, int> f = (x, y) => x * y;
        var h = f(2, 3);
        if (h != 6) { Global.Alert("Error."); }
        var b = a.Select(z => z * z).ToArray();
        var d = Convert.ToBase64String(Enumerable.Range(0, 255).Select(i => (Byte)(i)).ToArray());

        Q("#button_alert").Click(e =>
        {
            Global.Alert("Test Alert Message");
        });

        Q("#button_template").Click(e =>
        {
            var users = new[] { new { id = "zhang3", name = "ZHANG San" }, new { id = "li4", name = "LI Si" } };
            var template = T("#tmpl_template");
            var text = template.render(new { users = users });
            Q("#tbody_template").Append(text);
        });

        Q("#button_ajax").Click(e =>
        {
            jQuery.GetJSON("users.json", null, (data, textStatus, jqXHR) =>
            {
                var users = data;
                var template = T("#tmpl_template");
                var text = template.render(new { users = users });
                Q("#tbody_ajax").Append(text);
            });
        });

        var jsca = new JsonSerializationClientAdapter();
        var jhc = new JsonHttpClient(jsca, "/api/", "q", true, false);
        var jc = jsca.GetApplicationClient();
        Q("#button_servertime").Click(async e =>
        {
            var r = await jc.ServerTime(new ServerTimeRequest { });
            if (r.OnSuccess)
            {
                var Time = r.Success;
                var template = T("#tmpl_paragraph");
                Q("#div_servertime").Append(template.render(new { content = Time }));
            }
        });
    }
}
