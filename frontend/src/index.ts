import { processAudioLoop } from './audioLoop';
import { processImageLoop } from './imageLoop';

document.body.addEventListener('click', processAudioLoop, {once: true});
document.body.addEventListener('keydown', processAudioLoop, {once: true});

document.addEventListener("DOMContentLoaded", () => {
    processImageLoop();
});
