window.audioHelper = window.audioHelper || {};
window.audioHelper.currentGeneration = window.audioHelper.currentGeneration || 0;
window.audioHelper.handlers = window.audioHelper.handlers || [];
window.audioHelper.safeNumber = (value) => Number.isFinite(value) ? value : null;
window.audioHelper.audio = () => document.getElementById("audio");
window.audioHelper.bufferedEnd = (audio) => audio.buffered && audio.buffered.length > 0
    ? window.audioHelper.safeNumber(audio.buffered.end(audio.buffered.length - 1))
    : null;
