using Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var hc = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var cc = new ClientContext(hc, builder.HostEnvironment.BaseAddress);
cc.SystemName = "Client";
builder.Services.AddScoped(sp => hc);
builder.Services.AddScoped(sp => cc);
builder.Services.AddScoped(sp => cc.ApplicationClient);

await builder.Build().RunAsync();
