using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PhonemeFlow
{
    public static class NativeMacOSWrapper
    {
        [DllImport("PhonemeFlow.Core.MacOS.Universal", EntryPoint = "InitPhonemeX", CallingConvention = CallingConvention.Cdecl)]
        private static extern int InitPhonemeX(IntPtr dataPath, IntPtr voice);

        [DllImport("PhonemeFlow.Core.MacOS.Universal", EntryPoint = "TextToPhonemes", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr TextToPhonemes(IntPtr input);

        public static void Initialize(string path, string voice)
        {
            IntPtr pathPtr = IntPtr.Zero;
            IntPtr voicePtr = IntPtr.Zero;
            try
            {
                pathPtr = AllocateUtf8(path);
                voicePtr = AllocateUtf8(voice);
                int result = InitPhonemeX(pathPtr, voicePtr);
                if (result == 0)
                {
                    throw new InvalidOperationException($"Native macOS bridge rejected voice '{voice}'. See Editor log for details.");
                }
            }
            finally
            {
                FreeUtf8(pathPtr);
                FreeUtf8(voicePtr);
            }
        }

        public static string GetPhonemes(string text)
        {
            IntPtr inputPtr = IntPtr.Zero;
            try
            {
                inputPtr = AllocateUtf8(string.IsNullOrWhiteSpace(text) ? " " : text);
                IntPtr resultPtr = TextToPhonemes(inputPtr);
                if (resultPtr == IntPtr.Zero)
                {
                    Debug.LogWarning("[PhonemeFlow macOS] TextToPhonemes returned a null pointer.");
                    PhonemeFlowFeatureFlags.ActivateMacFallback("null_pointer", "Native returned null pointer.");
                    return string.Empty;
                }

                try
                {
                    string managed = Marshal.PtrToStringAnsi(resultPtr);
                    if (managed == null)
                    {
                        Debug.LogWarning("[PhonemeFlow macOS] Marshal returned null string.");
                        PhonemeFlowFeatureFlags.ActivateMacFallback("marshal_null", $"Pointer: 0x{resultPtr.ToInt64():X}");
                        return string.Empty;
                    }

                    return managed;
                }
                catch (AccessViolationException ex)
                {
                    Debug.LogWarning($"[PhonemeFlow macOS] Access violation while reading native string: {ex.Message}");
                    PhonemeFlowFeatureFlags.ActivateMacFallback("marshal_access_violation", ex.Message);
                    return string.Empty;
                }
            }
            finally
            {
                FreeUtf8(inputPtr);
            }
        }

        private static IntPtr AllocateUtf8(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Marshal.WriteByte(buffer + bytes.Length, 0);
            return buffer;
        }

        private static void FreeUtf8(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }
}
