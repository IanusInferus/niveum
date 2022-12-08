using System;
using System.Threading;
using Client;
using Communication;

public class ClientContext
{
    private JsonSerializationClientAdapter jsca;
    private JsonHttpClient jhc;
    private IApplicationClient ac;
    private Timer timer;

    public ClientContext(HttpClient HttpClient, String BaseAddress)
    {
        var u = new Uri(BaseAddress);
        var ApiAddress = $"http://{u.Host}:8003/api/";

        jsca = new JsonSerializationClientAdapter();
        jhc = new JsonHttpClient(jsca, ApiAddress, "q", false, HttpClient);
        ac = jsca.GetApplicationClient();

        timer = new Timer(async (object? stateInfo) =>
        {
            await ac.ServerTime(new ServerTimeRequest { });
        }, new AutoResetEvent(false), 20000, 20000);
    }

    public String SystemName { get; set; } = "Client";
    public JsonHttpClient JsonHttpClient { get { return jhc; } }
    public IApplicationClient ApplicationClient { get { return ac; } }
}
