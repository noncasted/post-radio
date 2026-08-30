// ES5 only. Owns the background slideshow: .NET hands over a batch of URLs and
// an interval, JS does the cycling, preloading and cross-fading on its own.
window.radioImages = window.radioImages || {};

(function (images, core) {
    var REFILL_THRESHOLD = 3;

    var state = {
        dotnet: null,
        urls: [],
        index: 0,
        intervalMs: 8000,
        fadeMs: 1000,
        timer: null,
        active: "b",
        pendingRefill: false,
        slots: null
    };

    function findSlots() {
        var slot = function (name) {
            return {
                main: document.getElementById("image-" + name + "-main"),
                left: document.getElementById("image-" + name + "-left"),
                right: document.getElementById("image-" + name + "-right"),
                hideTimer: null
            };
        };

        var found = { a: slot("a"), b: slot("b") };

        return (found.a.main && found.b.main) ? found : null;
    }

    function applyFade() {
        if (!state.slots)
            return;

        var seconds = (state.fadeMs / 1000) + "s";
        var apply = function (slot) {
            var parts = [slot.main, slot.left, slot.right];

            for (var i = 0; i < parts.length; i++) {
                if (parts[i])
                    parts[i].style.transition = "opacity " + seconds + " ease-in-out";
            }
        };

        apply(state.slots.a);
        apply(state.slots.b);
    }

    // A faded-out slot must stop being a hit-test target: otherwise the invisible
    // image on top steals the right-click and "copy image" grabs the wrong picture.
    function setSlot(slot, url, visible) {
        var parts = [slot.main, slot.left, slot.right];

        if (slot.hideTimer !== null && slot.hideTimer !== undefined) {
            window.clearTimeout(slot.hideTimer);
            slot.hideTimer = null;
        }

        for (var i = 0; i < parts.length; i++) {
            if (!parts[i])
                continue;

            if (url !== null)
                parts[i].src = url;

            parts[i].style.opacity = visible ? "1" : "0";
            parts[i].style.pointerEvents = visible ? "auto" : "none";

            if (visible)
                parts[i].style.visibility = "visible";
        }

        if (visible)
            return;

        // Once the fade is over the slot is fully out of the layout's hit testing.
        slot.hideTimer = window.setTimeout(function () {
            slot.hideTimer = null;

            for (var j = 0; j < parts.length; j++) {
                if (parts[j])
                    parts[j].style.visibility = "hidden";
            }
        }, state.fadeMs + 50);
    }

    function preload(url, done) {
        if (!url) {
            done(false);
            return;
        }

        var image = new Image();
        var settled = false;
        var settle = function (ok) {
            if (settled)
                return;

            settled = true;
            done(ok);
        };

        image.onload = function () { settle(true); };
        image.onerror = function () { settle(false); };
        image.src = url;

        // A TV that never fires onload must not freeze the slideshow.
        window.setTimeout(function () { settle(false); }, 15000);
    }

    function nextUrl() {
        if (state.urls.length === 0)
            return null;

        var url = state.urls[state.index % state.urls.length];
        state.index++;

        if (state.urls.length - state.index <= REFILL_THRESHOLD && !state.pendingRefill) {
            state.pendingRefill = true;
            core.invoke(state.dotnet, "OnImagesNeeded", state.urls.length);
        }

        return url;
    }

    function step() {
        if (!state.slots)
            return;

        var url = nextUrl();

        if (url === null)
            return;

        preload(url, function (ok) {
            if (!ok || !state.slots)
                return;

            var incoming = state.active === "a" ? state.slots.b : state.slots.a;
            var outgoing = state.active === "a" ? state.slots.a : state.slots.b;

            setSlot(incoming, url, true);
            setSlot(outgoing, null, false);
            state.active = state.active === "a" ? "b" : "a";
        });
    }

    function restartTimer() {
        if (state.timer !== null)
            window.clearInterval(state.timer);

        state.timer = window.setInterval(step, state.intervalMs);
    }

    images.start = function (dotnetRef, urls, options) {
        state.dotnet = dotnetRef;
        state.slots = findSlots();

        if (options) {
            if (options.intervalMs)
                state.intervalMs = options.intervalMs;

            if (options.fadeMs || options.fadeMs === 0)
                state.fadeMs = options.fadeMs;
        }

        state.urls = urls || [];
        state.index = 0;
        state.pendingRefill = false;
        applyFade();

        // The prerendered document paints the first picture into slot A before the circuit
        // is even up. Hiding it only to fade the same file back in would blank the
        // background for a whole fade, so adopt the slot instead of restarting from empty.
        var primed = state.slots && state.slots.a.main ? state.slots.a.main.getAttribute("src") : null;
        var adopt = primed && state.urls.length > 0 && state.urls[0] === primed;

        if (state.slots) {
            if (!adopt)
                setSlot(state.slots.a, null, false);

            setSlot(state.slots.b, null, false);
        }

        if (adopt) {
            setSlot(state.slots.a, primed, true);
            state.index = 1;
            state.active = "a";
        } else {
            step();
        }

        restartTimer();
    };

    images.append = function (urls) {
        state.pendingRefill = false;

        if (!urls || !urls.length)
            return;

        // Keep the list bounded: drop what has already been shown.
        if (state.index > 0 && state.urls.length > 200) {
            state.urls = state.urls.slice(state.index);
            state.index = 0;
        }

        for (var i = 0; i < urls.length; i++)
            state.urls.push(urls[i]);

        if (state.timer === null)
            restartTimer();

        if (state.urls.length === urls.length)
            step();
    };

    images.dispose = function () {
        if (state.timer !== null) {
            window.clearInterval(state.timer);
            state.timer = null;
        }

        if (state.slots) {
            var slots = [state.slots.a, state.slots.b];

            for (var i = 0; i < slots.length; i++) {
                if (slots[i].hideTimer !== null) {
                    window.clearTimeout(slots[i].hideTimer);
                    slots[i].hideTimer = null;
                }
            }
        }

        state.dotnet = null;
        state.urls = [];
        state.index = 0;
        state.slots = null;
    };
})(window.radioImages, window.radioCore);
