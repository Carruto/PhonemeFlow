using System.IO;
using UnityEditor;
using UnityEngine;

namespace PhonemeFlow
{
    internal static class PhonemeDataBuildUtility
    {
        private static readonly string SourcePath = Path.Combine("Assets", "PhonemeFlow", "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");
        private static readonly string DestinationPath = Path.Combine("Assets", "StreamingAssets", "PhonemeFlowResources", "phoneme-data");

        private static bool _copiedForBuild;

        public static bool TryPreparePhonemeData(BuildTarget target)
        {
            _copiedForBuild = false;

            if (!Directory.Exists(SourcePath))
            {
                Debug.LogWarning($"PhonemeFlow: phoneme-data not found at {SourcePath}. Skipping copy for {target} build.");
                return false;
            }

            if (Directory.Exists(DestinationPath))
            {
                Directory.Delete(DestinationPath, true);
            }

            Directory.CreateDirectory(DestinationPath);
            CopyDirectory(SourcePath, DestinationPath);
            WriteManifest(DestinationPath);

            _copiedForBuild = true;
            Debug.Log($"PhonemeFlow: Copied phoneme-data to StreamingAssets for {target} build.");

            return true;
        }

        public static void CleanupTemporaryData()
        {
            if (!_copiedForBuild)
            {
                return;
            }

            if (Directory.Exists(DestinationPath))
            {
                Directory.Delete(DestinationPath, true);
            }

            string metaPath = DestinationPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            _copiedForBuild = false;
            Debug.Log("PhonemeFlow: Removed temporary phoneme-data from StreamingAssets after build.");
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
