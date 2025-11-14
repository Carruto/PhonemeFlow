using System;
using System.IO;
using UnityEngine;

namespace PhonemeFlow
{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX

    public class PhonemeFlow_Editor_MacOS : IPhonemeFlow
    {
        private const string DefaultVoice = "en";

        private bool _isInitialized;
        private string _dataPath;
        private string _voice;

        public bool Initialize(string dataPath, string voice)
        {
            if (!IsRunningOnMac())
            {
                Debug.LogWarning("[PhonemeFlow macOS] Platform check failed.");
                return false;
            }

            if (!PhonemeFlowFeatureFlags.IsMacNativePathAllowed)
            {
                Debug.LogWarning($"[PhonemeFlow macOS] Native path disabled (reason: {PhonemeFlowFeatureFlags.MacFallbackReason}).");
                return false;
            }

            if (_isInitialized)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(dataPath) || !Directory.Exists(dataPath))
            {
                var message = $"[PhonemeFlow macOS] Invalid data path: {dataPath}";
                Debug.LogWarning(message);
                PhonemeFlowFeatureFlags.ActivateMacFallback("invalid_data_path", message);
                return false;
            }

            string resolvedVoice = ResolveVoice(dataPath, voice);
            if (string.IsNullOrEmpty(resolvedVoice))
            {
                resolvedVoice = DefaultVoice;
            }

            try
            {
                NativeMacOSWrapper.Initialize(dataPath, resolvedVoice);
                _isInitialized = true;
                _dataPath = dataPath;
                _voice = resolvedVoice;
                PhonemeFlowFeatureFlags.ClearMacFallback();
                Debug.Log($"[PhonemeFlow macOS] Initialized eSpeak-ng voice '{resolvedVoice}' at '{dataPath}'.");
                return true;
            }
            catch (Exception ex)
            {
                var message = $"[PhonemeFlow macOS] Native initialization failed: {ex.Message}";
                Debug.LogWarning(message);

                if (ShouldTriggerFatalFallback(ex))
                {
                    PhonemeFlowFeatureFlags.ActivateMacFallback("init_failed", message);
                }

                return false;
            }
        }

        public string GetPhonemes(string text)
        {
            if (!PhonemeFlowFeatureFlags.IsMacNativePathAllowed)
            {
                Debug.LogWarning($"[PhonemeFlow macOS] Native path disabled (reason: {PhonemeFlowFeatureFlags.MacFallbackReason}).");
                return string.Empty;
            }

            if (!_isInitialized)
            {
                Debug.LogWarning("[PhonemeFlow macOS] GetPhonemes called before initialization.");
                return string.Empty;
            }

            string sanitized = SanitizeInput(text);

            try
            {
                string phonemes = NativeMacOSWrapper.GetPhonemes(sanitized);
                if (string.IsNullOrEmpty(phonemes) || IsSentinelValue(phonemes))
                {
                    Debug.LogWarning($"[PhonemeFlow macOS] Native layer returned invalid phoneme string for '{sanitized}'.");
                    PhonemeFlowFeatureFlags.ActivateMacFallback("empty_result", $"Input: {sanitized}");
                    return string.Empty;
                }

                return phonemes;
            }
            catch (Exception ex) when (ex is DllNotFoundException ||
                                       ex is EntryPointNotFoundException ||
                                       ex is AccessViolationException ||
                                       ex is NullReferenceException)
            {
                Debug.LogWarning($"[PhonemeFlow macOS] Native exception: {ex.Message}");
                PhonemeFlowFeatureFlags.ActivateMacFallback("native_exception", ex.Message);
                return string.Empty;
            }
        }

        private static bool IsRunningOnMac()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                   Application.platform == RuntimePlatform.OSXPlayer;
        }

        private static string SanitizeInput(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? " " : text;
        }

        private static bool IsSentinelValue(string result)
        {
            if (string.IsNullOrEmpty(result))
            {
                return true;
            }

            var trimmed = result.Trim();
            return trimmed.Length == 0 ||
                   string.Equals(trimmed, "(null)", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.IndexOf('\0') >= 0;
        }

        private string ResolveVoice(string dataPath, string requestedVoice)
        {
            string candidate = string.IsNullOrWhiteSpace(requestedVoice) ? DefaultVoice : requestedVoice.Trim();

            if (VoiceExists(dataPath, candidate))
            {
                return candidate;
            }

            Debug.LogWarning($"[PhonemeFlow macOS] Voice '{candidate}' not found. Falling back to '{DefaultVoice}'.");
            if (VoiceExists(dataPath, DefaultVoice))
            {
                return DefaultVoice;
            }

            Debug.LogWarning("[PhonemeFlow macOS] Default voice 'en' is missing from phoneme-data.");
            return candidate;
        }

        private static bool VoiceExists(string dataPath, string voice)
        {
            if (string.IsNullOrEmpty(dataPath) || string.IsNullOrEmpty(voice))
            {
                return false;
            }

            string normalizedVoice = voice.Replace('\\', '/').TrimStart('/');
            string voicesRoot = Path.Combine(dataPath, "voices");
            if (!Directory.Exists(voicesRoot))
            {
                return false;
            }

            string directPath = Path.Combine(voicesRoot, normalizedVoice.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(directPath) || Directory.Exists(directPath))
            {
                return true;
            }

            string fileName = Path.GetFileName(normalizedVoice);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(voicesRoot, fileName, SearchOption.AllDirectories))
            {
                if (entry.EndsWith(fileName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldTriggerFatalFallback(Exception ex)
        {
            return ex is DllNotFoundException ||
                   ex is EntryPointNotFoundException ||
                   ex is AccessViolationException ||
                   ex is NullReferenceException;
        }
    }

#endif

}
