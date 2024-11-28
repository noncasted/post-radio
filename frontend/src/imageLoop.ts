import { fetchNextImage } from "./api";

const first = document.getElementById("image_1") as HTMLImageElement;
const second = document.getElementById("image_2") as HTMLImageElement;

export async function processImageLoop() {
    let index = Math.floor(Math.random() * 1001);

    setInterval(async () => {
        index = await updateImage(index);
    }, 10000);
}

export async function updateImage(index: number): Promise<number> {
    try {
        const imageUrl = await fetchNextImage(index);
        console.log(`Fetched image URL: ${imageUrl}`);

        if (index % 2 === 0) {
            first.src = imageUrl;
            first.style.zIndex = "-1";
            first.style.opacity = "1";
            second.style.zIndex = "-2";
            second.style.opacity = "0";
        } else {
            second.src = imageUrl;
            second.style.zIndex = "-11";
            second.style.opacity = "1";
            first.style.zIndex = "-2";
            first.style.opacity = "0";
        }
    }
    catch (error) {
        console.error(error);
    }

    return index + 1;
}