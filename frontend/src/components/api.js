"use strict";
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
Object.defineProperty(exports, "__esModule", { value: true });
exports.post = post;
exports.fetchNextImage = fetchNextImage;
exports.fetchNextAudio = fetchNextAudio;
var endpoint = "https://api.post-radio.io/";
/**
 * Makes a POST request to the specified API endpoint with a given payload.
 * @param path The API path (e.g., "image/getNext" or "audio/getNext").
 * @param payload The request payload (e.g., `{ Index: "1" }`).
 * @returns A Promise resolving to the JSON response.
 */
function post(path, payload) {
    return __awaiter(this, void 0, void 0, function () {
        var url, response, responseData, error_1;
        return __generator(this, function (_a) {
            switch (_a.label) {
                case 0:
                    url = "".concat(endpoint).concat(path);
                    console.log("Sending POST request to: ".concat(url, " with payload:"), payload);
                    _a.label = 1;
                case 1:
                    _a.trys.push([1, 4, , 5]);
                    return [4 /*yield*/, fetch(url, {
                            method: "POST",
                            headers: {
                                "Content-Type": "application/json",
                            },
                            body: JSON.stringify(payload),
                        })];
                case 2:
                    response = _a.sent();
                    if (!response.ok) {
                        console.error("Failed to fetch from ".concat(path, ": ").concat(response.statusText));
                        throw new Error("Failed to fetch from ".concat(path, ": ").concat(response.statusText));
                    }
                    return [4 /*yield*/, response.json()];
                case 3:
                    responseData = _a.sent();
                    console.log("Response from ".concat(path, ":"), responseData);
                    return [2 /*return*/, responseData];
                case 4:
                    error_1 = _a.sent();
                    console.error("Error during POST request to ".concat(path, ":"), error_1);
                    throw error_1;
                case 5: return [2 /*return*/];
            }
        });
    });
}
/**
 * Fetches the next image URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the image URL.
 */
function fetchNextImage(index) {
    return __awaiter(this, void 0, void 0, function () {
        var data;
        return __generator(this, function (_a) {
            switch (_a.label) {
                case 0:
                    console.log("Fetching next image for index: ".concat(index));
                    return [4 /*yield*/, post("image/getNext", { Index: index.toString() })];
                case 1:
                    data = _a.sent();
                    console.log("Next image URL: ".concat(data.url));
                    return [2 /*return*/, data.url];
            }
        });
    });
}
/**
 * Fetches the next audio metadata and download URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the audio metadata and download URL.
 */
function fetchNextAudio(index) {
    return __awaiter(this, void 0, void 0, function () {
        var data;
        return __generator(this, function (_a) {
            switch (_a.label) {
                case 0:
                    console.log("Fetching next audio for index: ".concat(index));
                    return [4 /*yield*/, post("audio/getNext", { Index: index.toString() })];
                case 1:
                    data = _a.sent();
                    console.log("Next audio metadata:", data.metadata);
                    console.log("Audio download URL: ".concat(data.downloadUrl));
                    return [2 /*return*/, data];
            }
        });
    });
}
