const endpoint = "https://post-radio.io/";

let audioStarted = false; // Flag to track if the audio has started
let audioInvoked = false;

async function fetchNextAudio(index: number): Promise<any> {
    const url =endpoint + "audio/getNext";
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
        const audioPlayer = document.getElementById('audio-player') as HTMLAudioElement;
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

            // Update the UI with the author and name
            const { author, name } = data.metadata;
            updateAudioInfo(author, name);

            // Play the audio automatically after user interaction
            await playAudio(data.downloadUrl);

            // Wait for the audio to finish playing
            await new Promise<void>((resolve) => {
                const audioPlayer = document.getElementById('audio-player') as HTMLAudioElement;
                audioPlayer.onended = () => resolve();
            });

            // Increment index to fetch the next audio
            index++;
        } catch (error) {
            console.error(error);
            break;
        }
    }
}

async function fetchNextImage(index: number): Promise<string> {
    const apiUrl = endpoint + "image/getNext";

    const response = await fetch(apiUrl, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
        body: JSON.stringify({ Index: index.toString() }),
    });

    if (!response.ok) {
        throw new Error(`Failed to fetch image: ${response.statusText}`);
    }

    const data = await response.json();
    return data.url;
}

async function updateImage(index: number): Promise<number> {
    const first = document.getElementById("image_1") as HTMLImageElement;
    const second = document.getElementById("image_2") as HTMLImageElement;

    const imageUrl = await fetchNextImage(index);
    console.log(`Fetched image URL: ${imageUrl}`);

    if (index % 2 === 0) {
        first.src = imageUrl;
        first.style.zIndex = "-1";
        first.style.opacity = "1";
        second.style.zIndex = "-2";
        second.style.opacity = "0";
    }
    else {
        second.src = imageUrl;
        second.style.zIndex = "-11";
        second.style.opacity = "1";
        first.style.zIndex = "-2";
        first.style.opacity = "0";
    }

    return index + 1;
}

document.body.addEventListener('click', startAudioPlayback, { once: true });
document.body.addEventListener('keydown', startAudioPlayback, { once: true });

document.addEventListener("DOMContentLoaded", () => {
    let index = Math.floor(Math.random() * 1001);

    // Update the image every 5 seconds
    setInterval(async () => {
        index = await updateImage(index);
    }, 10000);

    // Load the first image immediately
    updateImage(index);
});
