using Jelly;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using KristofferStrube.Blazor.WebAudio;

public class MusicService
{
    public const long ChunkSize = 256000; //256000
    
    public event Action? RefreshHook = null!;
    event Action? OnStateChanged = null!;

    public SongEntity ActiveSong = null!;

    ConfigService Config = null!;
    HttpClient Http = null!;
    IJSRuntime Runtime = null!;
    
    CancellationTokenSource ActiveTask = new();
    AudioContext AudioEnvironment  = null!;
    
    public MusicDownloader Downloader = null!;
    public MusicStreamer Streamer = null!;
    
    public List<(int SongID, string SongName)> Upcoming = new List<(int SongID, string SongName)>();
    public List<(int SongID, string SongName)> History = new List<(int SongID, string SongName)>();

    public bool InteractionPaused = false;

    public Sequence Sequence = null!;

    public MusicService(HttpClient http, IJSRuntime runtime)
    {
        Http = http;
        Runtime = runtime;
    }
    
    public async Task InitializeAsync(ConfigService config)
    {
        Config = config;
        
        OnStateChanged += RefreshAll;
        
        Downloader = new MusicDownloader(OnStateChanged!, Config, Http);
        Streamer = new MusicStreamer(OnStateChanged!, Downloader);
        
        Sequence = await Http.GetFromJsonAsync<Sequence>($"{Config.ApiUrl}/api/get/sequence/") ?? null!;
        
        //Fill queue with first album
        AlbumEntity Selected = await Http.GetFromJsonAsync<AlbumEntity>($"{Config.ApiUrl}/api/get/album/{1}") ?? null!;
        for (int i = 0; i < Selected!.SongIDs!.Count; i++)
            Upcoming.Add((Selected.SongIDs[i], Selected.SongNames![i]));

        //Set as first song
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/get/song/{Upcoming[0].SongID}") ?? null!;
        Upcoming.RemoveAt(0);
    }
    
    void RefreshAll()
    {
        RefreshHook?.Invoke();
    }
    
    void Dispose()
    {
        OnStateChanged -= RefreshAll;
    }
    
    public async Task ToggleAction()
    {
        if (InteractionPaused)
            return;
        InteractionPaused = true;
        
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

            InteractionPaused = false;
        }
    }
    
    public void PlayInstantly((int SongID, string SongName) Chosen)
    {
        Upcoming.Insert(0, (Chosen.SongID, Chosen.SongName));   
        _ = NextSong();
    }
    
    async Task PlayActiveSong()
    {
        await ActiveTask.CancelAsync();
        ActiveTask = new CancellationTokenSource();
            
        await AudioEnvironment.CloseAsync();
        AudioEnvironment = await AudioContext.CreateAsync(Runtime);
        
        while (Streamer.IsStreaming)
            await Task.Delay(100);
            
        _ = Downloader.DownloadSong(ActiveSong, ActiveTask.Token);
        _ = Streamer.ProcessChunks(AudioEnvironment, ActiveTask.Token);
        
        while (Streamer.LoadedBytes < 0)
            await Task.Delay(100);
            
        InteractionPaused = false;
    }
    
    public async Task NextSong()
    {
        if (InteractionPaused)
            return;
        if (Upcoming.Count == 0)
            return;
        InteractionPaused = true;
            
        if (ActiveSong != null)
            History.Insert(0, (ActiveSong.SongID, ActiveSong.SongName!));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/get/song/{Upcoming[0].SongID}") ?? null!;
        Upcoming.RemoveAt(0);
        
        _ = PlayActiveSong();
    }
    
    public async Task LastSong()
    {
        if (InteractionPaused)
            return;
        if (History.Count == 0)
            return;
        InteractionPaused = true;
            
        if (ActiveSong != null)
            Upcoming.Insert(0, (ActiveSong.SongID, ActiveSong.SongName!));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/get/song/{History[0].SongID}") ?? null!;
        History.RemoveAt(0);

        _ = PlayActiveSong();
    }
}