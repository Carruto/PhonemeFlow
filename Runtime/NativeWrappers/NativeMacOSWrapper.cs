using System;
using System.Runtime.InteropServices;

namespace PhonemeFlow
{
    public static class NativeMacOSWrapper
    {
        // Use CharSet.Ansi or Unicode depending on how the native lib is compiled
        [DllImport("PhonemeFlow.Core.MacOS.Universal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void InitPhonemeX(string dataPath, string voice);

        [DllImport("PhonemeFlow.Core.MacOS.Universal", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr TextToPhonemes(string input);

        public static void Initialize(string path, string voice)
        {
            InitPhonemeX(path, voice);
        }

        public static string GetPhonemes(string text)
        {
            IntPtr ptr = TextToPhonemes(text);
            return Marshal.PtrToStringAnsi(ptr);
        }
    }
}
