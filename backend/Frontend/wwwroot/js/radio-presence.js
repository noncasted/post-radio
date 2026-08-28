// ES5 only. Presence heartbeat: the single source of the online counter.
//
// A beat is sent only while audio is really playing, so a page view, a crawler or an
// abandoned tab never counts as a listener. The client id is a coarse device fingerprint
// rather than a per-circuit id, so two tabs of the same browser collapse into one listener.
window.radioPresence = window.radioPresence || {};

(function (presence, core) {
    var ENDPOINT = "/api/radio/presence/beat";
    var STORAGE_KEY = "radio.cid";
    var BEAT_MS = 30000;

    var clientId = null;
    var timer = null;
    var isListening = null;
    var lastBeatAt = 0;

    // FNV-1a: a few lines of ES5, and collisions only cost one merged listener.
    function hash(value) {
        var result = 0x811c9dc5;

        for (var i = 0; i < value.length; i++) {
            result ^= value.charCodeAt(i);
            result = (result + (result << 1) + (result << 4) + (result << 7) + (result << 8) + (result << 24)) >>> 0;
        }

        return result.toString(16);
    }

    function fingerprint() {
        var screenPart = "0x0x0";
        var parts;

        if (window.screen)
            screenPart = window.screen.width + "x" + window.screen.height + "x" + (window.screen.colorDepth || 0);

        parts = [
            navigator.userAgent || "",
            navigator.platform || "",
            navigator.language || "",
            (navigator.languages && navigator.languages.join(",")) || "",
            navigator.hardwareConcurrency || 0,
            screenPart,
            window.devicePixelRatio || 0,
            new Date().getTimezoneOffset()
        ];

        // Two hashes over different orderings: 32 bits alone collide too eagerly.
        return hash(parts.join("|")) + hash(parts.reverse().join("|"));
    }

    function read() {
        try {
            return window.localStorage ? window.localStorage.getItem(STORAGE_KEY) : null;
        } catch (e) {
            return null;
        }
    }

    function write(value) {
        try {
            if (window.localStorage)
                window.localStorage.setItem(STORAGE_KEY, value);
        } catch (e) {
            core.log("presence: client id not persisted", e);
        }
    }

    // Storage keeps the id stable when the fingerprint drifts (a moved window, a browser
    // update); the fingerprint keeps it stable when storage is unavailable (private mode).
    presence.clientId = function () {
        if (clientId !== null)
            return clientId;

        clientId = read();

        if (!clientId) {
            clientId = fingerprint();
            write(clientId);
        }

        return clientId;
    };

    function beat() {
        var request;

        try {
            request = new XMLHttpRequest();
            request.open("POST", ENDPOINT, true);
            request.setRequestHeader("X-Radio-Client-Id", presence.clientId());
            request.send();
        } catch (e) {
            core.log("presence beat failed", e);
        }
    }

    function tick() {
        var listening = isListening !== null && isListening() === true;

        if (!listening)
            return;

        var now = core.now();

        if (now - lastBeatAt < BEAT_MS - 1000)
            return;

        lastBeatAt = now;
        beat();
    }

    // Started once per page, with a predicate telling whether audio is currently playing.
    presence.start = function (listeningPredicate) {
        isListening = listeningPredicate;

        if (timer !== null)
            return;

        timer = window.setInterval(tick, 5000);
        tick();
    };

    presence.stop = function () {
        if (timer !== null) {
            window.clearInterval(timer);
            timer = null;
        }

        isListening = null;
    };
})(window.radioPresence, window.radioCore);
