using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ChatAppWeb;
using DotNetEnv;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

string? baseUrl = builder.Configuration["BaseApiPath"];

if (baseUrl is null)
{
    throw new ApplicationException("BaseUrl is not set");
}

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorageAsSingleton();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(baseUrl)
});

await builder.Build().RunAsync();