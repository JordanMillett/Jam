using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Text.Json;
using Jam;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

HttpClient Http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddSingleton(Http);

ConfigService Config = JsonSerializer.Deserialize<ConfigService>(await Http.GetStringAsync("config.json"))!;
builder.Services.AddSingleton(Config);

builder.Services.AddSingleton<MusicService>();
builder.Services.AddBlazorBootstrap();

var host = builder.Build();

MusicService Music = host.Services.GetRequiredService<MusicService>();
await Music.InitializeAsync();

await host.RunAsync();