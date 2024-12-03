import {fetchNextImage} from "./api.js";

const first = document.getElementById("image_1") as HTMLImageElement;
const second = document.getElementById("image_2") as HTMLImageElement;

async function updateImage(index: number): Promise<number> {
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

    return index + 1;
}

document.addEventListener("DOMContentLoaded", () => {
    let index = Math.floor(Math.random() * 1001);

    setInterval(async () => {
        index = await updateImage(index);
    }, 10000);

    updateImage(index);
});
