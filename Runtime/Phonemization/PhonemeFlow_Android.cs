using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PhonemeFlow
{
#if UNITY_ANDROID

    public class PhonemeFlow_Android : IPhonemeFlow
    {
        const string CacheMarkerFile = ".phoneme_cache_ready";
        const string StreamingAssetRoot = "PhonemeFlowResources/phoneme-data";

        public void Initialize(string dataPath, string voice)
        {
            NativeAndroidWrapper.Initialize(dataPath, voice);
        }

        public string GetPhonemes(string text)
        {
            return NativeAndroidWrapper.GetPhonemes(text);
        }

        public IEnumerator CopyPhonemeDataToPersistentPath(System.Action<string> onComplete, System.Action<float, string> onProgress = null)
        {
            string targetRoot = Path.Combine(Application.persistentDataPath, "PhonemeFlowResources", "phoneme-data");
            string markerPath = Path.Combine(targetRoot, CacheMarkerFile);

            onProgress?.Invoke(0f, "Preparing phoneme data directory...");

            if (!Directory.Exists(targetRoot))
            {
                Directory.CreateDirectory(targetRoot);
            }

            if (File.Exists(markerPath))
            {
                onProgress?.Invoke(1f, "Using cached phoneme data");
                onComplete?.Invoke(targetRoot);
                yield break;
            }

            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var assets = activity.Call<AndroidJavaObject>("getAssets");

            int totalFiles = CountFiles(assets, StreamingAssetRoot);
            if (totalFiles == 0)
            {
                Debug.LogWarning($"[PhonemeFlow Android] No files found under {StreamingAssetRoot}.");
                onProgress?.Invoke(1f, "No phoneme files found");
                onComplete?.Invoke(targetRoot);
                yield break;
            }

            int processed = 0;
            yield return CopyFolderRecursive(assets, StreamingAssetRoot, targetRoot, () =>
            {
                processed++;
                float progress = Mathf.Clamp01((float)processed / totalFiles);
                onProgress?.Invoke(progress, $"Copying phoneme data ({processed}/{totalFiles})...");
            });

            try
            {
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhonemeFlow Android] Failed to write cache marker: {ex.Message}");
            }

            onProgress?.Invoke(1f, "Phoneme data ready");
            onComplete?.Invoke(targetRoot);
        }

        private int CountFiles(AndroidJavaObject assets, string assetPath)
        {
            string[] children = assets.Call<string[]>("list", assetPath);
            if (children == null || children.Length == 0)
                return 0;

            int count = 0;
            foreach (var name in children)
            {
                if (Path.GetExtension(name).ToLowerInvariant() == ".meta")
                    continue;

                string assetChildPath = assetPath + "/" + name;
                string[] grandchildren = assets.Call<string[]>("list", assetChildPath);
                if (grandchildren != null && grandchildren.Length > 0)
                {
                    count += CountFiles(assets, assetChildPath);
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerator CopyFolderRecursive(AndroidJavaObject assets, string assetPath, string targetDir, System.Action onFileCopied)
        {
            string[] children = assets.Call<string[]>("list", assetPath);
            if (children == null || children.Length == 0)
                yield break;

            foreach (var name in children)
            {
                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (ext == ".meta")
                    continue;

                string realName = name.Replace("ExclamationMark_", "!");
                string assetChildPath = assetPath + "/" + name;
                string targetChildPath = Path.Combine(targetDir, realName);

                string[] grandchildren = assets.Call<string[]>("list", assetChildPath);
                if (grandchildren != null && grandchildren.Length > 0)
                {
                    if (!Directory.Exists(targetChildPath))
                    {
                        Directory.CreateDirectory(targetChildPath);
                    }

                    yield return CopyFolderRecursive(assets, assetChildPath, targetChildPath, onFileCopied);
                }
                else
                {
                    string url = Application.streamingAssetsPath + "/" + assetChildPath;
                    UnityWebRequest www = UnityWebRequest.Get(url);
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        byte[] data = www.downloadHandler.data;
                        if (data != null && data.Length > 0)
                        {
                            File.WriteAllBytes(targetChildPath, data);
                        }
                        else
                        {
                            Debug.LogWarning($"[PhonemeFlow] Skipping empty asset '{url}'");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[PhonemeFlow] Failed to copy '{url}': {www.error}");
                    }

                    onFileCopied?.Invoke();
                }
            }
        }
    }

#endif

}
