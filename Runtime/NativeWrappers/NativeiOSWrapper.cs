using System;
using System.Runtime.InteropServices;

namespace PhonemeFlow
{
#if UNITY_IOS && !UNITY_EDITOR

    public static class NativeiOSWrapper
    {
        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void InitPhonemeX(string dataPath, string voice);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr TextToPhonemes(string input);

    public static void Initialize(string path, string voice)
        {
#if UNITY_IOS && !UNITY_EDITOR

            if (path.StartsWith("file://")) {
                path = path.Substring(7);
            }
            InitPhonemeX(path, voice);
#else
            UnityEngine.Debug.LogWarning("PhonemeFlow native initialization is only available on iOS device builds.");
#endif
        }

        public static string GetPhonemes(string text)
        {
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr ptr = TextToPhonemes(text);
            return Marshal.PtrToStringAnsi(ptr);
#else
            return "This platform does not support native iOS phoneme access.";
#endif
        }
    }
#endif
}
