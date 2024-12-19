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

    public bool IsPlaying = false;
    public bool IsDownloading = false;
    
    private AudioContext AudioEnvironment  = null!;
    public AudioBufferSourceNode AudioSource = null!;
    
    CancellationTokenSource DownloadTasks = new();
    CancellationTokenSource PlaybackTasks = new();
    
    ConcurrentQueue<byte[]> DownloadedData = new();
    public long DownloadedBytes = 0;
    public long FileSize = 0;
    
    const long ChunkSize = 48000; //256000
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
    
    public async Task ToggleAction()
    {
        if (AudioEnvironment == null)
            AudioEnvironment = await AudioContext.CreateAsync(Runtime);

        await UpdatePaused();

        if(AudioSource == null)
        {
            _ = StartPlayback();
        }else
        {
            if (IsPlaying)
                _ = PausePlayback();
            else
                _ = ResumePlayback();
        }
    }

    async Task OnSongChange()
    {
        await EndPlayback();

        _ = StartDownload();
        
        await UpdatePaused();
        
        if (IsPlaying) //User was playing music before
            _ = StartPlayback();
            
        OnStateChanged?.Invoke();
    }
    
    async Task EndPlayback()
    {
        DownloadTasks.Cancel();
        DownloadTasks = new CancellationTokenSource();
        DownloadedData.Clear();
        DownloadedBytes = 0;
        
        PlaybackTasks.Cancel();
        PlaybackTasks = new CancellationTokenSource();
        if (AudioSource != null)
            await AudioSource.DisconnectAsync();
        AudioSource = null!;
        
        OnStateChanged?.Invoke();
    }

    async Task StartPlayback()
    {
        AudioSource = null!;
        AudioBufferSourceNode One = null!;
        AudioBufferSourceNode Two = null!;
        bool BackgroundLoadOne = false;
    
        while (DownloadedData.Count < 2 && !PlaybackTasks.Token.IsCancellationRequested)
            await Task.Delay(100);
        
        //PREP FIRST NODE
        DownloadedData.TryDequeue(out var data1);
        AudioBuffer buffer = await AudioEnvironment.DecodeAudioDataAsync(data1);
        One = await AudioEnvironment.CreateBufferSourceAsync();
        await One.SetBufferAsync(buffer);
        await One.ConnectAsync(await AudioEnvironment.GetDestinationAsync());
        await One.StartAsync();
        AudioSource = One;
        //WAIT FOR FIRST NODE
        double durationInSeconds = (double)data1.Length / BytesPerSecond;
        await Task.Delay(TimeSpan.FromSeconds(durationInSeconds));
    
        while (!PlaybackTasks.Token.IsCancellationRequested)
        {   
            if (DownloadedData.TryPeek(out var chunk))
            {
                Console.WriteLine("SWAPPING NODE");
                
                if(BackgroundLoadOne)
                {
                    //_ = PrepNode(chunk).ContinueWith(task => One = task.Result);
                    PrepNode(chunk, One);
                    AudioSource = Two; 
                }else
                {
                    PrepNode(chunk, Two);
                    AudioSource = One;
                }
                BackgroundLoadOne = !BackgroundLoadOne;

                durationInSeconds = (double)chunk.Length / BytesPerSecond;
                await Task.Delay(TimeSpan.FromSeconds(durationInSeconds));
                DownloadedData.TryDequeue(out _);
            }
            else
            {
                // Buffer underflow, wait for more chunks
                Console.WriteLine("WAITING");
                await Task.Delay(100);
            }
            
            while (!IsPlaying)
            {
                Console.WriteLine("STOPPED");
                await Task.Delay(100);
            }
        }

        _ = EndPlayback();
    }
    
    void PrepNode(byte[] chunk, AudioBufferSourceNode Node)
    {
        double durationInSeconds = (double)chunk.Length / BytesPerSecond;
        
         _ = AudioEnvironment.DecodeAudioDataAsync(chunk).ContinueWith(async task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                AudioBuffer Buffer = task.Result;
                Node = await AudioEnvironment.CreateBufferSourceAsync();
                _ = Node.SetBufferAsync(Buffer);
                _ = Node.ConnectAsync(await AudioEnvironment.GetDestinationAsync());
                _ = Node.StartAsync(durationInSeconds);
            }
        });
    }
    
    //GUI STUFF
    public async Task NextSong()
    {
        if (Upcoming.Count == 0)
            return;
            
        if (ActiveSong != null)
            History.Insert(0, (ActiveSong.SongID, ActiveSong.SongName));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{Upcoming[0].SongID}");
        Upcoming.RemoveAt(0);
        
        _ = OnSongChange();
    }
    
    public async Task LastSong()
    {
        if (History.Count == 0)
            return;
            
        if (ActiveSong != null)
            Upcoming.Insert(0, (ActiveSong.SongID, ActiveSong.SongName));
            
        ActiveSong = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{History[0].SongID}");
        History.RemoveAt(0);

        _ = OnSongChange();
    }
    
    async Task ResumePlayback()
    {
        await AudioEnvironment.ResumeAsync();
        await UpdatePaused();
        OnStateChanged?.Invoke();
    }
    
    async Task PausePlayback()
    {
        await AudioEnvironment.SuspendAsync();
        await UpdatePaused();
        OnStateChanged?.Invoke();
    }
    
    public async Task<bool> UpdatePaused()
    {
        if (AudioEnvironment == null)
        {
            IsPlaying = false;
            return true;
        }
        else
        {
            if (await AudioEnvironment.GetStateAsync() == AudioContextState.Running)
            {
                IsPlaying = true;
                return false;
            }else
            {
                IsPlaying = false;
                return true;
            }
        }
    }
    
    //DOWNLOADING
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

            //Console.WriteLine(DownloadedBytes + " / " + FileSize);

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