using System;
using System.Collections.Generic;
using System.IO;
using FBXViewer.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FBXViewer.Converter
{
    public class QuestPackager
    {
        private const string AudioDirectoryName = "Audio";
        private const string BundleDirectoryName = "Bundle";
        private const string MetadataDirectoryName = "Metadata";
        private const string ModelDirectoryName = "Model";
        private const string AndroidBundleFileName = "model_android.bundle";
        private const string WindowsBundleFileName = "model_windows.bundle";

        public class PackageStructure
        {
            public string RootPath { get; }
            public string AudioPath { get; }
            public string BundlePath { get; }
            public string ModelPath { get; }
            public string MetadataPath { get; }

            public PackageStructure(string rootPath)
            {
                RootPath = rootPath;
                AudioPath = Path.Combine(rootPath, AudioDirectoryName);
                BundlePath = Path.Combine(rootPath, BundleDirectoryName);
                ModelPath = Path.Combine(rootPath, ModelDirectoryName);
                MetadataPath = Path.Combine(rootPath, MetadataDirectoryName);
            }

            public void CreateDirectories()
            {
                Directory.CreateDirectory(RootPath);
                Directory.CreateDirectory(AudioPath);
                Directory.CreateDirectory(BundlePath);
                Directory.CreateDirectory(ModelPath);
                Directory.CreateDirectory(MetadataPath);
            }
        }

        private class BundleBuildResult
        {
            public bool Success;
            public string Platform = string.Empty;
            public string BundleRelativePath = string.Empty;
            public string AssetName = string.Empty;
            public string ErrorMessage = string.Empty;
        }

        public static bool GeneratePackage(ProjectData projectData, string packageOutputPath)
        {
            try
            {
                var structure = new PackageStructure(packageOutputPath);
                structure.CreateDirectories();

                Debug.Log("[QuestPackager] === Package generation started ===");
                Debug.Log($"[QuestPackager] Output path: {packageOutputPath}");

                CopyAudioFiles(projectData, structure);
                CopyModelFile(projectData, structure);

                List<BundleBuildResult> bundleResults = BuildModelBundles(projectData, structure);
                if (bundleResults.Count == 0)
                {
                    Debug.LogWarning("[QuestPackager] No AssetBundle was generated.");
                }

                CopyMetadata(projectData, structure);
                GenerateManifest(projectData, structure, bundleResults);

                Debug.Log("[QuestPackager] === Package generation completed ===");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestPackager] Error: {ex.Message}");
                return false;
            }
        }

        private static void CopyAudioFiles(ProjectData projectData, PackageStructure structure)
        {
            int copiedCount = 0;

            foreach (PartInfo part in projectData.parts)
            {
                for (int i = 0; i < part.audioFilePaths.Length; i++)
                {
                    string audioPath = part.audioFilePaths[i];
                    if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
                    {
                        continue;
                    }

                    string fileName = $"{part.partName}_{i}.wav";
                    string destPath = Path.Combine(structure.AudioPath, fileName);

                    File.Copy(audioPath, destPath, true);
                    copiedCount++;
                }
            }

            Debug.Log($"[QuestPackager] Copied audio files: {copiedCount}");
        }

        private static void CopyModelFile(ProjectData projectData, PackageStructure structure)
        {
            if (string.IsNullOrEmpty(projectData.fbxPath) || !File.Exists(projectData.fbxPath))
            {
                Debug.LogWarning("[QuestPackager] FBX file was not found.");
                return;
            }

            string fbxFileName = Path.GetFileName(projectData.fbxPath);
            string destPath = Path.Combine(structure.ModelPath, fbxFileName);

            File.Copy(projectData.fbxPath, destPath, true);
            Debug.Log($"[QuestPackager] Copied FBX model: {fbxFileName}");
        }

        private static List<BundleBuildResult> BuildModelBundles(ProjectData projectData, PackageStructure structure)
        {
            var results = new List<BundleBuildResult>();

#if UNITY_EDITOR
            BundleBuildResult androidResult = BuildModelBundle(projectData, structure, "Quest", AndroidBundleFileName, BuildTarget.Android);
            if (androidResult.Success)
            {
                results.Add(androidResult);
            }
            else
            {
                Debug.LogWarning($"[QuestPackager] Android AssetBundle failed: {androidResult.ErrorMessage}");
            }

            BundleBuildResult windowsResult = BuildModelBundle(projectData, structure, "Windows", WindowsBundleFileName, BuildTarget.StandaloneWindows64);
            if (windowsResult.Success)
            {
                results.Add(windowsResult);
            }
            else
            {
                Debug.LogWarning($"[QuestPackager] Windows AssetBundle failed: {windowsResult.ErrorMessage}");
            }
#else
            Debug.LogWarning("[QuestPackager] AssetBundle generation requires the Unity Editor.");
#endif

            return results;
        }

#if UNITY_EDITOR
        private static BundleBuildResult BuildModelBundle(ProjectData projectData, PackageStructure structure, string platform, string bundleFileName, BuildTarget buildTarget)
        {
            var result = new BundleBuildResult
            {
                Platform = platform,
                BundleRelativePath = $"{BundleDirectoryName}/{bundleFileName}"
            };

            string assetPath = ToAssetDatabasePath(projectData.fbxPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                result.ErrorMessage = $"FBX path is not inside this Unity project: {projectData.fbxPath}";
                return result;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelAsset == null)
            {
                result.ErrorMessage = $"FBX asset was not found by AssetDatabase: {assetPath}";
                return result;
            }

            var buildMap = new[]
            {
                new AssetBundleBuild
                {
                    assetBundleName = bundleFileName,
                    assetNames = new[] { assetPath }
                }
            };

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                structure.BundlePath,
                buildMap,
                BuildAssetBundleOptions.ChunkBasedCompression,
                buildTarget);

            string bundlePath = Path.Combine(structure.BundlePath, bundleFileName);
            if (manifest == null || !File.Exists(bundlePath))
            {
                result.ErrorMessage = $"BuildPipeline did not create {bundlePath}. Check whether {buildTarget} Build Support is installed.";
                return result;
            }

            AssetDatabase.Refresh();

            result.Success = true;
            result.AssetName = assetPath.ToLowerInvariant();
            Debug.Log($"[QuestPackager] Generated {platform} AssetBundle: {bundlePath}");
            Debug.Log($"[QuestPackager] Bundle asset name: {result.AssetName}");
            return result;
        }
#endif

        private static string ToAssetDatabasePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath;
            }

            string normalizedDataPath = Application.dataPath.Replace('\\', '/');
            if (normalizedPath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + normalizedPath.Substring(normalizedDataPath.Length);
            }

            int assetsIndex = normalizedPath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                return normalizedPath.Substring(assetsIndex + 1);
            }

            return string.Empty;
        }

        private static void CopyMetadata(ProjectData projectData, PackageStructure structure)
        {
            string srcMetadataPath = Path.Combine(projectData.exportPath, "project_metadata.json");

            if (File.Exists(srcMetadataPath))
            {
                string destPath = Path.Combine(structure.MetadataPath, "project_metadata.json");
                File.Copy(srcMetadataPath, destPath, true);
                Debug.Log("[QuestPackager] Copied metadata.");
            }
            else
            {
                Debug.LogWarning("[QuestPackager] project_metadata.json was not found.");
            }
        }

        private static void GenerateManifest(ProjectData projectData, PackageStructure structure, IReadOnlyList<BundleBuildResult> bundleResults)
        {
            BundleBuildResult questBundle = FindBundleResult(bundleResults, "Quest");
            BundleBuildResult fallbackBundle = questBundle ?? (bundleResults.Count > 0 ? bundleResults[0] : null);

            var manifest = new PackageManifest
            {
                version = "2.0",
                projectName = projectData.projectName,
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                partCount = projectData.GetPartCount(),
                audioCount = projectData.GetTotalDescriptionCount(),
                targetPlatform = "Quest",
                modelBundle = fallbackBundle != null ? fallbackBundle.BundleRelativePath : string.Empty,
                modelAssetName = fallbackBundle != null ? fallbackBundle.AssetName : string.Empty,
                metadata = $"{MetadataDirectoryName}/project_metadata.json",
                audioDirectory = AudioDirectoryName,
                bundles = new List<PackageBundleEntry>(),
                parts = new List<PartManifestEntry>()
            };

            foreach (BundleBuildResult bundleResult in bundleResults)
            {
                manifest.bundles.Add(new PackageBundleEntry
                {
                    platform = bundleResult.Platform,
                    bundle = bundleResult.BundleRelativePath,
                    assetName = bundleResult.AssetName
                });
            }

            foreach (PartInfo part in projectData.parts)
            {
                var partEntry = new PartManifestEntry
                {
                    name = part.partName,
                    descriptionCount = part.descriptions.Length,
                    audioFiles = new List<string>()
                };

                for (int i = 0; i < part.descriptions.Length; i++)
                {
                    partEntry.audioFiles.Add($"{part.partName}_{i}.wav");
                }

                manifest.parts.Add(partEntry);
            }

            string json = JsonUtility.ToJson(manifest, true);
            string manifestPath = Path.Combine(structure.RootPath, "manifest.json");
            File.WriteAllText(manifestPath, json);

            Debug.Log($"[QuestPackager] Generated manifest: {manifestPath}");
        }

        private static BundleBuildResult FindBundleResult(IReadOnlyList<BundleBuildResult> bundleResults, string platform)
        {
            for (int i = 0; i < bundleResults.Count; i++)
            {
                if (string.Equals(bundleResults[i].Platform, platform, StringComparison.OrdinalIgnoreCase))
                {
                    return bundleResults[i];
                }
            }

            return null;
        }
    }

    [Serializable]
    public class PackageManifest
    {
        public string version;
        public string projectName;
        public string createdAt;
        public int partCount;
        public int audioCount;
        public string targetPlatform;
        public string modelBundle;
        public string modelAssetName;
        public string metadata;
        public string audioDirectory;
        public List<PackageBundleEntry> bundles = new List<PackageBundleEntry>();
        public List<PartManifestEntry> parts = new List<PartManifestEntry>();
    }

    [Serializable]
    public class PackageBundleEntry
    {
        public string platform;
        public string bundle;
        public string assetName;
    }

    [Serializable]
    public class PartManifestEntry
    {
        public string name;
        public int descriptionCount;
        public List<string> audioFiles = new List<string>();
    }
}
