window.audioHelper.setAudioElement = (dotnetHelper) => {
    const audio = window.audioHelper.audio();
    window.audioHelper.detach();
    if (!audio) return;

    const send = (eventName, generation) => {
        if (!audio) return;

        dotnetHelper.invokeMethodAsync(
            "OnAudioEvent",
            eventName,
            generation,
            window.audioHelper.safeNumber(audio.currentTime),
            window.audioHelper.safeNumber(audio.duration),
            audio.readyState,
            audio.networkState,
            audio.paused,
            audio.ended,
            window.audioHelper.bufferedEnd(audio),
            audio.error ? audio.error.code : null,
            audio.error ? (audio.error.message || "") : null)
            .catch((e) => console.debug("audio event callback failed:", e));
    };

    const add = (eventName) => {
        const handler = () => send(eventName, window.audioHelper.currentGeneration);
        audio.addEventListener(eventName, handler);
        window.audioHelper.handlers.push([eventName, handler]);
    };

    ["timeupdate", "ended", "error", "waiting", "stalled", "playing", "canplay",
     "canplaythrough", "pause", "play", "seeking", "seeked",
     "durationchange", "loadedmetadata", "loadeddata", "suspend",
     "abort", "emptied"]
        .forEach(add);

    const visibilityHandler = () => {
        dotnetHelper.invokeMethodAsync(
            "OnVisibilityChange",
            document.hidden,
            document.visibilityState || "unknown")
            .catch((e) => console.debug("visibility callback failed:", e));
    };
    document.addEventListener("visibilitychange", visibilityHandler);
    window.audioHelper.handlers.push(["__visibility__", visibilityHandler]);
};

window.audioHelper.detach = () => {
    const audio = window.audioHelper.audio();
    for (const [eventName, handler] of window.audioHelper.handlers) {
        if (eventName === "__visibility__") {
            document.removeEventListener("visibilitychange", handler);
        } else if (audio) {
            audio.removeEventListener(eventName, handler);
        }
    }

    window.audioHelper.handlers = [];
};
