var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g = Object.create((typeof Iterator === "function" ? Iterator : Object).prototype);
    return g.next = verb(0), g["throw"] = verb(1), g["return"] = verb(2), typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (g && (g = 0, op[0] && (_ = 0)), _) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
var _a;
var audioStarted = false; // Flag to track if the audio has started
var audioInvoked = false;
var audioEndpoint = "https://api.post-radio.io/";
document.body.addEventListener('click', startAudioPlayback, { once: true });
document.body.addEventListener('keydown', startAudioPlayback, { once: true });
(_a = document.getElementById('next-button')) === null || _a === void 0 ? void 0 : _a.addEventListener('click', skipAudio);
var audioPlayer = document.getElementById('audio-player');
function fetchNextAudio(index) {
    return __awaiter(this, void 0, void 0, function () {
        var url, response;
        return __generator(this, function (_a) {
            switch (_a.label) {
                case 0:
                    url = audioEndpoint + "audio/getNext";
                    console.log('get next audio: ' + url);
                    return [4 /*yield*/, fetch(url, {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json'
                            },
                            body: JSON.stringify({ Index: index.toString() })
                        })];
                case 1:
                    response = _a.sent();
                    if (!response.ok) {
                        throw new Error('Failed to fetch audio');
                    }
                    return [2 /*return*/, response.json()];
            }
        });
    });
}
function updateAudioInfo(author, name) {
    var authorElement = document.getElementById('author');
    var nameElement = document.getElementById('name');
    authorElement.textContent = author;
    nameElement.textContent = name;
}
function playAudio(url) {
    return new Promise(function (resolve, reject) {
        audioPlayer.volume = 0.2;
        audioPlayer.src = url;
        audioPlayer.load(); // Ensure the audio is loaded
        // When the audio is ready to play, start automatically
        audioPlayer.oncanplaythrough = function () {
            // If it's the first user interaction, unmute and play
            if (!audioStarted) {
                audioStarted = true; // Mark audio as started
                audioPlayer.play().then(function () {
                    // Unmute once playback begins
                    audioPlayer.muted = false;
                    audioStarted = false; // Mark audio as started
                    resolve(audioPlayer);
                }).catch(reject);
            }
        };
        audioPlayer.onerror = function () { return reject('Error loading audio'); };
    });
}
function startAudioPlayback() {
    return __awaiter(this, void 0, void 0, function () {
        var index, data, _a, author, name_1, error_1;
        return __generator(this, function (_b) {
            switch (_b.label) {
                case 0:
                    if (audioInvoked)
                        return [2 /*return*/];
                    audioInvoked = true;
                    index = Math.floor(Math.random() * 1001);
                    _b.label = 1;
                case 1:
                    if (!true) return [3 /*break*/, 8];
                    _b.label = 2;
                case 2:
                    _b.trys.push([2, 6, , 7]);
                    return [4 /*yield*/, fetchNextAudio(index)];
                case 3:
                    data = _b.sent();
                    _a = data.metadata, author = _a.author, name_1 = _a.name;
                    updateAudioInfo(author, name_1);
                    return [4 /*yield*/, playAudio(data.downloadUrl)];
                case 4:
                    _b.sent();
                    // Wait for the audio to finish playing
                    return [4 /*yield*/, new Promise(function (resolve) {
                            audioPlayer.onended = function () { return resolve(); };
                        })];
                case 5:
                    // Wait for the audio to finish playing
                    _b.sent();
                    // Increment index to fetch the next audio
                    index++;
                    return [3 /*break*/, 7];
                case 6:
                    error_1 = _b.sent();
                    console.error(error_1);
                    index++;
                    return [3 /*break*/, 8];
                case 7: return [3 /*break*/, 1];
                case 8: return [2 /*return*/];
            }
        });
    });
}
function skipAudio() {
    var audioPlayer = document.getElementById('audio-player');
    audioPlayer.currentTime = audioPlayer.duration;
}
