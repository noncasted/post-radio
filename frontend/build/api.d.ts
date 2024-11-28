/**
 * Makes a POST request to the specified API endpoint with a given payload.
 * @param path The API path (e.g., "image/getNext" or "audio/getNext").
 * @param payload The request payload (e.g., `{ Index: "1" }`).
 * @returns A Promise resolving to the JSON response.
 */
export declare function post<T>(path: string, payload: object): Promise<T>;
/**
 * Fetches the next image URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the image URL.
 */
export declare function fetchNextImage(index: number): Promise<string>;
/**
 * Fetches the next audio metadata and download URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the audio metadata and download URL.
 */
export declare function fetchNextAudio(index: number): Promise<{
    metadata: {
        author: string;
        name: string;
    };
    downloadUrl: string;
}>;
