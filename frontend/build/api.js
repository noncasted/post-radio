var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
const endpoint = "https://post-radio.io/";
/**
 * Makes a POST request to the specified API endpoint with a given payload.
 * @param path The API path (e.g., "image/getNext" or "audio/getNext").
 * @param payload The request payload (e.g., `{ Index: "1" }`).
 * @returns A Promise resolving to the JSON response.
 */
export function post(path, payload) {
    return __awaiter(this, void 0, void 0, function* () {
        const url = `${endpoint}${path}`;
        console.log(`Making POST request to: ${url}`);
        const response = yield fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify(payload),
        });
        if (!response.ok) {
            throw new Error(`Failed to fetch from ${path}: ${response.statusText}`);
        }
        return response.json();
    });
}
/**
 * Fetches the next image URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the image URL.
 */
export function fetchNextImage(index) {
    return __awaiter(this, void 0, void 0, function* () {
        const data = yield post("image/getNext", { Index: index.toString() });
        return data.url;
    });
}
/**
 * Fetches the next audio metadata and download URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the audio metadata and download URL.
 */
export function fetchNextAudio(index) {
    return __awaiter(this, void 0, void 0, function* () {
        return post("audio/getNext", { Index: index.toString() });
    });
}
