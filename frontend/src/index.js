"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var audioLoop_1 = require("./audioLoop");
var imageLoop_1 = require("./imageLoop");
document.body.addEventListener('click', audioLoop_1.processAudioLoop, { once: true });
document.body.addEventListener('keydown', audioLoop_1.processAudioLoop, { once: true });
document.addEventListener("DOMContentLoaded", function () {
    (0, imageLoop_1.processImageLoop)();
});
