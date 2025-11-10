using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEditor.PackageManager;
using System.IO;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace PhonemeFlow
{
    public class CopyWASMForWebGL : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string ResourceFolderGuid = "26ecb85469e6e074aa5662516fbae19b";
        private static readonly string LegacySourcePath = Path.Combine("Assets", "PhonemeFlow", "Runtime", "BuildResources", "PhonemeFlowResources");
        private static readonly string DestinationAssetPath = Path.Combine("Assets", "StreamingAssets", "PhonemeFlowResources");

        private static string _cachedSourcePath;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                if (!TryGetSourcePath(out var sourcePath))
                {
                    return;
                }

                var destinationPath = GetAbsolutePath(DestinationAssetPath);

                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, true);
                }

                CopyDirectory(sourcePath, destinationPath);
                Debug.Log("PhonemeFlow: Copied PhonemeFlowResources to StreamingAssets for WebGL build.");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL)
            {
                var destinationPath = GetAbsolutePath(DestinationAssetPath);

                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, true);

                    var destinationMetaPath = GetAbsolutePath(DestinationAssetPath + ".meta");
                    if (File.Exists(destinationMetaPath))
                    {
                        File.Delete(destinationMetaPath); // Optional: remove meta file too
                    }

                    Debug.Log("PhonemeFlow: Removed temporary PhonemeFlowResources from StreamingAssets after build.");
                }
            }
        }

        private static bool TryGetSourcePath(out string sourcePath)
        {
            if (!string.IsNullOrEmpty(_cachedSourcePath) && Directory.Exists(_cachedSourcePath))
            {
                sourcePath = _cachedSourcePath;
                return true;
            }

            var guidAssetPath = AssetDatabase.GUIDToAssetPath(ResourceFolderGuid);
            if (TryResolveAssetPath(guidAssetPath, out var absoluteGuidPath))
            {
                _cachedSourcePath = absoluteGuidPath;
                sourcePath = absoluteGuidPath;
                return true;
            }

            var legacyAbsolutePath = GetAbsolutePath(LegacySourcePath);
            if (Directory.Exists(legacyAbsolutePath))
            {
                _cachedSourcePath = legacyAbsolutePath;
                sourcePath = legacyAbsolutePath;
                return true;
            }

            Debug.LogError("PhonemeFlow: Unable to locate PhonemeFlowResources. Ensure the package is installed correctly.");
            sourcePath = null;
            return false;
        }

        private static bool TryResolveAssetPath(string assetPath, out string absolutePath)
        {
            absolutePath = null;

            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            assetPath = assetPath.Replace('\\', '/');

            if (assetPath.StartsWith("Packages/"))
            {
                var packageInfo = PackageManagerPackageInfo.FindForAssetPath(assetPath);
                if (packageInfo != null)
                {
                    var packagePrefix = $"Packages/{packageInfo.name}";
                    var relativeInPackage = assetPath.Length > packagePrefix.Length
                        ? assetPath.Substring(packagePrefix.Length + 1)
                        : string.Empty;

                    var combined = string.IsNullOrEmpty(relativeInPackage)
                        ? packageInfo.resolvedPath
                        : Path.Combine(packageInfo.resolvedPath, relativeInPackage.Replace('/', Path.DirectorySeparatorChar));

                    absolutePath = Path.GetFullPath(combined);
                    return Directory.Exists(absolutePath);
                }
            }

            absolutePath = GetAbsolutePath(assetPath);
            return Directory.Exists(absolutePath);
        }

        private static string GetAbsolutePath(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath))
            {
                return null;
            }

            if (Path.IsPathRooted(projectRelativePath))
            {
                return projectRelativePath;
            }

            var normalizedRelativePath = projectRelativePath.Replace('/', Path.DirectorySeparatorChar);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedRelativePath));
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
