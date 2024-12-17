let audioElement = new Audio();
audioElement.controls = true;
document.body.appendChild(audioElement);

function playAudioChunk(chunk)
{
    return new Promise((resolve, reject) =>
    {
        const blob = new Blob([chunk], { type: 'audio/mpeg' });
        const url = URL.createObjectURL(blob);
        
        audioElement.src = url;
        
        // Set up event listener to resolve the promise when the audio ends
        audioElement.onended = () => {
            resolve();
        };

        // Play the audio and handle any errors
        audioElement.play().catch((error) => {
            reject(error);
        });
    });
}

function stopAudio()
{
    // Pause and reset the audio playback immediately
    audioElement.pause();
    audioElement.currentTime = 0;
}
