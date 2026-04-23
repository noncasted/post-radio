window.audioHelper.loadAndPlay = async (url, generation) => {
    const audio = window.audioHelper.audio();
    if (!audio) return { started: false, errorName: "no-audio-element", errorMessage: "audio element not found", readyState: 0, networkState: 0 };
    if (!url) return { started: false, errorName: "empty-url", errorMessage: "empty url", readyState: audio.readyState, networkState: audio.networkState };

    try {
        window.audioHelper.currentGeneration = generation;

        audio.pause();
        audio.removeAttribute("src");
        audio.load();

        audio.src = url;
        audio.currentTime = 0;
        audio.load();

        await audio.play();
        return {
            started: true,
            errorName: null,
            errorMessage: null,
            readyState: audio.readyState,
            networkState: audio.networkState,
            audioErrorCode: null,
            audioErrorMessage: null
        };
    } catch (e) {
        if (e.name !== "AbortError") console.error("play error:", e);
        return {
            started: false,
            errorName: e.name || "unknown",
            errorMessage: e.message || "",
            readyState: audio.readyState,
            networkState: audio.networkState,
            audioErrorCode: audio.error ? audio.error.code : null,
            audioErrorMessage: audio.error ? (audio.error.message || "") : null
        };
    }
};

window.audioHelper.getState = () => {
    const audio = window.audioHelper.audio();
    if (!audio) return null;

    const bufferedRanges = [];
    if (audio.buffered) {
        for (let i = 0; i < audio.buffered.length; i++) {
            bufferedRanges.push({
                start: window.audioHelper.safeNumber(audio.buffered.start(i)),
                end: window.audioHelper.safeNumber(audio.buffered.end(i))
            });
        }
    }

    return {
        readyState: audio.readyState,
        networkState: audio.networkState,
        currentTime: window.audioHelper.safeNumber(audio.currentTime),
        duration: window.audioHelper.safeNumber(audio.duration),
        paused: audio.paused,
        ended: audio.ended,
        muted: audio.muted,
        volume: window.audioHelper.safeNumber(audio.volume),
        seeking: audio.seeking,
        errorCode: audio.error ? audio.error.code : null,
        errorMessage: audio.error ? (audio.error.message || "") : null,
        src: audio.currentSrc || audio.src || null,
        buffered: bufferedRanges,
        hidden: document.hidden,
        visibilityState: document.visibilityState || "unknown",
        userAgent: navigator.userAgent
    };
};

window.audioHelper.stop = () => {
    const audio = window.audioHelper.audio();
    if (!audio) return;

    audio.pause();
    audio.removeAttribute("src");
    audio.load();
};

window.audioHelper.setVolume = (v) => {
    const audio = window.audioHelper.audio();
    if (!audio) return;

    audio.volume = v;
    audio.muted = v <= 0;
};
