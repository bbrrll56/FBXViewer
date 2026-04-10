using System.Collections;
using System.IO;
using FBXViewer.Data;
using UnityEngine;

namespace FBXViewer.Converter
{
    /// <summary>
    /// FBX変換の中核クラス
    /// プロジェクトデータから最終的なパッケージを生成
    /// </summary>
    public class FBXConverter : MonoBehaviour
    {
        private ProjectData currentProject;
        private AudioGenerationPipeline audioGenerator;
        private string conversionOutputFolder;

        // 変換完了イベント
        public delegate void OnConversionComplete(bool success, string message);
        public event OnConversionComplete ConversionComplete;

        private void Awake()
        {
            if (audioGenerator == null)
            {
                audioGenerator = GetComponent<AudioGenerationPipeline>();
                if (audioGenerator == null)
                {
                    audioGenerator = gameObject.AddComponent<AudioGenerationPipeline>();
                }
            }
        }

        /// <summary>
        /// コンバーターを明示的に初期化（重要）
        /// </summary>
        public void Initialize()
        {
            if (audioGenerator == null)
            {
                audioGenerator = GetComponent<AudioGenerationPipeline>();
                if (audioGenerator == null)
                {
                    audioGenerator = gameObject.AddComponent<AudioGenerationPipeline>();
                }
            }
            Debug.Log("[FBXConverter] Initialize完了");
        }

        /// <summary>
        /// 新規プロジェクトを作成
        /// </summary>
        public void CreateNewProject(string fbxPath, string projectName)
        {
            // Initialize が呼ばれていなければ呼ぶ
            if (audioGenerator == null)
            {
                Initialize();
            }

            currentProject = new ProjectData
            {
                fbxPath = fbxPath,
                projectName = projectName
            };
            Debug.Log($"[FBXConverter] プロジェクト作成: {projectName}");
        }

        /// <summary>
        /// 現在のプロジェクトを取得
        /// </summary>
        public ProjectData GetCurrentProject()
        {
            return currentProject;
        }

        /// <summary>
        /// パーツ情報を追加（選択FBXオブジェクト付き）
        /// </summary>
        public void AddPartInfo(string partName, string[] descriptions, string[] selectedObjectNames = null)
        {
            if (currentProject == null)
            {
                Debug.LogError("[FBXConverter] プロジェクトが作成されていません");
                return;
            }

            var part = new PartInfo(partName, descriptions, selectedObjectNames);
            currentProject.AddPart(part);
            Debug.Log($"[FBXConverter] パーツ追加: {partName} (説明文: {descriptions.Length}件, オブジェクト数: {selectedObjectNames?.Length ?? 0})");
        }

        /// <summary>
        /// プロジェクトの妥当性を検証
        /// </summary>
        public bool ValidateProject()
        {
            if (currentProject == null)
            {
                Debug.LogError("[FBXConverter] プロジェクトが作成されていません");
                return false;
            }

            return currentProject.IsValid();
        }

        /// <summary>
        /// 音声生成を開始
        /// </summary>
        public IEnumerator GenerateAudio()
        {
            if (!ValidateProject())
            {
                Debug.LogError("[FBXConverter] プロジェクトが無効です");
                yield break;
            }

            // 音声出力フォルダを設定
            conversionOutputFolder = Path.Combine(currentProject.exportPath, "Audio");

            Debug.Log($"[FBXConverter] === 音声生成開始 ===");

            // VoiceVoxサーバーの接続確認
            yield return StartCoroutine(audioGenerator.CheckVoiceVoxConnection());

            // 音声生成実行
            yield return StartCoroutine(audioGenerator.GenerateAudioForProject(currentProject, conversionOutputFolder));

            Debug.Log($"[FBXConverter] === 音声生成完了 ===");
        }

        /// <summary>
        /// 全変換プロセスを実行
        /// </summary>
        public IEnumerator Convert()
        {
            if (!ValidateProject())
            {
                Debug.LogError("[FBXConverter] プロジェクトが無効です。変換を中止します。");
                yield break;
            }

            Debug.Log($"[FBXConverter] === 変換開始 ===");
            Debug.Log($"プロジェクト: {currentProject.projectName}");
            Debug.Log($"パーツ数: {currentProject.GetPartCount()}");
            Debug.Log($"説明文総数: {currentProject.GetTotalDescriptionCount()}");

            // Step 1: 音声生成
            yield return StartCoroutine(GenerateAudio());

            // Step 2: メタデータ生成
            yield return StartCoroutine(GenerateMetadata());

            // Step 3: パッケージ生成
            yield return StartCoroutine(GeneratePackage());

            Debug.Log($"[FBXConverter] === 変換完了 ===");

            // 変換完了イベントを発火（全ステップ完了後）
            ConversionComplete?.Invoke(true, "全ステップが完了しました");
        }

        /// <summary>
        /// プロジェクトメタデータを生成
        /// </summary>
        private IEnumerator GenerateMetadata()
        {
            try
            {
                string metadataPath = Path.Combine(currentProject.exportPath, "project_metadata.json");

                // JSONメタデータを生成
                var metadata = new ProjectMetadata
                {
                    projectName = currentProject.projectName,
                    fbxPath = currentProject.fbxPath,
                    partCount = currentProject.GetPartCount(),
                    parts = new PartMetadata[currentProject.parts.Count]
                };

                for (int i = 0; i < currentProject.parts.Count; i++)
                {
                    var part = currentProject.parts[i];
                    metadata.parts[i] = new PartMetadata
                    {
                        partName = part.partName,
                        descriptionCount = part.descriptions.Length,
                        descriptions = part.descriptions,
                        audioFiles = part.audioFilePaths,
                        selectedObjectNames = part.selectedObjectNames
                    };
                }

                string json = JsonUtility.ToJson(metadata, true);
                File.WriteAllText(metadataPath, json);

                Debug.Log($"[FBXConverter] メタデータ生成: {metadataPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FBXConverter] メタデータ生成エラー: {ex.Message}");
            }

            yield return null;
        }

        /// <summary>
        /// 音声生成中かどうか
        /// </summary>
        public bool IsAudioGenerating()
        {
            return audioGenerator.IsGenerating();
        }

        /// <summary>
        /// Quest用パッケージを生成
        /// </summary>
        private IEnumerator GeneratePackage()
        {
            try
            {
                string packageOutputPath = Path.Combine(currentProject.exportPath, "Package");

                Debug.Log($"[FBXConverter] パッケージ生成開始: {packageOutputPath}");

                bool success = QuestPackager.GeneratePackage(currentProject, packageOutputPath);

                if (success)
                {
                    Debug.Log($"[FBXConverter] パッケージ生成完了: {packageOutputPath}");
                }
                else
                {
                    Debug.LogError("[FBXConverter] パッケージ生成に失敗しました");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FBXConverter] パッケージ生成エラー: {ex.Message}");
            }

            yield return null;
        }
    }

    /// <summary>
    /// JSONシリアライズ用のメタデータ構造体
    /// </summary>
    [System.Serializable]
    public class ProjectMetadata
    {
        public string projectName;
        public string fbxPath;
        public int partCount;
        public PartMetadata[] parts;
    }

    [System.Serializable]
    public class PartMetadata
    {
        public string partName;
        public int descriptionCount;
        public string[] descriptions;
        public string[] audioFiles;
        public string[] selectedObjectNames;  // FBXオブジェクト名のリスト
    }
}
