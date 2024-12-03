let audioStarted = false; // Flag to track if the audio has started
let audioInvoked = false;

const audioEndpoint = "https://api.post-radio.io/";

document.body.addEventListener('click', startAudioPlayback, { once: true });
document.body.addEventListener('keydown', startAudioPlayback, { once: true });
document.getElementById('next-button')?.addEventListener('click', skipAudio);

const audioPlayer = document.getElementById('audio-player') as HTMLAudioElement;

async function fetchNextAudio(index: number): Promise<any> {
    const url = audioEndpoint + "audio/getNext";
    console.log('get next audio: ' + url);
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ Index: index.toString() })
    });

    if (!response.ok) {
        throw new Error('Failed to fetch audio');
    }

    return response.json();
}

function updateAudioInfo(author: string, name: string) {
    const authorElement = document.getElementById('author') as HTMLElement;
    const nameElement = document.getElementById('name') as HTMLElement;

    authorElement.textContent = author;
    nameElement.textContent = name;
}

function playAudio(url: string): Promise<HTMLAudioElement> {
    return new Promise((resolve, reject) => {
        audioPlayer.volume = 0.2;
        audioPlayer.src = url;
        audioPlayer.load(); // Ensure the audio is loaded
        
        // When the audio is ready to play, start automatically
        audioPlayer.oncanplaythrough = () => {
            // If it's the first user interaction, unmute and play
            if (!audioStarted) {
                audioStarted = true; // Mark audio as started

                audioPlayer.play().then(() => {
                    // Unmute once playback begins
                    audioPlayer.muted = false;
                    audioStarted = false; // Mark audio as started
                    resolve(audioPlayer);
                }).catch(reject);
            }
        };

        audioPlayer.onerror = () => reject('Error loading audio');
    });
}

async function startAudioPlayback() {
    if (audioInvoked)
        return;

    audioInvoked = true;

    let index = Math.floor(Math.random() * 1001);

    while (true) {
        try {
            const data = await fetchNextAudio(index);

            const { author, name } = data.metadata;
            updateAudioInfo(author, name);

            await playAudio(data.downloadUrl);

            // Wait for the audio to finish playing
            await new Promise<void>((resolve) => {
                audioPlayer.onended = () => resolve();
            });

            // Increment index to fetch the next audio
            index++;
        } catch (error) {
            console.error(error);
            index++;
            break;
        }
    }
}

function skipAudio() {
    const audioPlayer = document.getElementById('audio-player') as HTMLAudioElement;
    audioPlayer.currentTime = audioPlayer.duration;
}
