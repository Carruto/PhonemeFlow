#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Carruto.PhonemeFlow.Editor
{
    /// <summary>
    /// Mirrors the native payload that lives inside the package into the host project's Assets folders.
    /// </summary>
    [InitializeOnLoad]
    internal static class PackageBootstrap
    {
        private const string PackageName = "com.carruto.phonemeflow";
        private const string MenuPath = "Tools/PhonemeFlow/Resync Native Payload";
        private const string SyncFileName = "PhonemeFlowSync.json";
        private static readonly string[] PluginExtensions =
        {
            ".dll", ".dylib", ".so", ".a", ".aar", ".jnilib", ".bundle"
        };
        private static readonly BuildTarget[] PackagePluginTargets =
        {
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneLinux64,
            BuildTarget.Android,
            BuildTarget.iOS,
            BuildTarget.WebGL
        };

        static PackageBootstrap()
        {
            RunBootstrap(false);
        }

        [MenuItem(MenuPath)]
        private static void RunFromMenu()
        {
            RunBootstrap(true);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateMenu()
        {
            return !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void RunBootstrap(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            try
            {
                Execute(force);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhonemeFlow] Native payload sync failed: {ex.Message}");
            }
        }

        private static void Execute(bool force)
        {
            var packageInfo = ResolvePackageInfo();
            if (packageInfo == null)
            {
                Debug.LogWarning("[PhonemeFlow] Unable to locate package directory, skipping sync.");
                return;
            }

            var packageRoot = packageInfo.resolvedPath;
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

            var sourcePluginRoot = Path.Combine(packageRoot, "Plugins", "PhonemeFlow");
            var sourceRuntimeRoot = Path.Combine(packageRoot, "Runtime");
            var sourceEditorRoot = Path.Combine(packageRoot, "Editor");
            var sourceResourceRoot = Path.Combine(packageRoot, "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");

            if (!Directory.Exists(sourcePluginRoot) && !Directory.Exists(sourceResourceRoot))
            {
                Debug.LogWarning("[PhonemeFlow] Package payload folders were not found, nothing to sync.");
                return;
            }

            var targetPackageRoot = Path.Combine(Application.dataPath, "PhonemeFlow");
            var targetPluginRoot = Path.Combine(targetPackageRoot, "Plugins", "PhonemeFlow");
            var targetRuntimeRoot = Path.Combine(targetPackageRoot, "Runtime");
            var targetEditorRoot = Path.Combine(targetPackageRoot, "Editor");
            var targetResourceRoot = Path.Combine(Application.dataPath, "StreamingAssets", "PhonemeFlowResources", "phoneme-data");
            var syncFilePath = Path.Combine(projectRoot, "ProjectSettings", SyncFileName);

            var editorPostInstallSource = Path.Combine(sourceEditorRoot, "PostInstall");
            Func<string, bool> editorSkipPredicate = path => IsWithin(path, editorPostInstallSource);

            var fingerprint = BuildFingerprint(
                packageRoot,
                sourcePluginRoot,
                sourceResourceRoot,
                sourceRuntimeRoot,
                sourceEditorRoot,
                editorSkipPredicate);
            var cachedState = LoadState(syncFilePath);
            var targetsValid = TargetsLookValid(targetPluginRoot, targetResourceRoot, targetRuntimeRoot, targetEditorRoot);

            if (!force && cachedState != null && cachedState.sourceHash == fingerprint && targetsValid)
            {
                Debug.Log("[PhonemeFlow] Native payload already in sync, skipping.");
                return;
            }

            Directory.CreateDirectory(targetPackageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPluginRoot) ?? targetPluginRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(targetResourceRoot) ?? targetResourceRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(syncFilePath) ?? projectRoot);

            bool anyCopied = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                anyCopied |= MirrorPluginPayload(sourcePluginRoot, targetPluginRoot);
                anyCopied |= MirrorDirectory(sourceRuntimeRoot, targetRuntimeRoot);
                RemoveDirectoryIfExists(Path.Combine(targetEditorRoot, "PostInstall"));
                anyCopied |= MirrorDirectory(sourceEditorRoot, targetEditorRoot, editorSkipPredicate);
                anyCopied |= MirrorDirectory(sourceResourceRoot, targetResourceRoot);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            DisablePackagePlugins(packageInfo, sourcePluginRoot);

            if (anyCopied)
            {
                AssetDatabase.Refresh();
                Debug.Log("[PhonemeFlow] Native payload mirrored successfully.");
            }
            else
            {
                Debug.Log("[PhonemeFlow] Native payload already up to date.");
            }

            SaveState(syncFilePath, fingerprint);
        }

        private static PackageInfo ResolvePackageInfo()
        {
            var package = PackageInfo.FindForAssembly(typeof(PackageBootstrap).Assembly);
            if (package != null)
            {
                return package;
            }

            var assetPath = $"Packages/{PackageName}";
            return PackageInfo.FindForAssetPath(assetPath);
        }

        private static bool MirrorPluginPayload(string sourcePluginRoot, string targetPluginRoot)
        {
            if (!Directory.Exists(sourcePluginRoot))
            {
                Debug.LogWarning("[PhonemeFlow] Plugin payload folder not found inside the package.");
                return false;
            }

            return MirrorDirectory(sourcePluginRoot, targetPluginRoot);
        }

        private static bool MirrorDirectory(string sourceDir, string targetDir, Func<string, bool> directorySkip = null)
        {
            if (!Directory.Exists(sourceDir))
            {
                return false;
            }

            if (directorySkip != null && directorySkip(sourceDir))
            {
                return false;
            }

            bool changed = false;
            Directory.CreateDirectory(targetDir);

            changed |= CopyFileIfDifferent(sourceDir + ".meta", targetDir + ".meta");

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                if (fileName == null)
                {
                    continue;
                }

                var targetFile = Path.Combine(targetDir, fileName);
                changed |= CopyFileIfDifferent(file, targetFile);
            }

            foreach (var child in Directory.GetDirectories(sourceDir))
            {
                if (directorySkip != null && directorySkip(child))
                {
                    continue;
                }

                var folderName = Path.GetFileName(child);
                if (folderName == null)
                {
                    continue;
                }

                var childTarget = Path.Combine(targetDir, folderName);
                changed |= MirrorDirectory(child, childTarget, directorySkip);
            }

            return changed;
        }

        private static bool CopyFileIfDifferent(string sourceFile, string targetFile)
        {
            if (!File.Exists(sourceFile))
            {
                return false;
            }

            if (File.Exists(targetFile))
            {
                var sourceHash = ComputeFileHash(sourceFile);
                var targetHash = ComputeFileHash(targetFile);
                if (string.Equals(sourceHash, targetHash, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? targetFile);
            }

            File.Copy(sourceFile, targetFile, true);
            return true;
        }

        private static string BuildFingerprint(
            string packageRoot,
            string pluginSourceRoot,
            string resourceSourceRoot,
            string runtimeSourceRoot,
            string editorSourceRoot,
            Func<string, bool> editorSkipPredicate)
        {
            var parts = new List<string>();

            if (Directory.Exists(pluginSourceRoot))
            {
                parts.AddRange(CollectDirectoryFiles(pluginSourceRoot, packageRoot));
            }

            if (Directory.Exists(resourceSourceRoot))
            {
                var resourceFiles = CollectDirectoryFiles(resourceSourceRoot, packageRoot);
                parts.AddRange(resourceFiles);
            }

            if (Directory.Exists(runtimeSourceRoot))
            {
                parts.AddRange(CollectDirectoryFiles(runtimeSourceRoot, packageRoot));
            }

            if (Directory.Exists(editorSourceRoot))
            {
                parts.AddRange(CollectDirectoryFiles(editorSourceRoot, packageRoot, editorSkipPredicate));
            }

            var concatenated = string.Join("|", parts.OrderBy(p => p, StringComparer.Ordinal));
            return ComputeHashForString(concatenated);
        }

        private static IEnumerable<string> CollectDirectoryFiles(string root, string relativeTo, Func<string, bool> directorySkip = null)
        {
            var results = new List<string>();
            if (!Directory.Exists(root))
            {
                return results;
            }

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (directorySkip != null && directorySkip(current))
                {
                    continue;
                }

                AddFileEntry(current + ".meta");

                foreach (var file in Directory.GetFiles(current, "*", SearchOption.TopDirectoryOnly))
                {
                    AddFileEntry(file);
                }

                foreach (var child in Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    stack.Push(child);
                }
            }

            return results.OrderBy(entry => entry, StringComparer.Ordinal);

            void AddFileEntry(string path)
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var rel = MakeRelativePath(relativeTo, path);
                var hash = ComputeFileHash(path);
                results.Add($"{rel}:{hash}");
            }
        }

        private static string MakeRelativePath(string root, string path)
        {
            if (string.IsNullOrEmpty(root))
            {
                return path;
            }

            root = EnsureTrailingSeparator(Path.GetFullPath(root));
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(root.Length).Replace('\\', '/');
            }

            return fullPath.Replace('\\', '/');
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                path += Path.DirectorySeparatorChar;
            }

            return path;
        }

        private static string ComputeFileHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static string ComputeHashForString(string data)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static void RemoveDirectoryIfExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            Directory.Delete(directoryPath, true);
            var metaPath = directoryPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        private static bool IsWithin(string candidatePath, string potentialParent)
        {
            if (string.IsNullOrEmpty(candidatePath) || string.IsNullOrEmpty(potentialParent))
            {
                return false;
            }

            var normalizedCandidate = NormalizePath(candidatePath);
            var normalizedParent = NormalizePath(potentialParent);
            if (string.IsNullOrEmpty(normalizedCandidate) || string.IsNullOrEmpty(normalizedParent))
            {
                return false;
            }

            if (normalizedCandidate.Equals(normalizedParent, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedCandidate.StartsWith(
                normalizedParent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TargetsLookValid(string pluginTargetRoot, string resourceTargetRoot, string runtimeTargetRoot, string editorTargetRoot)
        {
            return DirectoryHasFiles(pluginTargetRoot)
                && DirectoryHasFiles(resourceTargetRoot)
                && DirectoryHasFiles(runtimeTargetRoot)
                && DirectoryHasFiles(editorTargetRoot);
        }

        private static bool DirectoryHasFiles(string path)
        {
            if (!Directory.Exists(path))
            {
                return false;
            }

            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length > 0;
        }

        private static SyncState LoadState(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<SyncState>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveState(string path, string fingerprint)
        {
            try
            {
                var state = new SyncState
                {
                    sourceHash = fingerprint,
                    lastSyncUtcTicks = DateTime.UtcNow.Ticks
                };

                var json = JsonUtility.ToJson(state, true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhonemeFlow] Failed to persist sync state: {ex.Message}");
            }
        }

        private static void DisablePackagePlugins(PackageInfo packageInfo, string sourcePluginRoot)
        {
            if (packageInfo == null || string.IsNullOrEmpty(sourcePluginRoot) || !Directory.Exists(sourcePluginRoot))
            {
                return;
            }

            bool anyChanged = false;
            var files = Directory.GetFiles(sourcePluginRoot, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (!IsNativePlugin(file))
                {
                    continue;
                }

                var unityPath = ToUnityPath(file, packageInfo);
                if (string.IsNullOrEmpty(unityPath))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(unityPath) as PluginImporter;
                if (importer == null)
                {
                    continue;
                }

                bool changed = false;
                if (importer.GetCompatibleWithEditor())
                {
                    importer.SetCompatibleWithEditor(false);
                    changed = true;
                }

                foreach (var target in PackagePluginTargets)
                {
                    if (importer.GetCompatibleWithPlatform(target))
                    {
                        importer.SetCompatibleWithPlatform(target, false);
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                Debug.Log("[PhonemeFlow] Disabled package plugin imports to avoid duplicate native libraries.");
            }
        }

        private static bool IsNativePlugin(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            foreach (var candidate in PluginExtensions)
            {
                if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ToUnityPath(string absolutePath, PackageInfo packageInfo)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return null;
            }

            var normalized = NormalizePath(absolutePath);
            var assetsRoot = NormalizePath(Application.dataPath);
            if (!string.IsNullOrEmpty(assetsRoot) && normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                var relative = normalized.Substring(assetsRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return $"Assets/{relative}".Replace("\\", "/");
            }

            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
            {
                var packageRoot = NormalizePath(packageInfo.resolvedPath);
                if (normalized.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = normalized.Substring(packageRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return $"Packages/{packageInfo.name}/{relative}".Replace("\\", "/");
                }
            }

            return normalized.Replace("\\", "/");
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        [Serializable]
        private sealed class SyncState
        {
            public string sourceHash;
            public long lastSyncUtcTicks;
        }
    }
}
#endif
