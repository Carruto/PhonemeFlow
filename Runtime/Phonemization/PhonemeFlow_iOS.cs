#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PhonemeFlow
{
    public class PhonemeFlow_iOS : IPhonemeFlow
    {
        private const string CacheMarkerFile = ".phoneme_cache_ready";
        private string persistentPath = string.Empty;

        public bool Initialize(string dataPath, string voice)
        {
            try
            {
                NativeiOSWrapper.Initialize(dataPath, voice);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhonemeFlow iOS] Initialization failed: {ex.Message}");
                return false;
            }
        }

        public string GetPhonemes(string text)
        {
            return NativeiOSWrapper.GetPhonemes(text);
        }

        public IEnumerator CopyPhonemeDataToPersistentPath(Action<string> onComplete, Action<float, string> onProgress = null)
        {
            persistentPath = Path.Combine(Application.persistentDataPath, "PhonemeFlowResources", "phoneme-data");
            string markerPath = Path.Combine(persistentPath, CacheMarkerFile);

            onProgress?.Invoke(0f, "Preparing phoneme data directory...");

            if (!Directory.Exists(persistentPath))
            {
                Directory.CreateDirectory(persistentPath);
            }

            if (File.Exists(markerPath))
            {
                onProgress?.Invoke(1f, "Using cached phoneme data");
                onComplete?.Invoke(persistentPath);
                yield break;
            }

            string manifestUrl = Path.Combine(Application.streamingAssetsPath, "PhonemeFlowResources", "phoneme-data", "manifest.txt");
            if (!manifestUrl.StartsWith("file://", StringComparison.Ordinal))
            {
                manifestUrl = "file://" + manifestUrl;
            }

            UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestUrl);
            yield return manifestRequest.SendWebRequest();

            if (manifestRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[PhonemeFlow iOS] Failed to load manifest: " + manifestRequest.error);
                onProgress?.Invoke(1f, "Failed to load phoneme manifest");
                yield break;
            }

            string[] files = manifestRequest.downloadHandler.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int totalFiles = files.Length;
            int processed = 0;

            foreach (var relativeFile in files)
            {
                string trimmed = relativeFile.Trim();
                string srcUrl = "file://" + Path.Combine(Application.streamingAssetsPath, "PhonemeFlowResources", "phoneme-data", trimmed);
                string dstPath = Path.Combine(persistentPath, trimmed);

                string dstDir = Path.GetDirectoryName(dstPath);
                if (!Directory.Exists(dstDir))
                {
                    Directory.CreateDirectory(dstDir);
                }

                UnityWebRequest fileRequest = UnityWebRequest.Get(srcUrl);
                yield return fileRequest.SendWebRequest();

                if (fileRequest.result == UnityWebRequest.Result.Success)
                {
                    var data = fileRequest.downloadHandler.data;
                    if (data != null && data.Length > 0)
                    {
                        File.WriteAllBytes(dstPath, data);
                    }
                    else
                    {
                        Debug.LogWarning("[PhonemeFlow iOS] Skipped empty file: " + trimmed);
                    }
                }
                else
                {
                    Debug.LogError($"[PhonemeFlow iOS] Failed to copy: {srcUrl} => {fileRequest.error}");
                }

                processed++;
                float progress = totalFiles > 0 ? Mathf.Clamp01((float)processed / totalFiles) : 1f;
                onProgress?.Invoke(progress, $"Copying phoneme data ({processed}/{totalFiles})...");
            }

            string voiceSafe = Path.Combine(persistentPath, "voices", "ExclamationMark_v");
            string voiceExpected = Path.Combine(persistentPath, "voices", "!v");

            if (Directory.Exists(voiceSafe) && !Directory.Exists(voiceExpected))
            {
                Directory.Move(voiceSafe, voiceExpected);
                Debug.Log("[PhonemeFlow iOS] Renamed ExclamationMark_v to !v for eSpeak-ng.");
            }

            try
            {
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhonemeFlow iOS] Failed to write cache marker: {ex.Message}");
            }

            onProgress?.Invoke(1f, "Phoneme data ready");
            onComplete?.Invoke(persistentPath);
        }
    }
}
#endif
