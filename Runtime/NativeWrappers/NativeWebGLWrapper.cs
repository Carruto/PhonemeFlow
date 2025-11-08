#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PhonemeFlow
{
    public class NativeWebGLWrapper : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void InitPhonemeX(string dataPath, string voice);

        [DllImport("__Internal")]
        private static extern IntPtr TextToPhonemes(IntPtr inputPtr);

        public static void Initialize(string dataPath, string voice)
        {
            InitPhonemeX(dataPath, voice);
        }

        public static string GetPhonemes(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            IntPtr inputPtr = Marshal.AllocHGlobal(inputBytes.Length + 1);
            Marshal.Copy(inputBytes, 0, inputPtr, inputBytes.Length);
            Marshal.WriteByte(inputPtr, inputBytes.Length, 0); // null terminator

            IntPtr resultPtr = TextToPhonemes(inputPtr);
            string result = Marshal.PtrToStringUTF8(resultPtr);

            Marshal.FreeHGlobal(inputPtr);
            return result;
        }
    }
}
#endif
