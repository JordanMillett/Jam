using Jelly;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using KristofferStrube.Blazor.WebAudio;

public class MusicService
{
    public const long ChunkSize = 48000; //256000
    public const int BitrateKbps = 64;
    public const int BytesPerSecond = (BitrateKbps * 1000) / 8;
    
    public event Action? OnStateChanged = null!;

    public SongEntity ActiveSong = null!;
    
    ConfigService Config = null!;
    HttpClient Http = null!;
    IJSRuntime Runtime = null!;
    
    CancellationTokenSource ActiveTask = new();
    AudioContext AudioEnvironment  = null!;
    
    public MusicDownloader Downloader = null!;
    public MusicLoader Loader = null!;
    public MusicStreamer Streamer = null!;
    
    public List<(int SongID, string SongName)> Upcoming = new List<(int SongID, string SongName)>();
    public List<(int SongID, string SongName)> History = new List<(int SongID, string SongName)>();

    public MusicService(ConfigService config, HttpClient http, IJSRuntime runtime)
    {
        Config = config;
        Http = http;
        Runtime = runtime;
    }
    
    public async Task InitializeAsync()
    {
        Downloader = new MusicDownloader(OnStateChanged!, Config, Http);
        Loader = new MusicLoader(Downloader);
        Streamer = new MusicStreamer(Loader);
        
        //Fill queue with first album
        AlbumEntity Selected = await Http.GetFromJsonAsync<AlbumEntity>($"{Config.ApiUrl}/api/getalbum/{1}") ?? null!;
        for (int i = 0; i < Selected!.SongIDs!.Count; i++)
            Upcoming.Add((Selected.SongIDs[i], Selected.SongNames![i]));

        await NextSong();
    }
    
    public async Task ToggleAction()
    {
        if (AudioEnvironment == null)
            AudioEnvironment = await AudioContext.CreateAsync(Runtime);

        if(!Streamer.IsStreaming)
        {
            _ = PlayActiveSong();
        }else
        {
            if (await AudioEnvironment.GetStateAsync() == AudioContextState.Running)
                await AudioEnvironment.SuspendAsync();
            else
                await AudioEnvironment.ResumeAsync();
                
            OnStateChanged?.Invoke();
        }
    }
    
    async Task PlayActiveSong()
    {
        await ActiveTask.CancelAsync();
        ActiveTask = new CancellationTokenSource();
            
        await AudioEnvironment.CloseAsync();
        AudioEnvironment = await AudioContext.CreateAsync(Runtime);
            
        _ = Downloader.DownloadSong(ActiveSong, ActiveTask.Token);
        _ = Loader.ProcessChunks(AudioEnvironment, ActiveTask.Token);
        _ = Streamer.StartStreaming(AudioEnvironment, ActiveTask.Token);
        
        OnStateChanged?.Invoke();
    }
    
    public async Task NextSong()
    {
        if (Upcoming.Count == 0)
            return;
            
        if (ActiveSong != null)
            History.Insert(0, (ActiveSong.SongID, ActiveSong.SongName!));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{Upcoming[0].SongID}") ?? null!;
        Upcoming.RemoveAt(0);
        
        _ = PlayActiveSong();
    }
    
    public async Task LastSong()
    {
        if (History.Count == 0)
            return;
            
        if (ActiveSong != null)
            Upcoming.Insert(0, (ActiveSong.SongID, ActiveSong.SongName!));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{History[0].SongID}") ?? null!;
        History.RemoveAt(0);

        _ = PlayActiveSong();
    }
}