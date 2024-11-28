import {fetchNextAudio} from "./api";

const audioPlayer = document.getElementById('audio-player') as HTMLAudioElement;

let audioStarted = false;
let audioInvoked = false;

export async function processAudioLoop() {
    if (audioInvoked)
        return;

    audioInvoked = true;

    let index = Math.floor(Math.random() * 1001);

    while (true) {
        try {
            const {metadata, downloadUrl} = await fetchNextAudio(index);
            const {author, name} = metadata;

            updateCredentials(author, name);
            await playAudio(downloadUrl);

            await new Promise<void>((resolve) => audioPlayer.onended = () => resolve());

            index++;
        } catch (error) {
            console.error(error);
            break;
        }
    }
}

function updateCredentials(author: string, name: string) {
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
        audioPlayer.load();

        audioPlayer.oncanplaythrough = () => {
            if (!audioStarted) {
                audioStarted = true;
                audioPlayer.play().then(() => {
                    audioPlayer.muted = false;
                    resolve(audioPlayer);
                }).catch(reject);
            }
        };

        audioPlayer.onerror = () => reject('Error loading audio');
    });
}