const endpoint = "https://api.post-radio.io/";

/**
 * Makes a POST request to the specified API endpoint with a given payload.
 * @param path The API path (e.g., "image/getNext" or "audio/getNext").
 * @param payload The request payload (e.g., `{ Index: "1" }`).
 * @returns A Promise resolving to the JSON response.
 */
export async function post<T>(path: string, payload: object): Promise<T> {
    const url = `${endpoint}${path}`;
    console.log(`Sending POST request to: ${url} with payload:`, payload);

    try {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify(payload),
        });

        if (!response.ok) {
            console.error(`Failed to fetch from ${path}: ${response.statusText}`);
            throw new Error(`Failed to fetch from ${path}: ${response.statusText}`);
        }

        const responseData = await response.json();
        console.log(`Response from ${path}:`, responseData);
        return responseData;
    } catch (error) {
        console.error(`Error during POST request to ${path}:`, error);
        throw error;
    }
}

/**
 * Fetches the next image URL based on the given index.
 * @param index The current index.
 * @returns A Promise resolving to the image URL.
 */
export async function fetchNextImage(index: number): Promise<string> {
    console.log(`Fetching next image for index: ${index}`);
    const data = await post<{ url: string }>("image/getNext", { Index: index.toString() });
    console.log(`Next image URL: ${data.url}`);
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
    console.log(`Fetching next audio for index: ${index}`);

    // Explicitly define the type of the response here
    const data = await post<{
        metadata: { author: string; name: string };
        downloadUrl: string;
    }>("audio/getNext", { Index: index.toString() });

    console.log(`Next audio metadata:`, data.metadata);
    console.log(`Audio download URL: ${data.downloadUrl}`);

    return data;
}
