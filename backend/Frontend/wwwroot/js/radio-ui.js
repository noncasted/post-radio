// ES5 only. Chrome that must keep working with no circuit attached:
// idle auto-hide of the controls and removal of the pre-hydration splash.
window.radioUi = window.radioUi || {};

(function (ui) {
    var HIDE_MS = 5000;
    var timer = null;

    function show() {
        document.body.className = document.body.className.replace(/\s*controls-hidden/g, "");

        if (timer !== null)
            window.clearTimeout(timer);

        timer = window.setTimeout(function () {
            document.body.className += " controls-hidden";
        }, HIDE_MS);
    }

    ui.ready = function () {
        var splash = document.getElementById("splash");

        if (splash && splash.parentNode)
            splash.parentNode.removeChild(splash);
    };

    if (window.addEventListener) {
        window.addEventListener("mousemove", show, false);
        window.addEventListener("touchstart", show, false);
        window.addEventListener("keydown", show, false);
        window.addEventListener("click", show, false);
    }

    show();
})(window.radioUi);
