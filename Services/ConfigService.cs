using Jelly;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

public class ConfigService
{
    private readonly HttpClient Http = null!;
    private readonly MusicService Music = null!;
    
    public string ApiUrl = "http://localhost:5169";
    public LoginRequest Login = new();
    public string AuthToken = "";
    public bool Authenticated = false;
    
    public ConfigService(HttpClient http, MusicService music)
    {
        Http = http;
        Music = music;
    }
    
    public async Task TryLogin()
    {
        HttpResponseMessage Response = await Http.PostAsJsonAsync($"{ApiUrl}/api/auth/login/", Login);
        
        if(Response.IsSuccessStatusCode)
        {
            LoginResponse Data = await Response.Content.ReadFromJsonAsync<LoginResponse>() ?? null!;
            
            AuthToken = Data.AuthToken!;
            Authenticated = true;
            
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            await Music.InitializeAsync(this);
        }
    }
}