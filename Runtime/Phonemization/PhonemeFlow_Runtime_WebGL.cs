namespace PhonemeFlow
{
#if !UNITY_EDITOR && UNITY_WEBGL

    public class PhonemeFlow_Runtime_WebGL : IPhonemeFlow
    {
        public bool Initialize(string dataPath, string voice)
        {
            NativeWebGLWrapper.Initialize(dataPath, voice);
            return true;
        }

        public string GetPhonemes(string text)
        {
            return NativeWebGLWrapper.GetPhonemes(text);
        }
    }
#endif

}
