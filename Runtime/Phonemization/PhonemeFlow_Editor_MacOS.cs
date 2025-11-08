
namespace PhonemeFlow
{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX

    public class PhonemeFlow_Editor_MacOS : IPhonemeFlow
    {
        public void Initialize(string dataPath, string voice)
        {
            NativeMacOSWrapper.Initialize(dataPath, voice);
        }

        public string GetPhonemes(string text)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return NativeMacOSWrapper.GetPhonemes(text);
#endif
            return "This platform is not supported by PhonemeFlow_Editor_MacOS";
        }
    }

#endif

}
