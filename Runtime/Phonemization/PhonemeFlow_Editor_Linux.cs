namespace PhonemeFlow
{
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX

    public class PhonemeFlow_Editor_Linux : IPhonemeFlow
    {

        public void Initialize(string dataPath, string voice)
        {
            NativeLinuxWrapper.Initialize(dataPath, voice);
        }

        public string GetPhonemes(string text)
        {
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return NativeLinuxWrapper.GetPhonemes(text);
#endif
            return "This platform is not supported by PhonemeFlow_Editor_Linux";
        }
    }

#endif

}
