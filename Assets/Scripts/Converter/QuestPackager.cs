using System;
using System.Collections.Generic;
using System.IO;
using FBXViewer.Data;
using UnityEngine;

namespace FBXViewer.Converter
{
    /// <summary>
    /// Quest用パッケージを生成するクラス
    /// </summary>
    public class QuestPackager
    {
        /// <summary>
        /// パッケージの構造を定義
        /// </summary>
        public class PackageStructure
        {
            public string RootPath { get; set; }
            public string AudioPath { get; set; }
            public string ModelPath { get; set; }
            public string MetadataPath { get; set; }

            public PackageStructure(string rootPath)
            {
                RootPath = rootPath;
                AudioPath = Path.Combine(rootPath, "Audio");
                ModelPath = Path.Combine(rootPath, "Model");
                MetadataPath = Path.Combine(rootPath, "Metadata");
            }

            public void CreateDirectories()
            {
                if (!Directory.Exists(RootPath))
                    Directory.CreateDirectory(RootPath);
                if (!Directory.Exists(AudioPath))
                    Directory.CreateDirectory(AudioPath);
                if (!Directory.Exists(ModelPath))
                    Directory.CreateDirectory(ModelPath);
                if (!Directory.Exists(MetadataPath))
                    Directory.CreateDirectory(MetadataPath);
            }
        }

        /// <summary>
        /// Quest用パッケージを生成
        /// </summary>
        public static bool GeneratePackage(ProjectData projectData, string packageOutputPath)
        {
            try
            {
                // パッケージ構造を作成
                var structure = new PackageStructure(packageOutputPath);
                structure.CreateDirectories();

                Debug.Log("[QuestPackager] === パッケージ生成開始 ===");
                Debug.Log($"[QuestPackager] 出力パス: {packageOutputPath}");

                // 1. オーディオファイルをコピー
                CopyAudioFiles(projectData, structure);

                // 2. FBXモデルをコピー
                CopyModelFile(projectData, structure);

                // 3. メタデータをコピー・生成
                CopyMetadata(projectData, structure);

                // 4. マニフェストファイルを生成
                GenerateManifest(projectData, structure);

                Debug.Log("[QuestPackager] === パッケージ生成完了 ===");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[QuestPackager] エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 音声ファイルをコピー
        /// </summary>
        private static void CopyAudioFiles(ProjectData projectData, PackageStructure structure)
        {
            int copiedCount = 0;

            foreach (var part in projectData.parts)
            {
                for (int i = 0; i < part.audioFilePaths.Length; i++)
                {
                    string audioPath = part.audioFilePaths[i];

                    if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
                        continue;

                    string fileName = $"{part.partName}_{i}.wav";
                    string destPath = Path.Combine(structure.AudioPath, fileName);

                    File.Copy(audioPath, destPath, true);
                    copiedCount++;
                }
            }

            Debug.Log($"[QuestPackager] 音声ファイルをコピー: {copiedCount}個");
        }

        /// <summary>
        /// FBXモデルをコピー
        /// </summary>
        private static void CopyModelFile(ProjectData projectData, PackageStructure structure)
        {
            if (string.IsNullOrEmpty(projectData.fbxPath) || !File.Exists(projectData.fbxPath))
            {
                Debug.LogWarning("[QuestPackager] FBXファイルが見つかりません");
                return;
            }

            string fbxFileName = Path.GetFileName(projectData.fbxPath);
            string destPath = Path.Combine(structure.ModelPath, fbxFileName);

            File.Copy(projectData.fbxPath, destPath, true);
            Debug.Log($"[QuestPackager] FBXモデルをコピー: {fbxFileName}");
        }

        /// <summary>
        /// メタデータファイルをコピー
        /// </summary>
        private static void CopyMetadata(ProjectData projectData, PackageStructure structure)
        {
            string srcMetadataPath = Path.Combine(projectData.exportPath, "project_metadata.json");

            if (File.Exists(srcMetadataPath))
            {
                string destPath = Path.Combine(structure.MetadataPath, "project_metadata.json");
                File.Copy(srcMetadataPath, destPath, true);
                Debug.Log("[QuestPackager] メタデータをコピー");
            }
            else
            {
                Debug.LogWarning("[QuestPackager] project_metadata.json が見つかりません");
            }
        }

        /// <summary>
        /// パッケージマニフェストを生成
        /// </summary>
        private static void GenerateManifest(ProjectData projectData, PackageStructure structure)
        {
            var manifest = new PackageManifest
            {
                version = "1.0",
                projectName = projectData.projectName,
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                partCount = projectData.GetPartCount(),
                audioCount = projectData.GetTotalDescriptionCount(),
                parts = new List<PartManifestEntry>()
            };

            // パーツ情報をマニフェストに追加
            foreach (var part in projectData.parts)
            {
                manifest.parts.Add(new PartManifestEntry
                {
                    name = part.partName,
                    descriptionCount = part.descriptions.Length,
                    audioFiles = new List<string>()
                });

                var partEntry = manifest.parts[manifest.parts.Count - 1];
                for (int i = 0; i < part.descriptions.Length; i++)
                {
                    partEntry.audioFiles.Add($"{part.partName}_{i}.wav");
                }
            }

            // JSONで保存
            string json = JsonUtility.ToJson(manifest, true);
            string manifestPath = Path.Combine(structure.RootPath, "manifest.json");
            File.WriteAllText(manifestPath, json);

            Debug.Log($"[QuestPackager] マニフェスト生成: {manifestPath}");
        }
    }

    /// <summary>
    /// パッケージマニフェストの構造
    /// </summary>
    [System.Serializable]
    public class PackageManifest
    {
        public string version;
        public string projectName;
        public string createdAt;
        public int partCount;
        public int audioCount;
        public List<PartManifestEntry> parts = new List<PartManifestEntry>();
    }

    [System.Serializable]
    public class PartManifestEntry
    {
        public string name;
        public int descriptionCount;
        public List<string> audioFiles = new List<string>();
    }
}
