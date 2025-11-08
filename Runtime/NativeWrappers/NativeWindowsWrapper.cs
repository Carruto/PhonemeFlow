using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PhonemeFlow
{

    public static class NativeWindowsWrapper
    {
        private const string PluginName = "PhonemeFlow.Core.Windows.dll";
        private static bool libraryLoaded;

        static NativeWindowsWrapper()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            TryLoadPlugin();
#endif
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static void TryLoadPlugin()
        {
            if (libraryLoaded)
            {
                return;
            }

            try
            {
                string[] candidatePaths =
                {
                    Path.Combine(Application.dataPath, "PhonemeFlow", "Plugins", "PhonemeFlow", "Windows", PluginName),
                    Path.Combine(Application.dataPath, "Plugins", "PhonemeFlow", "Windows", PluginName),
                    Path.Combine(Application.dataPath, "Plugins", "x86_64", PluginName),
                    Path.Combine(Application.dataPath, "Plugins", PluginName)
                };

                foreach (string candidate in candidatePaths)
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    IntPtr handle = LoadLibrary(candidate);
                    libraryLoaded = handle != IntPtr.Zero;

                    if (!libraryLoaded)
                    {
                        Debug.LogWarning($"[PhonemeFlow] Failed to preload native library at: {candidate}");
                    }
                    else
                    {
                        Debug.Log($"[PhonemeFlow] Loaded native plugin from: {candidate}");
                        break;
                    }
                }

                if (!libraryLoaded)
                {
                    Debug.LogWarning("[PhonemeFlow] Native plugin could not be located in known search paths.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhonemeFlow] Exception while attempting to load native plugin manually: {ex.Message}");
            }
        }

        public enum Error
        {
            EE_OK = 0,
            EE_INTERNAL_ERROR = -1,
            EE_BUFFER_FULL = 1,
            EE_NOT_FOUND = 2
        }

        private enum AudioOutput
        {
            Playback,
            Retrieval,
            Synchronous,
            SynchronousPlayback
        }

        public static void Initialize(string path, string voice = "en")
        {
            if (espeak_Initialize(AudioOutput.Retrieval, 0, path, 0) == -1)
            {
                Debug.Log("Could not initialize PhonemeFlow core (eSpeak-ng). Is the data path correct? (" + path + ")");
                throw new Exception("Failed to initialize PhonemeFlow core (eSpeak-ng). Check data path: " + path);
            }

            Debug.Log("PhonemeFlow core (eSpeak-ng) initialized successfully.");
            if (SetVoiceByName(voice) != Error.EE_OK)
            {
                throw new Exception("Could not set voice '" + voice + "'. Voice not found or invalid data path.");
            }
        }

        public static string TextToPhonemes(string text)
        {
            Debug.Log("Converting to phonemes: '" + text + "'");
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
            IntPtr intPtr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, intPtr, bytes.Length);
            IntPtr textptr = intPtr;
            string text2 = "";
            try
            {
                IntPtr intPtr2;
                do
                {
                    intPtr2 = textptr;
                    IntPtr intPtr3 = espeak_TextToPhonemes(ref textptr, 1, 2);
                    if (intPtr3 == IntPtr.Zero)
                    {
                        Debug.Log("eSpeak returned null phoneme output.");
                        break;
                    }

                    string text3 = PtrToUtf8String(intPtr3);
                    if (string.IsNullOrEmpty(text3))
                    {
                        break;
                    }

                    text2 = text2 + text3 + " ";
                }
                while (!(textptr == IntPtr.Zero) && !(textptr == intPtr2));
                return text2.Trim();
            }
            finally
            {
                Marshal.FreeHGlobal(intPtr);
            }
        }

        public static Error SetVoiceByName(string name)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name + "\0");
            IntPtr intPtr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, intPtr, bytes.Length);
            Error result = espeak_SetVoiceByName_Internal(intPtr);
            Marshal.FreeHGlobal(intPtr);
            return result;
        }

        private static string PtrToUtf8String(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            int i;
            for (i = 0; Marshal.ReadByte(ptr, i) != 0; i++)
            {
            }

            byte[] array = new byte[i];
            Marshal.Copy(ptr, array, 0, i);
            return Encoding.UTF8.GetString(array);
        }

        [DllImport("PhonemeFlow.Core.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int espeak_Initialize(AudioOutput output, int bufferLength, string path, int options);

        [DllImport("PhonemeFlow.Core.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr espeak_TextToPhonemes(ref IntPtr textptr, int textmode, int phonememode);

        [DllImport("PhonemeFlow.Core.Windows.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "espeak_SetVoiceByName")]
        private static extern Error espeak_SetVoiceByName_Internal(IntPtr name);
    }

}
