// ES5 only. Owns playback state end to end: queue, watchdog, resume probe and
// per-track diagnostics. Talks to .NET twice per track, never per audio event.
window.radioPlayer = window.radioPlayer || {};

(function (player, core) {
    var DEFAULTS = {
        volume: 0.5,
        startupTimeoutMs: 20000,
        progressTimeoutMs: 30000,
        bufferingTimeoutMs: 90000,
        watchdogIntervalMs: 500,
        resumeMaxAttempts: 2,
        resumeProbeMs: 1200,
        preloadLeadMs: 20000,
        starvedRetryMs: 3000,
        maxEvents: 40
    };

    var PROGRESS_EVENTS = ["timeupdate", "playing", "canplay", "canplaythrough"];
    var BUFFERING_EVENTS = ["waiting", "stalled", "suspend"];
    var TRACKED_EVENTS = ["loadedmetadata", "loadeddata", "durationchange", "play", "pause", "seeking", "seeked", "abort", "emptied"];

    var state = {
        dotnet: null,
        config: DEFAULTS,
        audio: null,
        preload: null,
        handlers: [],
        queue: [],
        current: null,
        diag: null,
        started: false,
        advancing: false,
        watchdogTimer: null,
        lastStarvedAt: 0,
        hiddenSince: null,
        preloadedUrl: null
    };

    function config() {
        return state.config;
    }

    function newDiagnostics() {
        return {
            createdAt: core.now(),
            startedAt: null,
            firstProgressAt: null,
            lastProgressAt: null,
            bufferingStartedAt: null,
            bufferingCount: 0,
            bufferingTotalMs: 0,
            hiddenMs: 0,
            resumeAttempts: [],
            resumeInFlight: false,
            events: [],
            playErrorName: null,
            playErrorMessage: null,
            lastErrorCode: null,
            lastErrorMessage: null
        };
    }

    function recordEvent(name) {
        var diag = state.diag;

        if (!diag)
            return;

        diag.events.push({
            name: name,
            atSeconds: (core.now() - diag.createdAt) / 1000,
            currentTime: state.audio ? core.safeNumber(state.audio.currentTime) : null,
            readyState: state.audio ? state.audio.readyState : 0,
            networkState: state.audio ? state.audio.networkState : 0
        });

        while (diag.events.length > config().maxEvents)
            diag.events.shift();
    }

    function markProgress(resetResumeAttempts) {
        var diag = state.diag;

        if (!diag)
            return;

        var now = core.now();

        if (resetResumeAttempts === true)
            diag.resumeAttempts = [];

        if (diag.bufferingStartedAt !== null) {
            diag.bufferingTotalMs += now - diag.bufferingStartedAt;
            diag.bufferingStartedAt = null;
        }

        if (diag.firstProgressAt === null)
            diag.firstProgressAt = now;

        diag.lastProgressAt = now;
    }

    function markBuffering() {
        var diag = state.diag;

        if (!diag || diag.bufferingStartedAt !== null)
            return;

        diag.bufferingStartedAt = core.now();
        diag.bufferingCount++;
    }

    function buildDiagnostics() {
        var diag = state.diag || newDiagnostics();
        var audio = state.audio;
        var now = core.now();
        var bufferingMs = diag.bufferingTotalMs
            + (diag.bufferingStartedAt !== null ? now - diag.bufferingStartedAt : 0);

        return {
            currentTime: audio ? core.safeNumber(audio.currentTime) : null,
            duration: audio ? core.safeNumber(audio.duration) : null,
            bufferedEnd: core.bufferedEnd(audio),
            readyState: audio ? audio.readyState : 0,
            networkState: audio ? audio.networkState : 0,
            paused: audio ? audio.paused : true,
            ended: audio ? audio.ended : false,
            seeking: audio ? audio.seeking : false,
            errorCode: diag.lastErrorCode !== null ? diag.lastErrorCode : core.errorCode(audio),
            errorMessage: diag.lastErrorMessage !== null ? diag.lastErrorMessage : core.errorMessage(audio),
            playErrorName: diag.playErrorName,
            playErrorMessage: diag.playErrorMessage,
            bufferingCount: diag.bufferingCount,
            bufferingTotalSeconds: bufferingMs / 1000,
            sinceStartSeconds: diag.startedAt !== null ? (now - diag.startedAt) / 1000 : 0,
            sinceProgressSeconds: diag.lastProgressAt !== null ? (now - diag.lastProgressAt) / 1000 : 0,
            hidden: core.isHidden(),
            visibilityState: core.visibilityState(),
            hiddenSeconds: diag.hiddenMs / 1000,
            resumeAttempts: diag.resumeAttempts,
            events: diag.events,
            userAgent: navigator.userAgent
        };
    }

    function attach() {
        detach();

        var audio = state.audio;

        if (!audio)
            return;

        var add = function (name, handler) {
            audio.addEventListener(name, handler);
            state.handlers.push(["audio", name, handler]);
        };

        var i;

        for (i = 0; i < PROGRESS_EVENTS.length; i++) {
            add(PROGRESS_EVENTS[i], (function (name) {
                return function () {
                    var isTimeUpdate = name === "timeupdate";

                    if (!isTimeUpdate)
                        recordEvent(name);

                    // timeupdate only proves the stream moves; a real (re)start clears resume budget.
                    markProgress(!isTimeUpdate);
                };
            })(PROGRESS_EVENTS[i]));
        }

        for (i = 0; i < BUFFERING_EVENTS.length; i++) {
            add(BUFFERING_EVENTS[i], (function (name) {
                return function () {
                    recordEvent(name);
                    markBuffering();
                };
            })(BUFFERING_EVENTS[i]));
        }

        for (i = 0; i < TRACKED_EVENTS.length; i++) {
            add(TRACKED_EVENTS[i], (function (name) {
                return function () { recordEvent(name); };
            })(TRACKED_EVENTS[i]));
        }

        add("ended", function () {
            recordEvent("ended");
            finish("ended");
        });

        add("error", function () {
            recordEvent("error");

            if (state.diag) {
                state.diag.lastErrorCode = core.errorCode(audio);
                state.diag.lastErrorMessage = core.errorMessage(audio);
            }

            var code = core.errorCode(audio);
            finish("media-error:" + (code === null ? "unknown" : code));
        });

        var visibilityHandler = function () {
            if (core.isHidden()) {
                state.hiddenSince = core.now();
                return;
            }

            if (state.hiddenSince !== null) {
                if (state.diag)
                    state.diag.hiddenMs += core.now() - state.hiddenSince;

                state.hiddenSince = null;
            }

            // The tab was frozen, not stalled: do not let the watchdog trip on catch-up.
            markProgress(true);
        };

        document.addEventListener("visibilitychange", visibilityHandler);
        state.handlers.push(["document", "visibilitychange", visibilityHandler]);
    }

    function detach() {
        var i;

        for (i = 0; i < state.handlers.length; i++) {
            var entry = state.handlers[i];

            if (entry[0] === "document")
                document.removeEventListener(entry[1], entry[2]);
            else if (state.audio)
                state.audio.removeEventListener(entry[1], entry[2]);
        }

        state.handlers = [];
    }

    function applyVolume() {
        var volume = Math.min(1, Math.max(0, state.config.volume));

        if (state.audio) {
            state.audio.volume = volume;
            state.audio.muted = volume <= 0;
        }

        if (state.preload)
            state.preload.muted = true;
    }

    function stopAudio() {
        if (!state.audio)
            return;

        try {
            state.audio.pause();
            state.audio.removeAttribute("src");
            state.audio.load();
        } catch (e) {
            core.log("stop failed", e);
        }
    }

    function clearPreload() {
        state.preloadedUrl = null;

        if (!state.preload)
            return;

        try {
            state.preload.removeAttribute("src");
            state.preload.load();
        } catch (e) {
            core.log("preload clear failed", e);
        }
    }

    function maybePreloadNext() {
        if (!state.preload || state.queue.length === 0 || !state.audio)
            return;

        var next = state.queue[0];

        if (state.preloadedUrl === next.url)
            return;

        var duration = core.safeNumber(state.audio.duration);
        var currentTime = core.safeNumber(state.audio.currentTime);

        if (duration === null || currentTime === null)
            return;

        // Only start the second download near the end, so it cannot starve playback.
        if ((duration - currentTime) * 1000 > config().preloadLeadMs)
            return;

        state.preloadedUrl = next.url;

        try {
            state.preload.src = next.url;
            state.preload.load();
        } catch (e) {
            core.log("preload failed", e);
            state.preloadedUrl = null;
        }
    }

    function requestQueue(reason) {
        var now = core.now();

        if (now - state.lastStarvedAt < config().starvedRetryMs)
            return;

        state.lastStarvedAt = now;
        core.invoke(state.dotnet, "OnQueueStarved", reason);
    }

    function playNext() {
        if (!state.started || state.advancing)
            return;

        if (state.queue.length === 0) {
            state.current = null;
            requestQueue("queue-empty");
            return;
        }

        var track = state.queue.shift();
        state.current = track;
        state.diag = newDiagnostics();
        clearPreload();

        var audio = state.audio;

        if (!audio) {
            finishTrack(track, "no-audio-element", buildDiagnostics());
            return;
        }

        try {
            audio.pause();
            audio.removeAttribute("src");
            audio.load();
            audio.src = track.url;
            audio.currentTime = 0;
            audio.load();
        } catch (e) {
            core.warn("load failed", e);
        }

        applyVolume();
        state.diag.startedAt = core.now();

        var promise;

        try {
            promise = audio.play();
        } catch (e) {
            onPlayRejected(track, e);
            return;
        }

        if (!promise || !promise.then) {
            core.invoke(state.dotnet, "OnTrackStarted", track.token, track.songId);
            return;
        }

        promise.then(
            function () {
                if (state.current === track)
                    core.invoke(state.dotnet, "OnTrackStarted", track.token, track.songId);
            },
            function (e) { onPlayRejected(track, e); });
    }

    function onPlayRejected(track, error) {
        var name = (error && error.name) ? error.name : "unknown";

        if (state.diag) {
            state.diag.playErrorName = name;
            state.diag.playErrorMessage = (error && error.message) ? error.message : "";
        }

        if (name === "AbortError") {
            core.log("play aborted", error);
            return;
        }

        core.warn("play failed", error);

        if (state.current === track)
            finish("play-failed:" + name);
    }

    function finish(reason) {
        var track = state.current;

        if (!track || state.advancing)
            return;

        finishTrack(track, reason, buildDiagnostics());
    }

    function finishTrack(track, reason, diagnostics) {
        state.advancing = true;
        state.current = null;
        state.diag = null;

        core.invoke(state.dotnet, "OnTrackFinished", track.token, track.songId, reason, diagnostics);

        state.advancing = false;
        playNext();
    }

    function watchdogReason() {
        var diag = state.diag;

        if (!diag)
            return null;

        var now = core.now();
        var cfg = config();

        if (diag.bufferingStartedAt !== null)
            return (now - diag.bufferingStartedAt >= cfg.bufferingTimeoutMs) ? "buffering-timeout" : null;

        if (diag.firstProgressAt === null) {
            return (diag.startedAt !== null && now - diag.startedAt >= cfg.startupTimeoutMs)
                ? "startup-timeout"
                : null;
        }

        return (now - diag.lastProgressAt >= cfg.progressTimeoutMs) ? "progress-timeout" : null;
    }

    function tick() {
        if (!state.started)
            return;

        if (!state.current) {
            if (state.queue.length > 0)
                playNext();
            else
                requestQueue("idle");

            return;
        }

        maybePreloadNext();

        var reason = watchdogReason();

        if (reason === null)
            return;

        // A sleeping TV is not a stalled stream: suppress and re-arm.
        if (core.isHidden()) {
            markProgress(false);
            return;
        }

        if (reason === "progress-timeout") {
            tryResume();
            return;
        }

        core.warn("watchdog trip: " + reason);
        finish(reason);
    }

    function tryResume() {
        var diag = state.diag;
        var track = state.current;
        var audio = state.audio;

        if (!diag || !track || !audio)
            return;

        if (diag.resumeAttempts.length >= config().resumeMaxAttempts) {
            core.warn("resume exhausted");
            finish("progress-timeout");
            return;
        }

        if (diag.resumeInFlight)
            return;

        diag.resumeInFlight = true;

        var attempt = diag.resumeAttempts.length + 1;
        var startedAt = core.now();
        var beforeTime = core.safeNumber(audio.currentTime);

        var complete = function (resumed, detail, errorMessage, nudged) {
            diag.resumeInFlight = false;

            diag.resumeAttempts.push({
                attempt: attempt,
                resumed: resumed,
                detail: detail,
                errorMessage: errorMessage || null,
                beforeTime: beforeTime,
                afterTime: core.safeNumber(audio.currentTime),
                nudged: nudged === true,
                elapsedMs: Math.round(core.now() - startedAt)
            });

            if (resumed && state.current === track) {
                markProgress(false);
                return;
            }

            if (state.current === track && diag.resumeAttempts.length >= config().resumeMaxAttempts)
                finish("progress-timeout");
        };

        var waitForAdvance = function (reference, onDone) {
            var deadline = core.now() + config().resumeProbeMs;

            var poll = function () {
                if (state.current !== track) {
                    diag.resumeInFlight = false;
                    return;
                }

                var current = core.safeNumber(audio.currentTime);

                if (current !== null && reference !== null && current > reference + 0.02) {
                    onDone(true);
                    return;
                }

                if (core.now() >= deadline) {
                    onDone(false);
                    return;
                }

                window.setTimeout(poll, 100);
            };

            window.setTimeout(poll, 100);
        };

        var nudge = function () {
            var current = core.safeNumber(audio.currentTime);
            var duration = core.safeNumber(audio.duration);

            if (current === null || duration === null || current >= duration - 0.25) {
                complete(false, "play-resolved-no-progress", null, false);
                return;
            }

            var target = Math.min(duration - 0.05, current + 0.05);

            try {
                audio.currentTime = target;
                play(audio);
            } catch (e) {
                complete(false, "nudge-failed", e && e.message, true);
                return;
            }

            waitForAdvance(target, function (advanced) {
                complete(advanced, advanced ? "time-advanced-after-nudge" : "nudge-no-progress", null, true);
            });
        };

        var play = function (media) {
            var promise = media.play();

            if (promise && promise.catch)
                promise.catch(function (e) { core.log("resume play rejected", e); });
        };

        try {
            play(audio);
        } catch (e) {
            complete(false, (e && e.name) ? e.name : "unknown", e && e.message, false);
            return;
        }

        waitForAdvance(beforeTime, function (advanced) {
            if (advanced) {
                complete(true, "time-advanced-after-play", null, false);
                return;
            }

            nudge();
        });
    }

    player.init = function (dotnetRef, options) {
        var key;

        state.dotnet = dotnetRef;
        state.config = {};

        for (key in DEFAULTS) {
            if (DEFAULTS.hasOwnProperty(key))
                state.config[key] = DEFAULTS[key];
        }

        if (options) {
            for (key in options) {
                if (options.hasOwnProperty(key) && options[key] !== null && options[key] !== undefined)
                    state.config[key] = options[key];
            }
        }

        state.audio = document.getElementById("audio");
        state.preload = document.getElementById("audio-preload");
        attach();
        applyVolume();

        if (state.watchdogTimer !== null)
            window.clearInterval(state.watchdogTimer);

        state.watchdogTimer = window.setInterval(tick, state.config.watchdogIntervalMs);
    };

    player.enqueue = function (tracks) {
        if (!tracks || !tracks.length)
            return;

        for (var i = 0; i < tracks.length; i++)
            state.queue.push(tracks[i]);

        if (state.started && !state.current)
            playNext();
    };

    player.queueLength = function () {
        return state.queue.length + (state.current ? 1 : 0);
    };

    player.start = function () {
        if (state.started) {
            if (state.audio && state.audio.paused && state.current)
                state.audio.play();

            return;
        }

        state.started = true;
        playNext();
    };

    player.skip = function () {
        if (!state.current) {
            playNext();
            return;
        }

        finish("skip-requested");
    };

    player.reset = function (reason) {
        state.queue = [];
        clearPreload();

        if (state.current)
            finish(reason || "playlist-changed");

        stopAudio();
        state.current = null;
        state.diag = null;
    };

    player.setVolume = function (value) {
        state.config.volume = value;
        applyVolume();
    };

    player.dispose = function () {
        if (state.watchdogTimer !== null) {
            window.clearInterval(state.watchdogTimer);
            state.watchdogTimer = null;
        }

        detach();
        stopAudio();
        clearPreload();
        state.dotnet = null;
        state.queue = [];
        state.current = null;
        state.diag = null;
        state.started = false;
    };
})(window.radioPlayer, window.radioCore);
