var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
import { fetchNextImage } from "./api";
const first = document.getElementById("image_1");
const second = document.getElementById("image_2");
export function processImageLoop() {
    return __awaiter(this, void 0, void 0, function* () {
        let index = Math.floor(Math.random() * 1001);
        setInterval(() => __awaiter(this, void 0, void 0, function* () {
            index = yield updateImage(index);
        }), 10000);
    });
}
export function updateImage(index) {
    return __awaiter(this, void 0, void 0, function* () {
        try {
            const imageUrl = yield fetchNextImage(index);
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
        }
        catch (error) {
            console.error(error);
        }
        return index + 1;
    });
}
