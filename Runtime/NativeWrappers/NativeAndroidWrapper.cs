using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PhonemeFlow
{
    public static class NativeAndroidWrapper
    {

#if UNITY_ANDROID

        // Internal native function bindings using IntPtr
        [DllImport("PhonemeFlow.Core.Android", EntryPoint = "InitPhonemeX", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _InitPhonemeFlow(IntPtr path, IntPtr voice);

        [DllImport("PhonemeFlow.Core.Android", EntryPoint = "TextToPhonemes", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr _TextToPhonemes(IntPtr input);

        // Safe public wrapper with manual marshaling
        public static void Initialize(string path, string voice)
        {
            Debug.Log($"[PhonemeFlow] Calling native Init with path={path}, voice={voice}");

            IntPtr pathPtr = Marshal.StringToHGlobalAnsi(path);
            IntPtr voicePtr = Marshal.StringToHGlobalAnsi(voice);

            _InitPhonemeFlow(pathPtr, voicePtr);

            Marshal.FreeHGlobal(pathPtr);
            Marshal.FreeHGlobal(voicePtr);

        }

        public static string GetPhonemes(string text)
        {
#if UNITY_ANDROID
            if (!PhonemeFlowAdapter.IsInitialized)
            {
                Debug.LogWarning("[PhonemeFlow] Tried to phonemize before init.");
                return "";
            }

            IntPtr inputPtr = Marshal.StringToHGlobalAnsi(text);
            IntPtr resultPtr = _TextToPhonemes(inputPtr);
            Marshal.FreeHGlobal(inputPtr);

            return Marshal.PtrToStringAnsi(resultPtr);
#else
    return "Unsupported platform";
#endif
        }

#endif

    }
}
