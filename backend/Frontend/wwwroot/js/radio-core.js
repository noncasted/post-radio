// ES5 only: no arrow functions, no const/let, no destructuring, no for...of.
// The player must parse on old smart-TV WebKit builds.
window.radioCore = window.radioCore || {};

(function (core) {
    core.now = function () {
        return (window.performance && window.performance.now)
            ? window.performance.now()
            : new Date().getTime();
    };

    core.safeNumber = function (value) {
        return (typeof value === "number" && isFinite(value)) ? value : null;
    };

    core.bufferedEnd = function (media) {
        if (!media || !media.buffered || media.buffered.length === 0)
            return null;

        try {
            return core.safeNumber(media.buffered.end(media.buffered.length - 1));
        } catch (e) {
            return null;
        }
    };

    core.errorCode = function (media) {
        return (media && media.error) ? media.error.code : null;
    };

    core.errorMessage = function (media) {
        return (media && media.error) ? (media.error.message || "") : null;
    };

    core.visibilityState = function () {
        return document.visibilityState || (document.hidden ? "hidden" : "visible");
    };

    core.isHidden = function () {
        return document.hidden === true;
    };

    core.log = function (message, payload) {
        if (window.console && window.console.debug)
            window.console.debug("[radio] " + message, payload);
    };

    core.warn = function (message, payload) {
        if (window.console && window.console.warn)
            window.console.warn("[radio] " + message, payload);
    };

    // Fire-and-forget interop: a dead circuit must never break local playback.
    core.invoke = function (dotnet, name) {
        var args = Array.prototype.slice.call(arguments, 2);

        if (!dotnet)
            return;

        try {
            var promise = dotnet.invokeMethodAsync.apply(dotnet, [name].concat(args));

            if (promise && promise.catch)
                promise.catch(function (e) { core.log("interop failed: " + name, e); });
        } catch (e) {
            core.log("interop threw: " + name, e);
        }
    };
})(window.radioCore);
