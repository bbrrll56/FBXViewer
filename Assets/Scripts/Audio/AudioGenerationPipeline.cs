using System.Collections;
using System.Collections.Generic;
using System.IO;
using FBXViewer.Audio;
using FBXViewer.Data;
using UnityEngine;

namespace FBXViewer.Converter
{
    /// <summary>
    /// 音声生成パイプラインを管理するクラス
    /// 説明文 → VoiceVox → 音声ファイルの処理フロー
    /// </summary>
    public class AudioGenerationPipeline : MonoBehaviour
    {
        private VoiceVoxGenerator voiceVoxGenerator;
        private int speakerId = 1; // デフォルトキャラクター
        private bool isGenerating = false;
        private List<string> generatedAudioPaths = new List<string>();

        public delegate void OnAudioGenerationProgress(int current, int total, string message);
        public event OnAudioGenerationProgress ProgressUpdated;

        public delegate void OnAudioGenerationComplete(bool success, string message);
        public event OnAudioGenerationComplete GenerationComplete;

        private void Awake()
        {
            if (voiceVoxGenerator == null)
            {
                voiceVoxGenerator = GetComponent<VoiceVoxGenerator>();
                if (voiceVoxGenerator == null)
                {
                    voiceVoxGenerator = gameObject.AddComponent<VoiceVoxGenerator>();
                }
            }
        }

        /// <summary>
        /// 初期化（安全な Init）
        /// </summary>
        public void Initialize()
        {
            if (voiceVoxGenerator == null)
            {
                voiceVoxGenerator = GetComponent<VoiceVoxGenerator>();
                if (voiceVoxGenerator == null)
                {
                    voiceVoxGenerator = gameObject.AddComponent<VoiceVoxGenerator>();
                }
            }
            Debug.Log("[AudioGenerationPipeline] Initialize完了");
        }

        /// <summary>
        /// VoiceVoxサーバーの接続状態を確認
        /// </summary>
        public IEnumerator CheckVoiceVoxConnection()
        {
            // 安全チェック：voiceVoxGenerator が null なら初期化
            if (voiceVoxGenerator == null)
            {
                Initialize();
            }

            if (voiceVoxGenerator == null)
            {
                Debug.LogError("[AudioGenerationPipeline] VoiceVoxGenerator の初期化に失敗しました");
                yield break;
            }

            yield return voiceVoxGenerator.CheckServerConnection();
        }

        /// <summary>
        /// プロジェクト内の全パーツの説明文から音声を生成
        /// </summary>
        public IEnumerator GenerateAudioForProject(ProjectData projectData, string audioOutputFolder)
        {
            // 安全チェック：voiceVoxGenerator が null なら初期化
            if (voiceVoxGenerator == null)
            {
                Initialize();
            }

            if (voiceVoxGenerator == null)
            {
                Debug.LogError("[AudioGenerationPipeline] VoiceVoxGenerator の初期化に失敗しました");
                GenerationComplete?.Invoke(false, "VoiceVoxGenerator の初期化に失敗しました");
                yield break;
            }

            if (isGenerating)
            {
                Debug.LogWarning("既に音声生成処理が実行中です");
                yield break;
            }

            isGenerating = true;
            generatedAudioPaths.Clear();

            if (!Directory.Exists(audioOutputFolder))
            {
                Directory.CreateDirectory(audioOutputFolder);
            }

            int totalDescriptions = projectData.GetTotalDescriptionCount();
            int current = 0;

            foreach (var part in projectData.parts)
            {
                for (int i = 0; i < part.descriptions.Length; i++)
                {
                    string description = part.descriptions[i];
                    string audioFileName = $"{part.partName}_{i}.wav";
                    string audioPath = Path.Combine(audioOutputFolder, audioFileName);

                    ProgressUpdated?.Invoke(current + 1, totalDescriptions, $"{part.partName}: {description}");

                    yield return voiceVoxGenerator.GenerateAudio(description, audioPath, speakerId);

                    // ファイルが正常に生成されたか確認
                    if (File.Exists(audioPath))
                    {
                        part.SetAudioPath(i, audioPath);
                        generatedAudioPaths.Add(audioPath);
                        Debug.Log($"[AudioPipeline] 音声生成: {audioFileName}");
                    }
                    else
                    {
                        Debug.LogWarning($"[AudioPipeline] 音声生成失敗: {audioFileName}");
                    }

                    current++;
                }
            }

            isGenerating = false;
            Debug.Log($"[AudioPipeline] 音声生成完了: {generatedAudioPaths.Count}個");
        }

        /// <summary>
        /// キャラクターIDを設定
        /// </summary>
        public void SetSpeakerId(int id)
        {
            speakerId = id;
            Debug.Log($"[AudioPipeline] キャラクターID: {speakerId}");
        }

        /// <summary>
        /// 生成中かどうか
        /// </summary>
        public bool IsGenerating()
        {
            return isGenerating;
        }

        /// <summary>
        /// 生成済みの音声ファイルパスを取得
        /// </summary>
        public List<string> GetGeneratedAudioPaths()
        {
            return new List<string>(generatedAudioPaths);
        }
    }
}
