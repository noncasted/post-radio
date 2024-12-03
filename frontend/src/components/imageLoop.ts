const imageEndpoint = "https://api.post-radio.io/";

async function fetchNextImage(index: number): Promise<string> {
    const apiUrl = imageEndpoint + "image/getNext";

    const response = await fetch(apiUrl, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
        body: JSON.stringify({ Index: index.toString() }),
    });

    if (!response.ok) {
        throw new Error(`Failed to fetch image: ${response.statusText}`);
    }

    const data = await response.json();
    return data.url;
}

async function updateImage(index: number): Promise<number> {
    const first = document.getElementById("image_1") as HTMLImageElement;
    const second = document.getElementById("image_2") as HTMLImageElement;

    const imageUrl = await fetchNextImage(index);
    console.log(`Fetched image URL: ${imageUrl}`);

    if (index % 2 === 0) {
        first.src = imageUrl;
        first.style.zIndex = "-1";
        first.style.opacity = "1";
        second.style.zIndex = "-2";
        second.style.opacity = "0";
    }
    else {
        second.src = imageUrl;
        second.style.zIndex = "-11";
        second.style.opacity = "1";
        first.style.zIndex = "-2";
        first.style.opacity = "0";
    }

    return index + 1;
}

document.addEventListener("DOMContentLoaded", () => {
    let index = Math.floor(Math.random() * 1001);

    // Update the image every 5 seconds
    setInterval(async () => {
        index = await updateImage(index);
    }, 10000);

    // Load the first image immediately
    updateImage(index);
});
