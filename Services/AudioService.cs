using Jelly;
using Microsoft.JSInterop;
using System.Net.Http.Json;

public class AudioService
{
    public SongEntity Current = null!;

    public List<(int SongID, string SongName)> Upcoming = new List<(int SongID, string SongName)>();
    public List<(int SongID, string SongName)> History = new List<(int SongID, string SongName)>();

    ConfigService Config = null!;
    HttpClient Http = null!;
    IJSRuntime Runtime = null!;

    public bool Paused = true;
    public long ByteOffset = 0;
    public long FileSize = 0;
    
    CancellationTokenSource AudioTaskControl = new CancellationTokenSource();
    Task? AudioTask = null;
    
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
            
        if (Current != null)
            History.Insert(0, (Current.SongID, Current.SongName));
            
        Current = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{Upcoming[0].SongID}");
        Upcoming.RemoveAt(0);
        
        await OnSongChange();
    }
    
    public async Task LastSong()
    {
        if (History.Count == 0)
            return;
            
        if (Current != null)
            Upcoming.Insert(0, (Current.SongID, Current.SongName));
            
        Current = await Http.GetFromJsonAsync<SongEntity>($"{Config.ApiUrl}/api/getsong/{History[0].SongID}");
        History.RemoveAt(0);

        await OnSongChange();
    }
    
    public async Task ToggleAction()
    {    
        if (Paused)
        {
            Paused = false;

            Play();
        }
        else
        {
            Paused = true;

            AudioTaskControl.Cancel();
            AudioTask = null;
            
            await Runtime.InvokeVoidAsync("stopAudio");
        }
    }
    
    async Task OnSongChange()
    {
        if(!Paused)
        {
            await Runtime.InvokeVoidAsync("stopAudio");

            ByteOffset = 0;

            Play();
        }
    }
    
    void Play()
    {
        AudioTaskControl.Cancel();
        AudioTaskControl = new CancellationTokenSource();

        AudioTask = PlayAudio(AudioTaskControl.Token);
    }
    
    async Task PlayAudio(CancellationToken Control)
    {
        const long chunkSize = 256000;
        FileSize = await Http.GetFromJsonAsync<long>($"{Config.ApiUrl}/api/getsize/{Current.MP3FileName}");

        for (long i = ByteOffset; i < FileSize; i += chunkSize)
        {
            long End = Math.Min(i + chunkSize - 1, FileSize - 1);
            Control.ThrowIfCancellationRequested();
            
            var chunk = await FetchAudioChunk(i, End, Control);
            long updatedOffset = await Runtime.InvokeAsync<long>("playAudioChunk", chunk, i);

            ByteOffset = updatedOffset;
            
            Control.ThrowIfCancellationRequested();         
        }
    }
    
    async Task<byte[]> FetchAudioChunk(long Start, long End, CancellationToken Control)
    {
        string query = $"{Config.ApiUrl}/api/getaudiochunk/?URL={Current.MP3FileName}&Start={Start}&End={End}";
        
        return await Http.GetByteArrayAsync(query, Control);
    }
}