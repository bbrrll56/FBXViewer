using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FBXViewer.Audio
{
    /// <summary>
    /// VoiceVox APIを使用して音声を生成するクラス
    /// ローカルサーバー（http://localhost:50021）と通信
    /// </summary>
    public class VoiceVoxGenerator : MonoBehaviour
    {
        private const string VOICEVOX_API_BASE = "http://localhost:50021";
        private const string AUDIO_QUERY_ENDPOINT = "/audio_query";
        private const string SYNTHESIS_ENDPOINT = "/synthesis";
        private const int DEFAULT_SPEAKER_ID = 1; // デフォルトキャラクター

        /// <summary>
        /// テキストから音声ファイルを生成
        /// </summary>
        public IEnumerator GenerateAudio(string text, string outputPath, int speakerId = DEFAULT_SPEAKER_ID)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogError("テキストが空です");
                yield break;
            }

            // 出力ディレクトリを作成
            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Step 1: 音声クエリを取得（POST）
            string queryUrl = $"{VOICEVOX_API_BASE}{AUDIO_QUERY_ENDPOINT}?text={UnityWebRequest.EscapeURL(text)}&speaker={speakerId}";

            using (UnityWebRequest request = new UnityWebRequest(queryUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(new byte[0]);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[VoiceVox] クエリ取得失敗: {request.error}\nURL: {queryUrl}");
                    yield break;
                }

                string queryJson = request.downloadHandler.text;

                // Step 2: 音声を合成
                string synthesisUrl = $"{VOICEVOX_API_BASE}{SYNTHESIS_ENDPOINT}?speaker={speakerId}";

                using (UnityWebRequest synthesisRequest = new UnityWebRequest(synthesisUrl, "POST"))
                {
                    byte[] queryBytes = Encoding.UTF8.GetBytes(queryJson);
                    synthesisRequest.uploadHandler = new UploadHandlerRaw(queryBytes);
                    synthesisRequest.downloadHandler = new DownloadHandlerBuffer();
                    synthesisRequest.SetRequestHeader("Content-Type", "application/json");
                    synthesisRequest.timeout = 20;

                    yield return synthesisRequest.SendWebRequest();

                    if (synthesisRequest.result == UnityWebRequest.Result.Success)
                    {
                        // 音声ファイルを保存
                        byte[] audioData = synthesisRequest.downloadHandler.data;
                        File.WriteAllBytes(outputPath, audioData);
                        Debug.Log($"[VoiceVox] 音声生成完了: {outputPath}");
                    }
                    else
                    {
                        Debug.LogError($"[VoiceVox] 合成失敗: {synthesisRequest.error}");
                    }
                }
            }
        }

        /// <summary>
        /// VoiceVoxサーバーの接続状態をチェック
        /// </summary>
        public IEnumerator CheckServerConnection()
        {
            string statusUrl = $"{VOICEVOX_API_BASE}/version";

            using (UnityWebRequest request = UnityWebRequest.Get(statusUrl))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[VoiceVox] サーバーに接続しました");
                    yield return true;
                }
                else
                {
                    Debug.LogError($"[VoiceVox] サーバーに接続できません。\nVoiceVoxが起動していることを確認してください。\nエラー: {request.error}");
                    yield return false;
                }
            }
        }
    }
}
