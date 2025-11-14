
namespace PhonemeFlow
{
    public class PhonemeFlow_Editor_Windows : IPhonemeFlow
    {

        /// Initializes the phoneme converter with the given data path and voice.
        /// <param name="dataPath">The path to the phoneme data.</param>
        /// <param name="voice">The voice to use for the conversion.</param>

        public bool Initialize(string dataPath, string voice)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            NativeWindowsWrapper.Initialize(dataPath, voice);
            return true;
#else
            return false;
#endif
        }

        public string GetPhonemes(string text)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return NativeWindowsWrapper.TextToPhonemes(text);
#else
            return "This platform is not supported by PhonemeFlow_Editor_Windows";
#endif

        }
    }
}
