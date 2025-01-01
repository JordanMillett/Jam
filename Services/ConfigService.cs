using Jelly;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Microsoft.JSInterop;
using Blazored.LocalStorage;

public class ConfigService
{
    private readonly HttpClient Http = null!;
    private readonly MusicService Music = null!;
    private readonly IJSRuntime Runtime = null!;
    private readonly ILocalStorageService Local = null!;

    public string ApiUrl = "https://jelly.jordanmillett.net";
    //public string ApiUrl = "http://localhost:5169";
    public LoginRequest Login = new();
    public string AuthToken = "";
    public bool Authenticated = false;
    
    public ConfigService(HttpClient http, MusicService music, IJSRuntime runtime, ILocalStorageService local)
    {
        Http = http;
        Music = music;
        Runtime = runtime;
        Local = local;
    }

    public async Task OnInitializeAsync()
    {
        Console.WriteLine("CALLED");
        await TryLoadLoginDetails();
        
        await Runtime.InvokeVoidAsync("loadServiceWorker");
        //var dotNetReference = DotNetObjectReference.Create(this);
        //await Runtime.InvokeVoidAsync("setupMessageListener", dotNetReference);
        
        await TryLogin();
    }
    
    public async Task TryLoadLoginDetails()
    {
        if(await Local.ContainKeyAsync("login"))
        {
            try
            {
                Login = await Local.GetItemAsync<LoginRequest>("login") ?? new();
            }catch
            {
                await Local.RemoveItemAsync("login");
            }
        }
    }
    
    public async Task TryLogin()
    {
        HttpResponseMessage Response = await Http.PostAsJsonAsync($"{ApiUrl}/api/auth/login/", Login);
        
        if(Response.IsSuccessStatusCode)
        {
            LoginResponse Data = await Response.Content.ReadFromJsonAsync<LoginResponse>() ?? null!;
            
            AuthToken = Data.AuthToken!;
            Authenticated = true;
            await Local.SetItemAsync<LoginRequest>("login", Login);
            
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            await Music.InitializeAsync(this);
        }
    }
}