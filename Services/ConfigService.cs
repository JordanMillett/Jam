using Jelly;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

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
    
    public string FullImageURL(string FileName)
    {
        return ApiUrl + "/api/get/image/" + FileName;
    }
    
    public async Task TryLogin()
    {
        HttpResponseMessage Response = await Http.PostAsJsonAsync($"{ApiUrl}/api/auth/login/", Login);

        Console.WriteLine(AuthToken);
        
        if(Response.IsSuccessStatusCode)
        {
            LoginResponse Data = await Response.Content.ReadFromJsonAsync<LoginResponse>() ?? null!;
            
            AuthToken = Data.AuthToken!;
            Authenticated = true;

            await Music.InitializeAsync(this);
        }
        
        Console.WriteLine(AuthToken);
    }
}