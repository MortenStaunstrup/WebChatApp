using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ChatAppWeb;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorageAsSingleton();
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["BaseApiPath"];
    if (string.IsNullOrEmpty(baseUrl))
        throw new ApplicationException("BaseUrl is not set");
    return new HttpClient { BaseAddress = new Uri(baseUrl) };
    
});

await builder.Build().RunAsync();