using KristofferStrube.Blazor.WebAudio;
using System.Collections.Concurrent;

public class MusicStreamer
{
    Action? OnStateChanged = null!;
    MusicDownloader Downloader = null!;
    public MusicStreamer(Action update, MusicDownloader downloader)
    {
        OnStateChanged = update;
        Downloader = downloader;
    }

    public bool IsStreaming = false;
    public bool IsPaused = true;
    public long LoadedBytes = 0;
    public double Percentage = 0;
    List<AudioBufferSourceNode> LiveSources = new();
    async Task Cancel()
    {
        foreach (AudioBufferSourceNode source in LiveSources)
        {
            await source.StopAsync();
            await source.DisposeAsync();
        }
        LiveSources.Clear();
        
        IsStreaming = false;
        IsPaused = true;
        LoadedBytes = 0;
        Percentage = 0;
        
        Console.WriteLine("Music Streamer Cancelled");
    }

    public async Task ProcessChunks(AudioContext AudioEnvironment, CancellationToken Token)
    {
        Console.WriteLine("Music Streamer Started");
    
        IsStreaming = true;
        double nextStartTime = await AudioEnvironment.GetCurrentTimeAsync();
        double bitrate = 0;
        double songDuration = 0;
        double passed = 0;
        while (!Token.IsCancellationRequested)
        {   
            IsPaused = await AudioEnvironment.GetStateAsync() != AudioContextState.Running;
            OnStateChanged?.Invoke();

            if (Downloader.Downloaded.TryPeek(out var chunk))
            {
                AudioBuffer buffer = await AudioEnvironment.DecodeAudioDataAsync(chunk);
                AudioBufferSourceNode source = await AudioEnvironment.CreateBufferSourceAsync();
                await source.SetBufferAsync(buffer);
                await source.ConnectAsync(await AudioEnvironment.GetDestinationAsync());
                
                LiveSources.Add(source);
                
                await source.StartAsync(nextStartTime);
                double duration = await buffer.GetDurationAsync();
                nextStartTime += duration;
                
                if(bitrate == 0)
                {
                    bitrate = (chunk.Length * 8) / duration;
                    songDuration = (Downloader.FileSize * 8) / bitrate;
                    //Console.WriteLine(songDuration);
                }
                

                Downloader.Downloaded.TryDequeue(out _);
                LoadedBytes += chunk.Length;
                OnStateChanged?.Invoke();
            }
            else
            {
                await Task.Delay(100);
            }
            
            Percentage = (await AudioEnvironment.GetCurrentTimeAsync() / songDuration) * 100.0;
        }

        await Cancel();
    }  
}