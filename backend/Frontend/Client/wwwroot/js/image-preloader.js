window.imagePreloader = {
    load: function (url) {
        return new Promise(function (resolve) {
            if (!url) {
                resolve(false);
                return;
            }
            var img = new Image();
            img.onload = function () { resolve(true); };
            img.onerror = function () { resolve(false); };
            img.src = url;
        });
    }
};
