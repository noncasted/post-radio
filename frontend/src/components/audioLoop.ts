import {fetchNextAudio} from "./api.js";

let audioInvoked = false;

document.body.addEventListener('click', startAudioPlayback, {once: true});
document.body.addEventListener('keydown', startAudioPlayback, {once: true});
document.getElementById('next-button')?.addEventListener('click', skipAudio);

const audioPlayer = document.getElementById('audio-player') as HTMLAudioElement;
const authorElement = document.getElementById('author') as HTMLElement;
const nameElement = document.getElementById('name') as HTMLElement;

async function startAudioPlayback() {
    if (audioInvoked)
        return;

    audioInvoked = true;

    let index = Math.floor(Math.random() * 1001);

    while (true) {
        try {
            const data = await fetchNextAudio(index);

            const {author, name} = data.metadata;
            updateAudioInfo(author, name);

            await playAudio(data.downloadUrl);

            await new Promise<void>((resolve) => {
                audioPlayer.onended = () => resolve();
            });

        } catch (error) {
            console.error(error);
            await new Promise<void>((resolve) => setTimeout(resolve, 3000));
        }

        index++;
    }
}

function playAudio(url: string): Promise<HTMLAudioElement> {
    return new Promise((resolve, reject) => {
        audioPlayer.volume = 1;
        audioPlayer.src = url;
        audioPlayer.load();

        audioPlayer.oncanplaythrough = () => {
            audioPlayer.play().then(() => {
                audioPlayer.muted = false;
                resolve(audioPlayer);
            }).catch(reject);
        };

        audioPlayer.onerror = () => reject('Error loading audio');
    });
}

function updateAudioInfo(author: string, name: string) {
    authorElement.textContent = author;
    nameElement.textContent = name;
}

function skipAudio() {
    audioPlayer.currentTime = audioPlayer.duration;
}
