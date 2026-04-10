using System.IO;
using UnityEngine;

namespace FBXViewer.Viewer
{
    /// <summary>
    /// パッケージからメタデータとアセットを読み込む
    /// </summary>
    public static class PackageReader
    {
        /// <summary>
        /// パッケージフォルダからプロジェクトメタデータを読み込む
        /// </summary>
        public static ProjectMetadataData ReadProjectMetadata(string packageFolderPath)
        {
            string metadataPath = Path.Combine(packageFolderPath, "Metadata", "project_metadata.json");

            if (!File.Exists(metadataPath))
            {
                Debug.LogError($"[PackageReader] メタデータファイルが見つかりません: {metadataPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(metadataPath);
                ProjectMetadataData metadata = JsonUtility.FromJson<ProjectMetadataData>(json);
                Debug.Log($"[PackageReader] メタデータ読み込み完了: {metadata.projectName}");
                return metadata;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PackageReader] メタデータ読み込みエラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// パッケージマニフェストを読み込む
        /// </summary>
        public static PackageManifest ReadManifest(string packageFolderPath)
        {
            string manifestPath = Path.Combine(packageFolderPath, "manifest.json");

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[PackageReader] マニフェストが見つかりません: {manifestPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                PackageManifest manifest = JsonUtility.FromJson<PackageManifest>(json);
                return manifest;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PackageReader] マニフェスト読み込みエラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// パッケージから FBX モデルを読み込む
        /// </summary>
        public static GameObject LoadFBXModel(string packageFolderPath)
        {
            string modelFolderPath = Path.Combine(packageFolderPath, "Model");

            // Model フォルダから .fbx を探す
            if (!Directory.Exists(modelFolderPath))
            {
                Debug.LogError($"[PackageReader] Model フォルダが見つかりません: {modelFolderPath}");
                return null;
            }

            string[] fbxFiles = Directory.GetFiles(modelFolderPath, "*.fbx", SearchOption.TopDirectoryOnly);
            if (fbxFiles.Length == 0)
            {
                Debug.LogError($"[PackageReader] FBX ファイルが見つかりません");
                return null;
            }

            string fbxPath = fbxFiles[0];
            Debug.Log($"[PackageReader] FBX を読み込み: {fbxPath}");

            // Assets からの相対パスで AssetDatabase.LoadAssetAtPath を使う
            // ただし、Package フォルダはプロジェクト外なので、直接呼び出しのみ
            // プロジェクト外の場合、モデルコンポーネントまで直接読み込めない
            // ここではエラー処理としてログ出力

            Debug.LogWarning("[PackageReader] Package フォルダはプロジェクト外です。FBX 直接読み込みは非対応。");
            return null;
        }

        /// <summary>
        /// パッケージから音声ファイルを読み込む
        /// </summary>
        public static AudioClip LoadAudioClip(string packageFolderPath, string audioFileName)
        {
            string audioPath = Path.Combine(packageFolderPath, "Audio", audioFileName);

            if (!File.Exists(audioPath))
            {
                Debug.LogWarning($"[PackageReader] 音声ファイルが見つかりません: {audioPath}");
                return null;
            }

            // WAV ファイルをダイレクトロード（Unity では WAV ファイルをそのまま使える）
            // ここではファイルロードの情報をログ出力
            Debug.Log($"[PackageReader] 音声ファイルを準備: {audioPath}");

            // 実際のロード処理は PlayableAudio または別の方式が必要
            return null;
        }
    }
}
