const endpoint = "https://post-radio.io/";

/**
 * Makes a POST request to the specified API endpoint with a given payload.
 * @param path The API path (e.g., "image/getNext" or "audio/getNext").
 * @param payload The request payload (e.g., `{ Index: "1" }`).
 * @returns A Promise resolving to the JSON response.
 */
export async function post<T>(path: string, payload: object): Promise<T> {
    const url = `${endpoint}${path}`;
    console.log(`Making POST request to: ${url}`);

    const response = await fetch(url, {
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
}

/**
 * Fetches the next image URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the image URL.
 */
export async function fetchNextImage(index: number): Promise<string> {
    const data = await post<{ url: string }>("image/getNext", { Index: index.toString() });
    return data.url;
}

/**
 * Fetches the next audio metadata and download URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the audio metadata and download URL.
 */
export async function fetchNextAudio(index: number): Promise<{
    metadata: { author: string; name: string };
    downloadUrl: string;
}> {
    return post("audio/getNext", { Index: index.toString() });
}