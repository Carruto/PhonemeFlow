#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Carruto.PhonemeFlow.Editor
{
    /// <summary>
    /// Mirrors the native payload that lives inside the package into the host project's Assets folders.
    /// </summary>
    internal static class PackageBootstrap
    {
        private const string PackageName = "com.carruto.phonemeflow";
        private const string MenuPath = "Tools/PhonemeFlow/Resync Native Payload";
        private const string SyncFileName = "PhonemeFlowSync.json";
        private static readonly string[] PluginSubFolders = { "macOS", "Windows", "Linux" };

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += () => RunBootstrap(false);
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
            var sourceResourceRoot = Path.Combine(packageRoot, "Runtime", "BuildResources", "PhonemeFlowResources", "phoneme-data");

            if (!Directory.Exists(sourcePluginRoot) && !Directory.Exists(sourceResourceRoot))
            {
                Debug.LogWarning("[PhonemeFlow] Package payload folders were not found, nothing to sync.");
                return;
            }

            var targetPluginRoot = Path.Combine(Application.dataPath, "PhonemeFlow", "Plugins", "PhonemeFlow");
            var targetResourceRoot = Path.Combine(Application.dataPath, "StreamingAssets", "PhonemeFlowResources", "phoneme-data");
            var syncFilePath = Path.Combine(projectRoot, "ProjectSettings", SyncFileName);

            var fingerprint = BuildFingerprint(packageRoot, sourcePluginRoot, sourceResourceRoot);
            var cachedState = LoadState(syncFilePath);
            var targetsValid = TargetsLookValid(targetPluginRoot, targetResourceRoot);

            if (!force && cachedState != null && cachedState.sourceHash == fingerprint && targetsValid)
            {
                Debug.Log("[PhonemeFlow] Native payload already in sync, skipping.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPluginRoot) ?? targetPluginRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(targetResourceRoot) ?? targetResourceRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(syncFilePath) ?? projectRoot);

            bool anyCopied = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                anyCopied |= MirrorPluginPayload(sourcePluginRoot, targetPluginRoot);
                anyCopied |= MirrorDirectory(sourceResourceRoot, targetResourceRoot);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

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

            bool copied = false;

            var sourceRootMeta = sourcePluginRoot + ".meta";
            var targetRootMeta = targetPluginRoot + ".meta";
            copied |= CopyFileIfDifferent(sourceRootMeta, targetRootMeta);

            foreach (var sub in PluginSubFolders)
            {
                var source = Path.Combine(sourcePluginRoot, sub);
                var target = Path.Combine(targetPluginRoot, sub);
                if (!Directory.Exists(source))
                {
                    continue;
                }

                copied |= CopyFileIfDifferent(source + ".meta", target + ".meta");
                copied |= MirrorDirectory(source, target);
            }

            return copied;
        }

        private static bool MirrorDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
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
                var folderName = Path.GetFileName(child);
                if (folderName == null)
                {
                    continue;
                }

                var childTarget = Path.Combine(targetDir, folderName);
                changed |= MirrorDirectory(child, childTarget);
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

        private static string BuildFingerprint(string packageRoot, string pluginSourceRoot, string resourceSourceRoot)
        {
            var parts = new List<string>();

            if (Directory.Exists(pluginSourceRoot))
            {
                var pluginFiles = CollectPluginFiles(packageRoot, pluginSourceRoot);
                parts.AddRange(pluginFiles);
            }

            if (Directory.Exists(resourceSourceRoot))
            {
                var resourceFiles = CollectDirectoryFiles(resourceSourceRoot, packageRoot);
                parts.AddRange(resourceFiles);
            }

            var concatenated = string.Join("|", parts.OrderBy(p => p, StringComparer.Ordinal));
            return ComputeHashForString(concatenated);
        }

        private static IEnumerable<string> CollectPluginFiles(string packageRoot, string pluginSourceRoot)
        {
            var files = new List<string>();

            foreach (var sub in PluginSubFolders)
            {
                var source = Path.Combine(pluginSourceRoot, sub);
                if (!Directory.Exists(source))
                {
                    continue;
                }

                files.AddRange(CollectDirectoryFiles(source, packageRoot));
            }

            var rootMeta = pluginSourceRoot + ".meta";
            if (File.Exists(rootMeta))
            {
                var rel = MakeRelativePath(packageRoot, rootMeta);
                files.Add($"{rel}:{ComputeFileHash(rootMeta)}");
            }

            return files;
        }

        private static IEnumerable<string> CollectDirectoryFiles(string root, string relativeTo)
        {
            var results = new List<string>();
            var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                var rel = MakeRelativePath(relativeTo, file);
                var hash = ComputeFileHash(file);
                results.Add($"{rel}:{hash}");
            }

            var directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories).ToList();
            directories.Add(root);
            foreach (var dir in directories)
            {
                var meta = dir + ".meta";
                if (!File.Exists(meta))
                {
                    continue;
                }

                var rel = MakeRelativePath(relativeTo, meta);
                var hash = ComputeFileHash(meta);
                results.Add($"{rel}:{hash}");
            }

            return results.OrderBy(entry => entry, StringComparer.Ordinal);
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

        private static bool TargetsLookValid(string pluginTargetRoot, string resourceTargetRoot)
        {
            if (!Directory.Exists(pluginTargetRoot))
            {
                return false;
            }

            foreach (var sub in PluginSubFolders)
            {
                if (!Directory.Exists(Path.Combine(pluginTargetRoot, sub)))
                {
                    return false;
                }
            }

            if (!Directory.Exists(resourceTargetRoot))
            {
                return false;
            }

            return Directory.GetFiles(resourceTargetRoot, "*", SearchOption.AllDirectories).Length > 0;
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

        [Serializable]
        private sealed class SyncState
        {
            public string sourceHash;
            public long lastSyncUtcTicks;
        }
    }
}
#endif
