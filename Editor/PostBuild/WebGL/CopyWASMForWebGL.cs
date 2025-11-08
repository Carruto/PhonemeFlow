using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using System.IO;

namespace PhonemeFlow
{
    public class CopyWASMForWebGL : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly string SourcePath = Path.Combine("Assets", "PhonemeFlow", "Runtime", "BuildResources", "PhonemeFlowResources");
        private static readonly string DestinationPath = Path.Combine("Assets", "StreamingAssets", "PhonemeFlowResources");

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                if (Directory.Exists(DestinationPath))
                {
                    Directory.Delete(DestinationPath, true);
                }

                CopyDirectory(SourcePath, DestinationPath);
                Debug.Log("PhonemeFlow: Copied PhonemeFlowResources to StreamingAssets for WebGL build.");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                if (Directory.Exists(DestinationPath))
                {
                    Directory.Delete(DestinationPath, true);
                    File.Delete(DestinationPath + ".meta"); // Optional: remove meta file too
                    Debug.Log("PhonemeFlow: Removed temporary PhonemeFlowResources from StreamingAssets after build.");
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                if (filePath.EndsWith(".meta"))
                {
                    continue; // Skip Unity meta files
                }

                string fileName = Path.GetFileName(filePath);
                string destFile = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destFile, true);
            }

            foreach (string dirPath in Directory.GetDirectories(sourceDir))
            {
                string originalDirName = Path.GetFileName(dirPath);
                string adjustedDirName = originalDirName.Replace("ExclamationMark_", "!");

                if (adjustedDirName != originalDirName)
                {
                    Debug.Log($"PhonemeFlow Build: Converting folder name '{originalDirName}' -> '{adjustedDirName}' for output");
                }

                string destSubdir = Path.Combine(destinationDir, adjustedDirName);
                CopyDirectory(dirPath, destSubdir);
            }
        }
    }
}
