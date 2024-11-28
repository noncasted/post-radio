mergeInto(LibraryManager.library,
    {
        GetLanguageItch: function () {
            const language = navigator.systemLanguage || navigator.language;

            const languageString = language.toLowerCase();
            const bufferSize = lengthBytesUTF8(languageString) + 1;
            const buffer = _malloc(bufferSize);
            stringToUTF8(languageString, buffer, bufferSize);

            return buffer;
        }
    }
);