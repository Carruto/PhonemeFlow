mergeInto(LibraryManager.library, {
  TextToPhonemes: function (inputPtr) {
    const inputText = UTF8ToString(inputPtr);
    console.log("[PhonemeFlow] TextToPhonemes called with:", inputText);

    const bridge = window.PhonemeFlow;
    if (!bridge || typeof bridge.getPhonemes !== "function") {
      console.error("[PhonemeFlow] getPhonemes is not available.");
      return 0;
    }

    const result = bridge.getPhonemes(inputText);
    if (!result) {
      console.warn("[PhonemeFlow] Empty phoneme result.");
      return 0;
    }

    const lengthBytes = lengthBytesUTF8(result) + 1;
    const resultPtr = _malloc(lengthBytes);
    stringToUTF8(result, resultPtr, lengthBytes);

    console.log("[PhonemeFlow] Returning result:", UTF8ToString(resultPtr));
    return resultPtr;
  },

  InitPhonemeX: function (dataPathPtr, voicePtr) {
    const dataPath = UTF8ToString(dataPathPtr);
    const voice = UTF8ToString(voicePtr);

    if (typeof window.PhonemeFlow !== "undefined" && typeof window.PhonemeFlow.init === "function") {
      window.PhonemeFlow.init(dataPath, voice);
      console.log("[PhonemeFlow] Init complete.");
    } else {
      console.error("[PhonemeFlow] init function not found on window.PhonemeFlow.");
    }
  }
});
