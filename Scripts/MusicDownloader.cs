using Jelly;
using System.Net.Http.Json;
using System.Collections.Concurrent;

public class MusicDownloader
{
    Action? OnStateChanged = null!;
    ConfigService Config = null!;
    HttpClient Http = null!;
    public MusicDownloader(Action update, ConfigService config, HttpClient http)
    {
        OnStateChanged = update;
        Config = config;
        Http = http;
    }
    
    public bool IsDownloading = false;
    public ConcurrentQueue<byte[]> Downloaded = new();
    public long DownloadedBytes = 0;
    public long FileSize = 0;
    void Cancel()
    {
        IsDownloading = false;
        Downloaded.Clear();
        DownloadedBytes = 0;
        FileSize = 0;

        //Console.WriteLine("Music Downloader Cancelled");
    }
    
    public async Task DownloadSong(SongEntity ActiveSong, CancellationToken Token)
    {
        //Console.WriteLine("Music Downloader Started");
        
        IsDownloading = true;
        FileSize = await Http.GetFromJsonAsync<long>($"{Config.ApiUrl}/api/getsize/{ActiveSong.MP3FileName}");
        for (long i = 0; i < FileSize; i += MusicService.ChunkSize)
        {
            if (Token.IsCancellationRequested)
            {
                Cancel();
                return;
            }

            long end = Math.Min(i + MusicService.ChunkSize - 1, FileSize - 1);
            byte[] chunk = await FetchAudioChunk(ActiveSong, i, end, Token);
            
            Downloaded.Enqueue(chunk);
            DownloadedBytes += chunk.Length;

            //Console.WriteLine(DownloadedBytes + " / " + FileSize);

            // Small delay between downloads
            OnStateChanged?.Invoke();
            await Task.Delay(100);
        }
        
        IsDownloading = false;
        OnStateChanged?.Invoke();
    }

    async Task<byte[]> FetchAudioChunk(SongEntity ActiveSong, long Start, long End, CancellationToken Token)
    {
        string query = $"{Config.ApiUrl}/api/getaudiochunk/?URL={ActiveSong.MP3FileName}&Start={Start}&End={End}";
        long expectedSize = End - Start + 1;
        
        byte[] chunk = await Http.GetByteArrayAsync(query, Token);
        
        if (chunk.Length > expectedSize)
            Array.Resize(ref chunk, (int) expectedSize);

        return chunk;
    }
}