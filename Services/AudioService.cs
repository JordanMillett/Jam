using Jelly;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using KristofferStrube.Blazor.WebAudio;
using System.Collections.Concurrent;

public class AudioService
{
    public SongEntity ActiveSong = null!;

    public List<(int SongID, string SongName)> Upcoming = new List<(int SongID, string SongName)>();
    public List<(int SongID, string SongName)> History = new List<(int SongID, string SongName)>();

    ConfigService Config = null!;
    HttpClient Http = null!;
    IJSRuntime Runtime = null!;

    public bool IsPaused = true;
    public bool IsDownloading = false;
    
    private AudioContext AudioEnvironment  = null!;
    public AudioBufferSourceNode AudioSource = null!;
    
    CancellationTokenSource DownloadTasks = new();
    CancellationTokenSource PlaybackTasks = new();
    
    ConcurrentQueue<byte[]> DownloadedData = new();
    public long DownloadedBytes = 0;
    public long FileSize = 0;
    
    const long ChunkSize = 256000;
    const int BitrateKbps = 64;
    const int BytesPerSecond = (BitrateKbps * 1000) / 8;
    
    public event Action? OnStateChanged;
    
    public AudioService(ConfigService config, HttpClient http, IJSRuntime runtime)
    {
        Config = config;
        Http = http;
        Runtime = runtime;
    }
    
    public async Task InitializeAsync()
    {
        AlbumEntity Selected = await Http.GetFromJsonAsync<AlbumEntity>($"{Config.ApiUrl}/api/getalbum/{1}");

        for (int i = 0; i < Selected.SongIDs.Count; i++)
        {
            Upcoming.Add((Selected.SongIDs[i], Selected.SongNames[i]));
        }

        await NextSong();
    }

    public async Task NextSong()
    {
        if (Upcoming.Count == 0)
            return;
            
        if (ActiveSong != null)
            History.Insert(0, (ActiveSong.SongID, ActiveSong.SongName));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{Upcoming[0].SongID}");
        Upcoming.RemoveAt(0);
        
        await OnSongChange();
    }
    
    public async Task LastSong()
    {
        if (History.Count == 0)
            return;
            
        if (ActiveSong != null)
            Upcoming.Insert(0, (ActiveSong.SongID, ActiveSong.SongName));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{History[0].SongID}");
        History.RemoveAt(0);

        await OnSongChange();
    }
    
    public async Task ToggleAction()
    {
        if (AudioEnvironment == null)
            AudioEnvironment = await AudioContext.CreateAsync(Runtime);

        if(AudioSource == null)
        {
            await StartPlayback();
        }else
        {
            if (IsPaused)
                await ResumePlayback();
            else
                await PausePlayback();
        }
    }

    async Task OnSongChange()
    {
        await EndPlayback();

        _ = StartDownload();
        
        OnStateChanged?.Invoke();
        
        if (AudioSource != null && !IsPaused) //User was playing music before
            await StartPlayback();
    }
    
    async Task PausePlayback()
    {
        IsPaused = true;
        await AudioSource.StopAsync();
    }
    
    async Task EndPlayback()
    {
        DownloadTasks.Cancel();
        DownloadTasks = new CancellationTokenSource();
        DownloadedData.Clear();
        DownloadedBytes = 0;
        
        if (AudioSource != null)
        {
            PlaybackTasks.Cancel();
            PlaybackTasks = new CancellationTokenSource();
            await AudioSource.DisconnectAsync();
            AudioSource = null!;
        }
        
        OnStateChanged?.Invoke();
    }
    
    async Task ResumePlayback()
    {
        await AudioSource.StartAsync();
        IsPaused = false;
    }

    async Task StartPlayback()
    {   
        AudioSource = await AudioEnvironment.CreateBufferSourceAsync();
        IsPaused = false; 
        
        // Wait for some pre-buffering
        while (DownloadedData.Count < 1 && !PlaybackTasks.Token.IsCancellationRequested)
            await Task.Delay(100);
    
        // Play chunks from buffer
        while (!PlaybackTasks.Token.IsCancellationRequested)
        {
            if (DownloadedData.TryDequeue(out var chunk))
            {
                // Decode and play the chunk
                AudioBuffer downloaded = await AudioEnvironment.DecodeAudioDataAsync(chunk);
                await AudioSource.SetBufferAsync(downloaded);
                await AudioSource.ConnectAsync(await AudioEnvironment.GetDestinationAsync());
                await AudioSource.StartAsync();

                double durationInSeconds = (double)chunk.Length / BytesPerSecond;
                await Task.Delay(TimeSpan.FromSeconds(durationInSeconds));
            }
            else
            {
                // Buffer underflow, wait for more chunks
                await Task.Delay(100);
            }
        }

        await EndPlayback();
    }
    
    async Task StartDownload()
    {
        if (ActiveSong == null || IsDownloading) return;

        IsDownloading = true;
        FileSize = await Http.GetFromJsonAsync<long>($"{Config.ApiUrl}/api/getsize/{ActiveSong.MP3FileName}");
        for (long i = 0; i < FileSize; i += ChunkSize)
        {
            if (DownloadTasks.Token.IsCancellationRequested)
            {
                IsDownloading = false;
                break;
            }

            long end = Math.Min(i + ChunkSize - 1, FileSize - 1);
            byte[] chunk = await FetchAudioChunk(i, end);
            
            DownloadedData.Enqueue(chunk);
            DownloadedBytes += chunk.Length;

            Console.WriteLine(DownloadedBytes + " / " + FileSize);

            // Small delay between downloads
            OnStateChanged?.Invoke();
            await Task.Delay(1000);
        }
        
        IsDownloading = false;
        OnStateChanged?.Invoke();
    }

    async Task<byte[]> FetchAudioChunk(long Start, long End)
    {
        string query = $"{Config.ApiUrl}/api/getaudiochunk/?URL={ActiveSong.MP3FileName}&Start={Start}&End={End}";
        long expectedSize = End - Start + 1;
        
        byte[] chunk = await Http.GetByteArrayAsync(query, DownloadTasks.Token);
        
        if (chunk.Length > expectedSize)
            Array.Resize(ref chunk, (int) expectedSize);

        return chunk;
    }
}