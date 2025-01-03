using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Jam;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

HttpClient Http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddSingleton(Http);
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<MusicService>();
builder.Services.AddBlazorBootstrap();
builder.Services.AddBlazoredLocalStorageAsSingleton();

var host = builder.Build();

await host.Services.GetRequiredService<ConfigService>().OnInitializeAsync();

await host.RunAsync();

//dotnet watch run --no-hot-reload