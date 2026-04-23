window.audioHelper.tryResumePlayback = async () => {
    const audio = window.audioHelper.audio();
    if (!audio || !audio.getAttribute("src")) return { resumed: false, reason: "no-src" };

    const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
    const waitForTimeAdvance = async (referenceTime, timeoutMs) => {
        const start = performance.now();
        while (performance.now() - start < timeoutMs) {
            await sleep(100);
            if (Number.isFinite(audio.currentTime) && Number.isFinite(referenceTime) && audio.currentTime > referenceTime + 0.02) {
                return true;
            }
        }

        return false;
    };

    const beforeTime = audio.currentTime;
    const beforePaused = audio.paused;
    const startedAt = performance.now();
    let afterPlayTime = null;
    let afterPlayPaused = null;
    let nudged = false;
    let nudgeFromTime = null;
    let nudgeToTime = null;

    const resumeResult = (resumed, reason, errorMessage = null) => ({
        resumed,
        reason,
        errorMessage,
        beforeTime: window.audioHelper.safeNumber(beforeTime),
        beforePaused,
        afterPlayTime,
        afterPlayPaused,
        currentTime: window.audioHelper.safeNumber(audio.currentTime),
        duration: window.audioHelper.safeNumber(audio.duration),
        bufferedEnd: window.audioHelper.bufferedEnd(audio),
        readyState: audio.readyState,
        networkState: audio.networkState,
        paused: audio.paused,
        ended: audio.ended,
        seeking: audio.seeking,
        nudged,
        nudgeFromTime: window.audioHelper.safeNumber(nudgeFromTime),
        nudgeToTime: window.audioHelper.safeNumber(nudgeToTime),
        probeElapsedMs: Math.round(performance.now() - startedAt),
        hidden: document.hidden,
        visibilityState: document.visibilityState || "unknown",
        userAgent: navigator.userAgent
    });

    try {
        await audio.play();
        afterPlayTime = window.audioHelper.safeNumber(audio.currentTime);
        afterPlayPaused = audio.paused;

        if (await waitForTimeAdvance(beforeTime, 1200)) {
            return resumeResult(true, "time-advanced-after-play");
        }

        const current = audio.currentTime;
        const duration = audio.duration;
        if (Number.isFinite(current) && Number.isFinite(duration) && current < duration - 0.25) {
            nudged = true;
            nudgeFromTime = current;
            nudgeToTime = Math.min(duration - 0.05, current + 0.05);
            audio.currentTime = nudgeToTime;
            await audio.play();

            if (await waitForTimeAdvance(nudgeToTime, 1200)) {
                return resumeResult(true, "time-advanced-after-nudge");
            }
        }

        return resumeResult(false, nudged ? "play-resolved-nudge-no-progress" : "play-resolved-no-progress");
    } catch (e) {
        return resumeResult(false, e.name || "unknown", e.message || "");
    }
};
