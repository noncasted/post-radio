var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { fetchNextAudio } from "./api";
const audioPlayer = document.getElementById('audio-player');
let audioStarted = false;
let audioInvoked = false;
export function processAudioLoop() {
    return __awaiter(this, void 0, void 0, function* () {
        if (audioInvoked)
            return;
        audioInvoked = true;
        let index = Math.floor(Math.random() * 1001);
        while (true) {
            try {
                const { metadata, downloadUrl } = yield fetchNextAudio(index);
                const { author, name } = metadata;
                updateCredentials(author, name);
                yield playAudio(downloadUrl);
                yield new Promise((resolve) => audioPlayer.onended = () => resolve());
                index++;
            }
            catch (error) {
                console.error(error);
                break;
            }
        }
    });
}
function updateCredentials(author, name) {
    const authorElement = document.getElementById('author');
    const nameElement = document.getElementById('name');
    authorElement.textContent = author;
    nameElement.textContent = name;
}
function playAudio(url) {
    return new Promise((resolve, reject) => {
        const audioPlayer = document.getElementById('audio-player');
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
