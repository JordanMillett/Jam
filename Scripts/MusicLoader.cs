using KristofferStrube.Blazor.WebAudio;
using System.Collections.Concurrent;

public class MusicLoader
{
    MusicDownloader Downloader = null!;
    public MusicLoader(MusicDownloader downloader)
    {
        Downloader = downloader;
    }
    
    public ConcurrentQueue<(double Bytes, AudioBufferSourceNode Source)> Loaded = new();
    void Cancel()
    {
        Loaded.Clear();
        Console.WriteLine("Music Loader Cancelled");
    }

    public async Task ProcessChunks(AudioContext AudioEnvironment, CancellationToken Token)
    {
        Console.WriteLine("Music Loader Started");
        
        while (!Token.IsCancellationRequested)
        {   
            if (Downloader.Downloaded.TryPeek(out var chunk))
            {
                AudioBuffer buffer = await AudioEnvironment.DecodeAudioDataAsync(chunk);
                AudioBufferSourceNode source = await AudioEnvironment.CreateBufferSourceAsync();
                await source.SetBufferAsync(buffer);
                await source.ConnectAsync(await AudioEnvironment.GetDestinationAsync());
                Downloader.Downloaded.TryDequeue(out _);
                Loaded.Enqueue((chunk.Length, source));
            }
            else
            {
                await Task.Delay(100);
            }
        }

        Cancel();
    }  
}