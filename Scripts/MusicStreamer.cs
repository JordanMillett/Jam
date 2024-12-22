using KristofferStrube.Blazor.WebAudio;
using System.Collections.Concurrent;

public class MusicStreamer
{
    MusicLoader Loader = null!;
    public MusicStreamer(MusicLoader loader)
    {
        Loader = loader;
    }

    public bool IsStreaming = false;
    public bool IsPaused = false;
    void Cancel()
    {
        IsStreaming = false;
        IsPaused = false;

        Console.WriteLine("Music Streamer Cancelled");
    }
    
    public async Task StartStreaming(AudioContext AudioEnvironment, CancellationToken Token)
    {
        Console.WriteLine("Music Streamer Started");

        IsStreaming = true;
        while (Loader.Loaded.Count < 2 && !Token.IsCancellationRequested)
            await Task.Delay(100);
        
        while (!Token.IsCancellationRequested)
        {
            IsPaused = await AudioEnvironment.GetStateAsync() != AudioContextState.Running;
            
            if(IsPaused)
                await Task.Delay(100);
            
            if (Loader.Loaded.TryPeek(out var pair))
            {
                _ = pair.Source.StartAsync();
                
                double durationInSeconds = pair.Bytes / MusicService.BytesPerSecond;
                await Task.Delay(TimeSpan.FromSeconds(durationInSeconds));
                
                Loader.Loaded.TryDequeue(out _);

            }
            else
            {
                await Task.Delay(100);
            }
        }

        Cancel();
    }
}