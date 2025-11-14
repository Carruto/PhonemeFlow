using System;
using System.IO;
using UnityEngine;

namespace PhonemeFlow
{
    public static class PhonemeFlowAdapter
    {
        private static IPhonemeFlow phonemizer;
        private static bool isInitialized;

        public static IPhonemeFlow Phonemizer
        {
            get => phonemizer;
            set => phonemizer = value;
        }

        public static bool IsInitialized => isInitialized;

        public static void Initialize(string userSuppliedPath, string voice)
        {
            string resolvedPath = userSuppliedPath;

#if UNITY_EDITOR || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            resolvedPath = ResolveDesktopDataPath(userSuppliedPath);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            phonemizer = new PhonemeFlow_Editor_MacOS();
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            phonemizer = new PhonemeFlow_Editor_Windows();
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            phonemizer = new PhonemeFlow_Editor_Linux();
#endif

#elif UNITY_WEBGL && !UNITY_EDITOR

            phonemizer = new PhonemeFlow_Runtime_WebGL();

#elif UNITY_ANDROID && !UNITY_EDITOR

            // The native adapter is assigned manually in the controller.
            if (phonemizer == null)
            {
                Debug.LogError("PhonemeFlowAdapter.Phonemizer was not assigned before Android Initialize().");
                return;
            }

#elif UNITY_IOS && !UNITY_EDITOR

            // On iOS, the controller handles copy + initialization explicitly.
            if (phonemizer == null)
            {
                Debug.LogError("PhonemeFlowAdapter.Phonemizer was not assigned before iOS Initialize().");
                return;
            }

#else
            Debug.LogWarning("PhonemeFlowAdapter: Unsupported platform or editor context.");
            return;
#endif

            if (phonemizer == null)
            {
                Debug.LogError("PhonemeFlowAdapter.Initialize could not find a phonemizer for this platform.");
                return;
            }

            bool initResult = false;
            try
            {
                initResult = phonemizer.Initialize(resolvedPath, voice);
            }
            catch (Exception ex)
            {
                Debug.LogError($"PhonemeFlowAdapter.Initialize failed: {ex.Message}");
            }

            isInitialized = initResult;
        }

#if UNITY_EDITOR || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
        private static string ResolveDesktopDataPath(string userSuppliedPath)
        {
            if (string.IsNullOrEmpty(userSuppliedPath) || userSuppliedPath == "/phoneme-data" || userSuppliedPath == "\\phoneme-data")
            {
                return ResolveWithFallback(Path.Combine(Application.streamingAssetsPath, "PhonemeFlowResources", "phoneme-data"));
            }

            if (userSuppliedPath.StartsWith("/") || userSuppliedPath.StartsWith("\\"))
            {
                string trimmed = userSuppliedPath.TrimStart('/', '\\');
                return ResolveWithFallback(Path.Combine(Application.streamingAssetsPath, NormalizeRelativePath(trimmed)));
            }

            string candidate = Path.IsPathRooted(userSuppliedPath)
                ? userSuppliedPath
                : Path.Combine(Application.streamingAssetsPath, NormalizeRelativePath(userSuppliedPath));

            return ResolveWithFallback(candidate);
        }

        private static string ResolveWithFallback(string primaryPath)
        {
            if (Directory.Exists(primaryPath))
            {
                return primaryPath;
            }

            string editorFallback = Path.Combine(Application.dataPath, "PhonemeFlow", "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");
            if (Directory.Exists(editorFallback))
            {
                Debug.LogWarning($"[PhonemeFlowAdapter] StreamingAssets data path missing ({primaryPath}). Using editor resources at {editorFallback}.");
                return editorFallback;
            }

            return primaryPath;
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }
#endif

        public static string GetPhonemes(string text)
        {
            if (phonemizer == null || !isInitialized)
            {
                throw new InvalidOperationException("PhonemeFlowAdapter is not initialized. Call Initialize(dataPath, voice) first.");
            }

            return phonemizer.GetPhonemes(text);
        }
    }

    public interface IPhonemeFlow
    {
        bool Initialize(string dataPath, string voice);
        string GetPhonemes(string text);
    }
}
