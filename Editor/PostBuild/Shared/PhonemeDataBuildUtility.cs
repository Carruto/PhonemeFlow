using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PhonemeFlow
{
    internal static class PhonemeDataBuildUtility
    {
        private static bool _copiedForBuild;
        private static string _resolvedSourcePath;

        public static bool TryPreparePhonemeData(BuildTarget target)
        {
            _copiedForBuild = false;

            string sourcePath = GetSourcePath();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                Debug.LogWarning($"PhonemeFlow: phoneme-data folder could not be located. Skipping copy for {target} build.");
                return false;
            }

            string destinationPath = GetDestinationPath();

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }

            Directory.CreateDirectory(destinationPath);
            CopyDirectory(sourcePath, destinationPath);
            WriteManifest(destinationPath);

            _copiedForBuild = true;
            Debug.Log($"PhonemeFlow: Copied phoneme-data to StreamingAssets for {target} build (source: {sourcePath}).");

            return true;
        }

        public static void CleanupTemporaryData()
        {
            if (!_copiedForBuild)
            {
                return;
            }

            string destinationPath = GetDestinationPath();

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }

            string metaPath = destinationPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            _copiedForBuild = false;
            Debug.Log("PhonemeFlow: Removed temporary phoneme-data from StreamingAssets after build.");
        }

        private static string GetSourcePath()
        {
            if (!string.IsNullOrEmpty(_resolvedSourcePath) && Directory.Exists(_resolvedSourcePath))
            {
                return _resolvedSourcePath;
            }

            foreach (var candidate in EnumerateSourceCandidates())
            {
                if (Directory.Exists(candidate))
                {
                    _resolvedSourcePath = candidate;
                    return candidate;
                }
            }

            Debug.LogWarning("PhonemeFlow: Unable to resolve phoneme-data source directory. Checked Assets, Packages, and PackageCache.");
            return null;
        }

        private static IEnumerable<string> EnumerateSourceCandidates()
        {
            string dataPath = Application.dataPath;
            string projectRoot = string.IsNullOrEmpty(dataPath) ? null : Path.GetDirectoryName(dataPath);

            if (!string.IsNullOrEmpty(dataPath))
            {
                yield return Path.Combine(dataPath, "PhonemeFlow", "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");
            }

            if (!string.IsNullOrEmpty(projectRoot))
            {
                yield return Path.Combine(projectRoot, "Packages", "com.carruto.phonemeflow", "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");
            }

            PackageInfo packageInfo = null;
            try
            {
                packageInfo = PackageInfo.FindForAssembly(typeof(PhonemeDataBuildUtility).Assembly);
            }
            catch
            {
                // Ignore; fall through to other probes.
            }

            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                yield return Path.Combine(packageInfo.resolvedPath, "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");
            }

            if (!string.IsNullOrEmpty(projectRoot))
            {
                string cacheRoot = Path.Combine(projectRoot, "Library", "PackageCache");
                if (Directory.Exists(cacheRoot))
                {
                    string[] cacheCandidates = Directory.GetDirectories(cacheRoot, "com.carruto.phonemeflow*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in cacheCandidates)
                    {
                        yield return Path.Combine(dir, "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");
                    }
                }
            }
        }

        private static string GetDestinationPath()
        {
            string streamingRoot = Application.streamingAssetsPath;
            if (string.IsNullOrEmpty(streamingRoot))
            {
                streamingRoot = Path.Combine(Application.dataPath, "StreamingAssets");
            }

            return Path.Combine(streamingRoot, "PhonemeFlowResources", "phoneme-data");
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                if (filePath.EndsWith(".meta"))
                {
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                string destFile = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destFile, true);
            }

            foreach (string dirPath in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dirPath);
                string destSubdir = Path.Combine(destinationDir, dirName);
                Directory.CreateDirectory(destSubdir);
                CopyDirectory(dirPath, destSubdir);
            }
        }

        private static void WriteManifest(string destinationDir)
        {
            string[] allFiles = Directory.GetFiles(destinationDir, "*", SearchOption.AllDirectories);
            string manifestPath = Path.Combine(destinationDir, "manifest.txt");

            using (var writer = new StreamWriter(manifestPath))
            {
                foreach (var file in allFiles)
                {
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }

                    string relativePath = file.Replace("\\", "/").Substring(destinationDir.Length + 1);
                    writer.WriteLine(relativePath);
                }
            }

            Debug.Log($"PhonemeFlow: Generated manifest.txt with {allFiles.Length} files.");
        }
    }
}
