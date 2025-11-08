PhonemeFlowModule().then((Module) => {
    window.PhonemeFlow = {
        init: function (path, voice) {
            const pathPtr = Module._malloc(path.length + 1);
            Module.stringToUTF8(path, pathPtr, path.length + 1);

            const voicePtr = Module._malloc(voice.length + 1);
            Module.stringToUTF8(voice, voicePtr, voice.length + 1);

            Module._InitPhonemeX(pathPtr, voicePtr);

            Module._free(pathPtr);
            Module._free(voicePtr);
        },
        getPhonemes: function (text) {
            const textPtr = Module._malloc(text.length + 1);
            Module.stringToUTF8(text, textPtr, text.length + 1);

            const resultPtr = Module._TextToPhonemes(textPtr);
            const result = Module.UTF8ToString(resultPtr);

            Module._free(textPtr);
            return result;
        }
    };
    console.log('[PhonemeFlow] Bridge initialized.');
});
